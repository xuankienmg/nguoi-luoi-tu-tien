from pathlib import Path

from PIL import Image, ImageDraw, ImageOps

GAME = Path(__file__).parent / "game"
VIEWPORT = (600,480)
GRID = (400,320)
CASTER = (162,386)
TARGET = (420,386)
EFFECT_ANCHOR = (240,396)
EFFECT_SCALE = .84
size = (480,432)

def stage():
    with Image.open(GAME / "concept-courtyard-v1.webp") as source:
        plate = ImageOps.fit(source.convert("RGBA"), VIEWPORT, Image.Resampling.LANCZOS)
    bg = Image.alpha_composite(plate, Image.new("RGBA", VIEWPORT, (3,14,21,33)))
    shadow = Image.new("RGBA", VIEWPORT)
    ink = ImageDraw.Draw(shadow)
    for x, y in (CASTER, TARGET):
        ink.ellipse((x-29,y-4,x+29,y+4), fill=(15,18,26,95))
    bg.alpha_composite(shadow)
    with Image.open(GAME / "hero_luyenhu_stand.png") as source:
        art = source.convert("RGBA")
        art = art.crop(art.getchannel("A").getbbox())
        hero = art.resize((round(72*art.width/art.height),72), Image.Resampling.LANCZOS)
    with Image.open(GAME / "quai_bang_ho_hd.png") as source:
        foe = source.convert("RGBA").resize((72,72), Image.Resampling.LANCZOS)
    for sprite, (x,y) in ((hero, CASTER), (foe, TARGET)):
        bg.alpha_composite(sprite, (x-sprite.width//2,y-sprite.height))
    return bg

def composite(bg, effect):
    preview = bg.copy()
    origin = tuple(round(a-b*EFFECT_SCALE) for a,b in zip(CASTER,EFFECT_ANCHOR))
    effect_size = tuple(round(axis*EFFECT_SCALE) for axis in size)
    preview.alpha_composite(effect.resize(effect_size,Image.Resampling.LANCZOS),origin)
    return preview.convert("RGB")

