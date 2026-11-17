from pathlib import Path
from bisect import bisect_right
from itertools import accumulate
import json

from PIL import Image, ImageChops, ImageDraw, ImageFilter

from stage import CASTER, TARGET, VIEWPORT, EFFECT_SCALE, stage

ROOT = Path(__file__).parent
SOURCE = ROOT / "source"
IMPACT = 45
SEAL_START = 23
SKY_Y = 54
FPS = 30
CELL = (400, 320)
COLUMNS = 10
RESAMPLE = Image.Resampling.LANCZOS
EXPLOSIONS = {
    "dich-linh": ("hoa-dich-linh-chi-no-v3.webp",(192,180),.78),
    "ngu-chuong": ("hoa-ngu-chuong-chi-no-v3.webp",(180,113),.88),
}

def load_explosion(filename):
    with Image.open(ROOT/"explosions"/filename) as clip:
        assert clip.mode == "RGBA" and clip.is_animated
        images, delays = [], []
        for i in range(clip.n_frames):
            clip.seek(i)
            clip.load()
            images.append(clip.convert("RGBA"))
            delays.append(clip.info["duration"])
    return images, list(accumulate(delays))

def portal_cutoff():
    mask = Image.new("L", VIEWPORT, 255)
    ink = ImageDraw.Draw(mask)
    ink.rectangle((0,0,VIEWPORT[0],SKY_Y),fill=0)
    for offset in range(1,12):
        y = SKY_Y+offset
        ink.line((0,y,VIEWPORT[0],y),fill=round(offset*255/12))
    return mask

PORTAL_CUTOFF = portal_cutoff()

def frames(file, size, n, columns):
    width, height = size
    with Image.open(file) as source:
        atlas = source.convert("RGBA")
    return [atlas.crop((i%columns*width, i//columns*height,
                        (i%columns+1)*width, (i//columns+1)*height)) for i in range(n)]

def place(dst, image, anchor, point, scale):
    scaled = image.resize(tuple(round(axis*scale) for axis in image.size), RESAMPLE)
    position = tuple(round(dst_coord-src_coord*scale) for dst_coord,src_coord in zip(point,anchor))
    dst.alpha_composite(scaled,position)

def holy_pillar(frame, cell, anchor):
    stretch = (1.18, 3.18)
    beam = cell.resize(tuple(round(n*k) for n,k in zip(cell.size,stretch)),RESAMPLE)
    origin = tuple(round(at-n*k) for at,n,k in zip(TARGET,anchor,stretch))
    light = Image.new("RGBA", VIEWPORT)
    light.alpha_composite(beam, origin)
    mask = ImageChops.multiply(light.getchannel("A"),PORTAL_CUTOFF)
    light.putalpha(mask)
    for blur, gain, color in ((17,.72,(255,194,94)), (6,.52,(255,234,154))):
        halo = Image.new("RGBA", VIEWPORT, color)
        halo.putalpha(mask.filter(ImageFilter.GaussianBlur(blur)).point(
            lambda a: round(a*gain)))
        frame.alpha_composite(halo)
    frame.alpha_composite(light)

def build():
    kim = json.loads((SOURCE/"kim-long.json").read_text(encoding="utf-8"))
    holy = json.loads((SOURCE/"holy-light.json").read_text(encoding="utf-8"))
    guardian = frames(SOURCE/"kim-long.png",kim["cellSize"],kim["frameCount"],kim["columns"])
    pillar = holy["runtime"]
    beam_frames = frames(SOURCE/"holy-light.png",(pillar["frameWidth"],pillar["frameHeight"]),
                         holy["frameCount"],holy["columns"])
    seals = frames(SOURCE/"holy-light-formation.png",
                   (pillar["frameWidth"],pillar["frameHeight"]),
                   holy["frameCount"],holy["columns"])
    total = 105
    rows = (total+COLUMNS-1)//COLUMNS
    timing = [round((i+1)*1000/FPS)-round(i*1000/FPS) for i in range(total)]
    atlas = Image.new("RGBA",(COLUMNS*CELL[0],rows*CELL[1]))
    effects = []
    bg = stage()
    for i in range(total):
        fx = Image.new("RGBA",VIEWPORT)
        if i < len(guardian):
            place(fx,guardian[i],kim["anchor"],CASTER,EFFECT_SCALE*1.5)
        beam_index = i-IMPACT+holy["impactFrame"]
        seal = None
        if SEAL_START <= i and beam_index < len(seals):
            source_index = (round(holy["impactFrame"]*(i-SEAL_START)/(IMPACT-SEAL_START))
                            if i < IMPACT else beam_index)
            seal = seals[source_index]
            # Preserve the source's separate sky and ground circles at their own anchors.
            place(fx,seal.crop((0,97,seal.width,seal.height)),(pillar["anchor"][0],52),TARGET,1.10)
        if holy["impactFrame"]-2 <= beam_index < len(beam_frames):
            holy_pillar(fx,beam_frames[beam_index],pillar["anchor"])
        if seal is not None:
            place(fx,seal.crop((0,0,seal.width,65)),(pillar["anchor"][0],25),(TARGET[0],SKY_Y),1.46)
        atlas.paste(fx.resize(CELL,RESAMPLE),(i%COLUMNS*CELL[0],i//COLUMNS*CELL[1]))
        effects.append(fx)
    atlas.save(ROOT/"atlas.png")
    overlays = {}
    for key,(file,anchor,scale) in EXPLOSIONS.items():
        bursts, ends = load_explosion(file)
        previews = []
        for i,fx in enumerate(effects):
            shot = Image.alpha_composite(bg,fx)
            if i >= IMPACT:
                offset = (i-IMPACT+.5)*1000/FPS
                blast = bisect_right(ends,offset)
                if blast < len(bursts):
                    place(shot,bursts[blast],anchor,TARGET,scale)
            shot = shot.convert("RGB")
            # Keep empty composited preview frames distinct in the WebP.
            ImageDraw.Draw(shot).point((519+i%39,152-i//3),fill=(72,92,106))
            previews.append(shot)
        previews[IMPACT+(9 if key=="dich-linh" else 18)].save(ROOT/f"poster-{key}.png")
        preview = ROOT/f"preview-{key}.webp"
        previews[0].save(preview,save_all=True,append_images=previews[1:],
                         duration=timing,loop=0,lossless=True,method=3)
        overlays[key] = {"file":f"explosions/{file}","sourceAnchor":list(anchor),
                         "previewScale":scale,"durationMs":ends[-1],
                         "startFrame":IMPACT,"loop":False}
        with Image.open(preview) as clip:
            assert clip.n_frames==total and clip.size==VIEWPORT
            delays=[]
            for i in range(clip.n_frames):
                clip.seek(i)
                clip.load()
                delays.append(clip.info["duration"])
            assert sum(delays)==3500
    meta = {
        "name": "Kim Long Thiên Ấn · Thiên Quang", "version": 4, "fps": FPS,
        "frameCount": total, "durationMs": sum(timing), "impactFrame": IMPACT,
        "impactMs": round(IMPACT*1000/FPS), "atlas": "atlas.png",
        "beam": {"source":"Sprite Fusion Holy Light", "startFrame":IMPACT-2,
                 "impactFrame":IMPACT,"anchorKind":"target-ground"},
        "targetFormation": {"source":"Thien Quang Tran The v1 / formation-sheet",
                            "startFrame":SEAL_START,"skyCenter":[TARGET[0],SKY_Y]},
        "atlasIncludesExplosion":False, "explosionOverlays":overlays,
        "cellSize": list(CELL), "columns": COLUMNS, "rows": rows, "loop": False,
        "anchor": [108,257.3333], "targetAnchor": [280,257.3333],
        "previewLayout": {"size": list(VIEWPORT), "caster": list(CASTER), "target": list(TARGET)}
    }
    (ROOT/"demo.json").write_text(json.dumps(meta,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    with Image.open(ROOT/"atlas.png") as sheet:
        assert sheet.mode=="RGBA" and sheet.size==(4000,3520)
        assert sheet.crop((0,0,400,320)).getbbox() is None
        assert sheet.crop((1600,3200,2000,3520)).getbbox() is None
        def area(i, region):
            x,y=i%10*400,i//10*320
            left,top,right,bottom=region
            return sheet.crop((x+left,y+top,x+right,y+bottom)).getbbox()
        assert not area(SEAL_START-1,(245,0,400,310))
        assert area(IMPACT-5,(245,12,320,65))
        assert area(IMPACT+3,(240,40,320,130))
        assert area(IMPACT+8,(245,150,400,310))
        assert area(IMPACT+8,(0,0,215,230))
    print("Verified both 105-frame previews, separate explosion clips and VFX-only atlas.")

if __name__ == "__main__":
    build()

