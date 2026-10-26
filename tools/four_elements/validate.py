import json
import hashlib
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
catalog = json.loads((Path(__file__).parent / "catalog.json").read_text(encoding="utf-8"))
errors, report, hashes = [], [], set()
directions = {entry.get("castDirection") for entry in catalog}
if directions != {"left", "above", "below", "center"}:
    errors.append("catalog must cover all four cast directions")
for entry in catalog:
    if entry.get("rotateWithTravel") and entry.get("castDirection") not in ("above", "below"):
        errors.append("vertical rotation on nonvertical cast: " + entry["skillId"])
    path = ROOT / "wwwroot" / "img" / "fx" / entry["element"] / (entry["skillId"] + ".png")
    if not path.exists():
        errors.append("missing sheet: " + entry["skillId"])
        continue
    im = Image.open(path)
    if im.mode != "RGBA" or im.size != (768, 768):
        errors.append(f"{entry['skillId']}: expected RGBA 768x768, got {im.mode} {im.size}")
        continue
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if digest in hashes:
        errors.append("duplicate image: " + entry["skillId"])
    hashes.add(digest)
    frames, edge = [], 0
    for i in range(16):
        tile = im.crop((i % 4 * 192, i // 4 * 192, (i % 4 + 1) * 192, (i // 4 + 1) * 192))
        alpha = tile.getchannel("A")
        bbox = alpha.getbbox()
        if not bbox:
            errors.append(f"{entry['skillId']}: empty frame {i}")
        elif min(bbox[0], bbox[1], 192 - bbox[2], 192 - bbox[3]) < 8:
            edge += 1
        frames.append(hashlib.sha256(tile.tobytes()).hexdigest())
    if edge:
        errors.append(f"{entry['skillId']}: {edge} frames violate padding")
    if len(set(frames)) != 16:
        errors.append(f"{entry['skillId']}: repeated animation frames")
    alpha_min, alpha_max = im.getchannel("A").getextrema()
    # A glowing effect may be translucent throughout; resampling can also turn 255 into 254.
    if alpha_min != 0 or alpha_max < 128:
        errors.append(f"{entry['skillId']}: transparent background or visible subject missing")
    icon = ROOT / "wwwroot" / "img" / ("sk_" + entry["skillId"] + ".png")
    if not icon.exists():
        errors.append("missing icon: " + entry["skillId"])
    clip = ROOT / "wwwroot" / "fx-preview" / "assets" / (entry["skillId"] + ".webp")
    if not clip.exists():
        errors.append("missing animation clip: " + entry["skillId"])
    else:
        animation = Image.open(clip)
        if not getattr(animation, "is_animated", False) or animation.n_frames != 17:
            errors.append("animation must have 16 frames plus rest: " + entry["skillId"])
    report.append(dict(skillId=entry["skillId"], sha256=digest, frames=len(set(frames)), edgeFrames=edge))
output = ROOT / "docs" / "four-elements-assets-qa.json"
output.write_text(json.dumps(dict(count=len(report), errors=errors, sheets=report), indent=2), encoding="utf-8")
if errors:
    print("\n".join(errors))
    raise SystemExit(1)
assert len(report) == 32
print("PASS 32 unique RGBA sheets, 512 distinct frames, 8px padding, alpha, 32 cast icons and 32 animated WebP clips.")
