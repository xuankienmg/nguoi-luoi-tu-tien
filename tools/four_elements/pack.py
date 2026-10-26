"""Package AI sheets without painting effects or replacing generated transparency."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageOps, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
PHASES = [[0, .25, 0, 3], [.25, .45, 4, 7], [.45, .6, 8, 11], [.6, .75, 12, 15]]


def package(entry):
    source = HERE / "sources" / (entry["skillId"] + ".png")
    raw = Image.open(source).convert("RGBA")
    if raw.width != raw.height:
        raise ValueError(f"{source}: expected a square 4x4 grid, got {raw.size}")
    if raw.getchannel("A").getextrema()[0] != 0:
        raise ValueError(f"{source}: generated background has no transparency")
    size = raw.width / 4
    frames = [raw.crop(tuple(round(v * size) for v in (i % 4, i // 4, i % 4 + 1, i // 4 + 1))) for i in range(16)]
    # Apply ONE transform to all cells: preserve relative positions and scale progression.
    # Padding protects authored glow from neighbouring cells and atlas sampling.
    sheet = Image.new("RGBA", (768, 768))
    for i, frame in enumerate(frames):
        tile = frame.resize((176, 176), Image.Resampling.LANCZOS)
        sheet.alpha_composite(tile, (i % 4 * 192 + 8, i // 4 * 192 + 8))
    target = ROOT / "wwwroot" / "img" / "fx" / entry["element"] / (entry["skillId"] + ".png")
    target.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(target, optimize=True)
    bounds = [sheet.crop((i % 4 * 192, i // 4 * 192, (i % 4 + 1) * 192, (i // 4 + 1) * 192)).getchannel("A").point(lambda a: 255 if a > 12 else 0).getbbox() for i in range(16)]
    visible = [b for b in bounds if b]
    union = [min(b[0] for b in visible), min(b[1] for b in visible), max(b[2] for b in visible), max(b[3] for b in visible)]
    x = (union[0] + union[2]) / 2
    y = union[3] if entry["motion"] == "ground" else (union[1] + union[3]) / 2
    index = 6 if entry["motion"] == "projectile" else 8
    icon = sheet.crop((index % 4 * 192, index // 4 * 192, (index % 4 + 1) * 192, (index // 4 + 1) * 192))
    icon = ImageOps.contain(icon.crop(icon.getbbox()), (88, 88), Image.Resampling.LANCZOS)
    icon_canvas = Image.new("RGBA", (96, 96))
    icon_canvas.alpha_composite(icon, ((96 - icon.width) // 2, (96 - icon.height) // 2))
    icon_canvas.save(ROOT / "wwwroot" / "img" / ("sk_" + entry["skillId"] + ".png"), optimize=True)
    animation = [sheet.crop((i % 4 * 192, i // 4 * 192, (i % 4 + 1) * 192, (i // 4 + 1) * 192)) for i in range(16)]
    animation.append(Image.new("RGBA", (192, 192)))
    clip = ROOT / "wwwroot" / "fx-preview" / "assets" / (entry["skillId"] + ".webp")
    clip.parent.mkdir(parents=True, exist_ok=True)
    animation[0].save(clip, save_all=True, append_images=animation[1:],
                      duration=[138, 137, 138, 137] + [110] * 4 + [83, 82, 83, 82] * 2 + [750],
                      loop=0, lossless=True, method=4)
    return dict(skillId=entry["skillId"], name=entry["name"], element=entry["element"],
                src=f'img/fx/{entry["element"]}/{entry["skillId"]}.png', frameWidth=192, frameHeight=192,
                columns=4, frameCount=16, frameMs=100, anchorX=x, anchorY=y,
                motion=entry["motion"], castDirection=entry["castDirection"], rotateWithTravel=entry.get("rotateWithTravel", False), scale=1.65, phases=PHASES, visibleBounds=union,
                targetScale=.55 if "_BASIC_" in entry.get("code", "") or entry["skillId"] in ["tho_linh_dan", "loi_quang_cau", "bang_linh_cham", "phong_linh_tien"] else 1.35)


def contacts(specs):
    # Overview uses formation / impact / fading frames so each spell's progression is reviewable.
    overview = Image.new("RGB", (1024, ((len(specs) + 3) // 4) * 282), "#101a25")
    draw = ImageDraw.Draw(overview)
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 14)
    except OSError:
        font = ImageFont.load_default()
    for k, spec in enumerate(specs):
        x, y = k % 4 * 256, k // 4 * 282
        sheet = Image.open(ROOT / "wwwroot" / spec["src"])
        draw.text((x + 8, y + 8), spec["name"], font=font, fill="#d5e1ec")
        for n, f in enumerate([2, 6, 9, 14]):
            tile = sheet.crop((f % 4 * 192, f // 4 * 192, (f % 4 + 1) * 192, (f // 4 + 1) * 192)).resize((124, 124))
            overview.paste(tile, (x + n % 2 * 128, y + 28 + n // 2 * 126), tile)
    output = ROOT / "wwwroot" / "fx-preview" / "four-elements-contact.jpg"
    overview.save(output, quality=92)


def passive_icons(specs):
    data = json.loads((ROOT / "docs" / "four-elements-skills.json").read_text(encoding="utf-8"))
    count = 0
    for skill in data:
        if skill["Category"] not in ("Căn cơ", "Bị động"):
            continue
        same = [s for s in specs if s["element"] == skill["Element"]]
        if len(same) != 8:
            continue
        index = [s for s in data if s["Element"] == skill["Element"] and s["Category"] in ("Căn cơ", "Bị động")].index(skill)
        source = same[index % 8]
        frame = [5, 7, 8, 9, 6, 10, 4, 11, 3, 12, 2][index]
        sheet = Image.open(ROOT / "wwwroot" / source["src"])
        tile = sheet.crop((frame % 4 * 192, frame // 4 * 192, (frame % 4 + 1) * 192, (frame // 4 + 1) * 192))
        if not tile.getbbox():
            raise ValueError("empty passive icon source: " + skill["Id"])
        icon = ImageOps.contain(tile.crop(tile.getbbox()), (88, 88), Image.Resampling.LANCZOS)
        output = Image.new("RGBA", (96, 96))
        output.alpha_composite(icon, ((96 - icon.width) // 2, (96 - icon.height) // 2))
        output.save(ROOT / "wwwroot" / "img" / ("sk_" + skill["Id"] + ".png"), optimize=True)
        count += 1
    return count


def demo(specs):
    chosen = [next(s for s in specs if s["skillId"] == key) for key in
              ["hon_nguyen_tho", "hon_nguyen_loi", "hon_nguyen_bang", "hon_nguyen_phong"]]
    labels = ["THỔ", "LÔI", "BĂNG", "PHONG"]
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 17)
    sheets = [Image.open(ROOT / "wwwroot" / s["src"]) for s in chosen]
    frames = []
    for frame in range(17):
        canvas = Image.new("RGBA", (440, 480), "#101a25")
        draw = ImageDraw.Draw(canvas)
        for i, sheet in enumerate(sheets):
            x, y = i % 2 * 220, i // 2 * 240
            draw.text((x + 14, y + 12), labels[i], font=font, fill="#d9e5eb")
            if frame < 16:
                tile = sheet.crop((frame % 4 * 192, frame // 4 * 192, (frame % 4 + 1) * 192, (frame // 4 + 1) * 192))
                canvas.alpha_composite(tile, (x + 14, y + 42))
        frames.append(canvas)
    frames[0].save(ROOT / "wwwroot" / "fx-preview" / "four-elements-demo.webp", save_all=True, append_images=frames[1:],
                   duration=[138, 137, 138, 137] + [110] * 4 + [83, 82, 83, 82] * 2 + [750], loop=0, lossless=True, method=4)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--partial", action="store_true", help="Package available sheets while generation is in progress")
    args = parser.parse_args()
    catalog = json.loads((HERE / "catalog.json").read_text(encoding="utf-8"))
    specs = []
    for entry in catalog:
        if args.partial and not (HERE / "sources" / (entry["skillId"] + ".png")).exists():
            continue
        specs.append(package(entry))
    (ROOT / "wwwroot" / "js" / "four-elements-fx-data.js").write_text("/* Generated from tools/four_elements/catalog.json by pack.py. */\nconst FOUR_ELEMENTS_FX_DATA = " + json.dumps(specs, ensure_ascii=False, indent=2) + ";\n", encoding="utf-8")
    (ROOT / "docs" / "four-elements-art-prompts.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2), encoding="utf-8")
    contacts(specs)
    if len(specs) == 32:
        demo(specs)
    icons = passive_icons(specs) if (ROOT / "docs" / "four-elements-skills.json").exists() else 0
    print(f"Packaged {len(specs)}/32 RGBA sprite sheets, animated WebP clips and cast icons; {icons}/44 passive icons.")
