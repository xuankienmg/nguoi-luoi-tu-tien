from pathlib import Path
from math import cos, sin, pi, tau
import json
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

from stage import CASTER, TARGET, VIEWPORT, composite, stage, size
from tran_phap_v3 import OUTER, INNER, SKY, project

ROOT = Path(__file__).parent
CELL = (320, 288)
TIMING = [33, 33, 34] * 30
VARIANTS = {
    "kim-long": ((255, 176, 42), 3, 1.22, 2.1, 0.12),
    "loi-than": ((110, 135, 255), 4, 1.68, 2.9, 0.36),
    "hoa-phuong": ((255, 93, 38), 4, 0.85, 2.4, 0.24),
    "huyen-vu": ((45, 235, 163), 3, -1.12, 1.5, 0.13),
    "nguyet-kiem": ((172, 146, 255), 2, -1.54, 2.3, 0.07),
}

def ease(first, last, t):
    x = max(0, min(1, (t - first) / (last - first)))
    return x*x*(3-2*x)

def tinted(mask, color, intensity):
    layer = Image.new("RGBA", mask.size, color)
    layer.putalpha(mask.point([min(255, round(i*intensity)) for i in range(256)]))
    return layer

def formation(t):
    x, _, diameter, squash, tilt, _, onset, speed = SKY
    appear = ease(onset, onset+.42, t)
    strength = appear*(1-ease(2.27, 2.94, t))
    base = (53, 177, 251)
    layers = Image.new("RGBA", (480, 440))
    for texture, direction in ((OUTER, 1), (INNER, -.81)):
        mask = project(layers, texture, x, 278, diameter*(.75+.25*appear)*.90,
                       squash, tilt*.65, (t-onset)*speed*direction)
        layers.alpha_composite(tinted(mask.filter(ImageFilter.GaussianBlur(4)), base, strength*1.4))
        layers.alpha_composite(tinted(mask.filter(ImageFilter.MaxFilter(3)), base, strength*.72))
        layers.alpha_composite(tinted(mask, (212, 244, 255), strength))
    return layers.crop((0, 0, *size))

def stream(t, config, front):
    color, lanes, twist, speed, wave = config
    rise = ease(.18, 1.06, t)
    release = ease(1.94, 2.64, t)
    visible = ease(.09, .47, t)*(1-ease(2.51, 2.94, t))
    ribbon = Image.new("L", size)
    core = Image.new("L", size)
    body = ImageDraw.Draw(ribbon)
    light = ImageDraw.Draw(core)
    for lane in range(lanes):
        orbit = tau*lane/lanes
        for packet in range(2):
            head = (packet/2 + lane*.057 + t*(.35+.075*speed))%1
            length = .032 + .005*((packet+lane)%3)
            previous = None
            for step in range(10):
                q = step/9
                p = head-length*(1-q)
                if p <= 0:
                    continue
                angle = orbit + t*.63*speed + p*twist*tau
                depth = sin(angle)
                if (depth > .10) != front:
                    previous = None
                    continue
                radius = (70+87*sin(pi*p)**.7)*(.53+.47*rise)
                radius *= 1+release*.14*p+wave*.22*sin(p*9+t*7)
                elevation = p*(48+178*rise+43*release)
                x = 240+radius*cos(angle)
                y = 285-elevation+13*depth
                shape = ease(0,.73,q)*(1-ease(.82,1,q))
                lifetime = ease(0,.09,p)*(1-ease(.88,1,p))
                strength = round(255*shape*lifetime*visible*(.95 if front else .73))
                if previous is not None and strength:
                    width = max(1,round(1+4*shape))
                    body.line([previous,(x,y)],fill=strength,width=width)
                    light.line([previous,(x,y)],fill=strength,width=max(1,width//2))
                previous = (x,y)
    result = tinted(ribbon.filter(ImageFilter.GaussianBlur(7)),color,1.65)
    result.alpha_composite(tinted(ribbon.filter(ImageFilter.GaussianBlur(2)),color,1.4))
    result.alpha_composite(tinted(ribbon,color,.8))
    white = tuple(round(227+c*.10) for c in color)
    result.alpha_composite(tinted(core,white,1.20))
    return result

def render(t, art, config):
    effect = Image.new("RGBA", size)
    if t <= .04 or t >= 2.94:
        return effect
    effect.alpha_composite(formation(t))
    effect.alpha_composite(stream(t, config, front=False))
    appear = ease(.25, 1.19, t)
    fade = 1-ease(1.99, 2.80, t)
    if appear and fade:
        factor = (.85+.15*appear+.009*sin(t*5))
        scale = min(338/art.width, 263/art.height)*factor
        sprite = art.resize((round(art.width*scale), round(art.height*scale)),
                            Image.Resampling.LANCZOS)
        reveal = ease(.27, 1.19, t)
        crest = ease(.27,.41,t)*(1-ease(.99,1.19,t))
        dissolve = ease(2.02, 2.76, t)
        alpha = sprite.getchannel("A")
        mask = Image.new("L", sprite.size)
        energy = Image.new("L", sprite.size)
        rows = ImageDraw.Draw(mask)
        light = ImageDraw.Draw(energy)
        boundary = sprite.height*(1-1.12*reveal)
        for column in range(0, sprite.width, 3):
            # The edges of the condensation front ripple across the sprite.
            wave = (sin(column*.061-t*14)+sin(column*.115+t*9)*.35)*7*crest
            edge = boundary+wave
            if edge+18 < sprite.height:
                rows.rectangle((column, max(0,round(edge+18)), column+2, sprite.height),
                               fill=round(255*fade))
            for y in range(max(0,round(edge-12)),min(sprite.height,round(edge+18))):
                cover = ease(-12,18,y-edge)
                rows.line((column,y,column+2,y),fill=round(255*cover*fade))
                glow = (1-ease(2,17,abs(y-edge)))*crest
                light.line((column,y,column+2,y),fill=round(240*glow))
        if dissolve:
            fall = Image.new("L",sprite.size)
            fall_rows = ImageDraw.Draw(fall)
            for y in range(sprite.height):
                keep = 1-ease(.05,1,y/sprite.height)*dissolve
                fall_rows.line((0,y,sprite.width,y),fill=round(255*keep))
            mask = ImageChops.multiply(mask,fall)
        sprite.putalpha(ImageChops.multiply(alpha, mask))
        origin = (240-sprite.width//2,round(285+2*sin(t*4))-sprite.height)
        rim = ImageChops.multiply(alpha,energy)
        color = config[0]
        effect.alpha_composite(tinted(rim.filter(ImageFilter.GaussianBlur(7)),color,1.65),origin)
        effect.alpha_composite(sprite,origin)
        effect.alpha_composite(tinted(rim,tuple(round(206+c*.18) for c in color),.83),origin)
    effect.alpha_composite(stream(t, config, front=True))
    return effect

def build(identifier):
    folder = ROOT / identifier
    with Image.open(folder / "master.png") as source:
        art = source.convert("RGBA")
    bounds = art.getchannel("A").point(lambda opacity: 255 if opacity > 22 else 0).getbbox()
    art = art.crop(bounds)
    frames = [render(i/30, art, VARIANTS[identifier]) for i in range(90)]
    bg = stage()
    preview = [composite(bg, frame) for frame in frames]
    # Subtle movement of the two foot shadows keeps empty preview frames distinct.
    for i, frame in enumerate(preview):
        shadow = Image.new("RGBA", VIEWPORT)
        ink = ImageDraw.Draw(shadow)
        for x,y in (CASTER, TARGET):
            ink.ellipse((x-20,y,x+20,y+3),fill=(26,22,27,32+i%30))
        frame.paste(shadow,mask=shadow.getchannel("A"))
    preview[39].save(folder / "poster.png")
    preview[0].save(folder / "preview.webp", save_all=True, append_images=preview[1:],
                    duration=TIMING, loop=0, lossless=True, method=3)
    atlas = Image.new("RGBA", (CELL[0]*10, CELL[1]*9))
    for i, frame in enumerate(frames):
        atlas.paste(frame.resize(CELL, Image.Resampling.LANCZOS),
                    ((i%10)*CELL[0],(i//10)*CELL[1]))
    atlas.save(folder / "atlas.png", compress_level=6)
    metadata = {
        "id": identifier, "version": 3, "artSource": "OpenAI ImageGen", "formationSource": "tran-phap-v3/01-thien-an",
        "sheet": "atlas.png", "cellSize": list(CELL), "columns": 10, "rows": 9,
        "fps": 30, "frameCount": 90, "durationMs": 3000, "loop": False,
        "anchor": [160,264], "anchorMeaning": "Caster ground position",
        "facing": "right-three-quarter", "apparitionReference": "Kim Tien / Than Binh Tram",
        "maxOrbitStreaks": VARIANTS[identifier][1]*2,
        "previewLayout": {"size": list(VIEWPORT), "caster": list(CASTER), "target": list(TARGET)},
        "phasesMs": {"gather": 0, "manifest": 270, "hold": 1190, "release": 1940, "end": 3000}
    }
    (folder / "demo.json").write_text(json.dumps(metadata, indent=2)+"\n", encoding="utf-8")
    with Image.open(folder / "preview.webp") as clip:
        durations = []
        for i in range(clip.n_frames):
            clip.seek(i)
            clip.load()
            durations.append(clip.info["duration"])
        assert durations == TIMING, durations
    with Image.open(folder / "atlas.png") as packed:
        assert packed.mode == "RGBA" and packed.size == (3200, 2592)
        assert packed.crop((0,0,320,288)).getbbox() is None
        assert packed.crop((2880,2304,3200,2592)).getbbox() is None
        assert packed.crop((2880,864,3200,1152)).getbbox() is not None
    print(f"{identifier}: 90 frames, 3000 ms, 3200x2592 RGBA atlas", flush=True)

if __name__ == "__main__":
    for name in sys.argv[1:] or VARIANTS:
        build(name)

