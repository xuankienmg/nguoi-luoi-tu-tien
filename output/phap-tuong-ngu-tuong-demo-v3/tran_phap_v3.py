from pathlib import Path
import sys
from math import cos, sin, tau, exp, radians
from random import Random
import json
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent
SIZE = (480, 440)
STAMP = 640
CENTER = STAMP / 2
FRAMES = 90
TIMING = [33, 33, 34] * 30
COLORS = {
    "cyan": (53, 177, 251), "rose": (240, 89, 226),
    "jade": (58, 231, 159), "gold": (255, 166, 53),
    "fire": (255, 91, 47),
}


def seal(detail):
    mask = Image.new("L", (STAMP, STAMP))
    d = ImageDraw.Draw(mask)

    def point(radius, angle):
        return CENTER + radius * cos(angle), CENTER + radius * sin(angle)

    def ring(radius, ink, width=2):
        d.ellipse((CENTER-radius, CENTER-radius, CENTER+radius, CENTER+radius), outline=ink, width=width)

    def polygon(radius, sides, angle, ink):
        vertices = [point(radius, angle + i*tau/sides) for i in range(sides)]
        d.line(vertices + vertices[:1], fill=ink, width=3)

    def glyph(x, y, angle, size, i, ink):
        strokes = [
            [(-.53, .52), (0, -.65), (.52, .52)], [(-.5, 0), (0, -.44), (.5, 0), (0, .49), (-.5, 0)],
            [(-.48, -.55), (.47, -.55), (-.36, .55), (.53, .55)], [(0, -.65), (0, .6), (.55, .14)],
            [(-.42, -.55), (.4, -.08), (-.37, .49), (.48, .49)], [(-.48, .5), (-.48, -.4), (.5, -.4), (.5, .5)],
        ]
        pts = [(x + size*(a*cos(angle)-b*sin(angle)), y + size*(a*sin(angle)+b*cos(angle)))
               for a, b in strokes[i % len(strokes)]]
        d.line(pts, fill=ink, width=max(2, int(size/7)), joint="curve")
        if i % 3 == 0:
            d.ellipse((x-1.5, y-1.5, x+1.5, y+1.5), fill=ink)

    if detail == "outer":
        for r, ink, w in [(278, 245, 4), (270, 125, 2), (238, 255, 3), (230, 119, 2), (203, 184, 3)]:
            ring(r, ink, w)
        for i in range(84):
            a = i*tau/84
            d.line([point(231, a), point(237, a)], fill=195, width=2)
            glyph(*point(253, a), a + tau/4, 11 if i % 7 else 15, i * 13, 227)
        for i in range(8):
            a = (i+.5)*tau/8
            x, y = point(214, a)
            d.ellipse((x-18,y-18,x+18,y+18), fill=0, outline=255, width=3)
            d.ellipse((x-13,y-13,x+13,y+13), outline=128, width=2)
            glyph(x, y, a, 16, i, 245)
            d.line([point(279,a), point(297,a)], fill=230, width=3)
            c = point(296,a)
            d.ellipse((c[0]-4,c[1]-4,c[0]+4,c[1]+4), fill=255)
    if detail == "inner":
        for r, ink, w in [(186, 238, 4), (175, 123, 2), (143, 235, 3), (73, 215, 3), (62, 119, 2), (24, 255, 4)]:
            ring(r, ink, w)
        for i in range(36):
            a = i*tau/36
            glyph(*point(158,a), a+tau/4, 13, i*5, 202)
        for i in range(6):
            a = i*tau/6
            vertices = [point(140,a), point(92,a+.26), point(42,a), point(92,a-.26), point(140,a)]
            d.line(vertices, fill=206, width=3, joint="curve")
            d.line([point(42,a), point(25,a)], fill=255, width=3)
        polygon(54, 3, 0, 208)
        polygon(54, 3, tau/6, 208)
        glyph(CENTER, CENTER, 0, 26, 1, 255)
    return mask


OUTER, INNER = seal("outer"), seal("inner")

# x, y, diameter, foreshortening, screen angle, color, onset, angular velocity
GROUND = (240, 351, 418, .30, 0, "fire", .04, 18)
SKY = (240, 143, 383, .33, -5, "cyan", .19, -16)
SKY_RIM = (252, 65, 207, .33, -5, "cyan", .42, 17)
CROSS_A = (224, 195, 323, .41, -31, "rose", .17, -18)
CROSS_B = (256, 194, 304, .39, 36, "rose", .41, 20)
STACK = [
    (240, 84, 215, .30, -4, "gold", .52, 21),
    (240, 140, 376, .31,  3, "gold", .37, -16),
    (240, 199, 263, .31, -3, "gold", .22, 19),
    (240, 258, 406, .29,  1, "gold", .08, -13),
]
NEST = [(240, 206, 399, .78, -10, "jade", .15, 12)]
COMBINED = [
    (240, 78, 226, .32, -5, "gold", .56, -17),
    (240, 121, 371, .35, 3, "gold", .46, 14),
    (240, 169, 242, .37, -4, "gold", .34, -19),
    (219, 230, 296, .42, -31, "gold", .63, -17),
    (263, 224, 287, .38, 36, "gold", .74, 18),
    (240, 351, 420, .29, 0, "gold", .02, 16),
]
SCENES = [
    ("01-thien-an", "Thiên Ấn · Ấn chính", [SKY], "Đại ấn xanh lam với lõi và vành phù văn xoay ngược chiều.", 1),
    ("07-thien-an-vong-ngoai", "Thiên Ấn · Vòng ngoài", [SKY_RIM], "Vành phù văn xanh lam xoay nhẹ, tâm trống.", 1),
    ("02-giao-an", "Giao Ấn", [CROSS_A, CROSS_B], "Hai mặt trận nghiêng cắt nhau.", 1),
    ("03-trung-hoan", "Trùng Hoàn", NEST, "Ba vành đồng tâm xoay xen chiều.", 1),
    ("04-tang-tran", "Tầng Trận", STACK, "Bốn tầng lớn nhỏ trên cùng trục.", 1),
    ("05-dia-an", "Địa Ấn", [GROUND], "Vòng phù văn mở sát mặt đất.", 1),
    ("06-hop-tran", "Hợp Trận", COMBINED, "Địa ấn, ba tầng thiên ấn, hai vòng chéo và các vành lồng.", 1),
]


def ease(x):
    x = min(1, max(0, x))
    return x*x*(3-2*x)


def project(dst, texture, x, y, diameter, squash, tilt, spin):
    layer = texture.rotate(spin, Image.Resampling.BICUBIC)
    layer = layer.resize((round(diameter), round(diameter*squash)), Image.Resampling.LANCZOS)
    layer = layer.rotate(tilt, Image.Resampling.BICUBIC, expand=True)
    origin = (round(x-layer.width/2), round(y-layer.height/2))
    local = Image.new("L", SIZE)
    local.paste(layer, origin)
    return local


def apply_mask(dst, rgb, mask, strength):
    if strength <= 0:
        return
    ink = Image.new("RGBA", SIZE, rgb + (0,))
    ink.putalpha(mask.point([round(i*min(1,strength)) for i in range(256)]))
    dst.alpha_composite(ink)


def render_scene(scene, index):
    slug, _, formations, _, _ = scene
    frame = Image.new("RGBA", SIZE)
    t = index/30
    fade = 1 - ease((t-2.27)/.69)
    for k, (x,y,d,s,tilt,color,start,speed) in enumerate(formations):
        reveal = ease((t - start)/.42)
        visible = reveal*fade
        if visible <= 0:
            continue
        rgb = COLORS[color]
        bright = tuple(round(180+v*.29) for v in rgb)
        spin = (t-start)*speed + k*17
        diameter = d*(.75+.25*reveal)*(0.90 if tilt else 1)
        y += 2*sin(t*2.4+k)*(y<300)
        parts = [(OUTER, 1, spin)]
        if slug != "07-thien-an-vong-ngoai":
            parts.append((INNER, 1, -spin*.81))
        if slug in ("03-trung-hoan", "06-hop-tran") and k == 0:
            parts.append((OUTER, .41, spin*1.53+11))
        for texture, scale, angle in parts:
            mask = project(frame, texture, x, y, diameter*scale, s, tilt*.65, angle)
            apply_mask(frame, rgb, mask.filter(ImageFilter.GaussianBlur(6)), visible)
            apply_mask(frame, rgb, mask.filter(ImageFilter.GaussianBlur(2)), 1.0*visible)
            apply_mask(frame, rgb, mask.filter(ImageFilter.MaxFilter(3)), .57*visible)
            apply_mask(frame, bright, mask, visible*(.92+.07*sin(t*5+k)))
    if fade > 0:
        d = ImageDraw.Draw(frame, "RGBA")
        rng = Random(89)
        low = max(row[1] for row in formations)
        high = min(row[1] for row in formations)
        rgb = COLORS[formations[0][5]]
        for i in range(80):
            x0, y0 = rng.uniform(36,444), rng.uniform(high-43,low+39)
            drift, start, phase = rng.uniform(15,48), rng.uniform(.11,.77), rng.uniform(0,tau)
            l = max(0, t-start)
            x = x0 + 14*sin(l*1.1+phase)
            y = y0 - drift*l
            a = round(185*fade*ease(l/.45)*(.3+.7*sin(l*3+phase)**2))
            if a:
                r = 1.35 if i % 9 == 0 else .65
                d.ellipse((x-r,y-r,x+r,y+r), fill=rgb+(a,))
    return frame


def build():
    catalog = []
    requested = set(sys.argv[1:])
    for scene in SCENES:
        slug, title, formations, description, _ = scene
        catalog.append(dict(id=slug,title=title,description=description,formations=len(formations)))
        if requested and slug not in requested:
            continue
        frames = [render_scene(scene, i) for i in range(FRAMES)]
        frames[45].save(ROOT / (slug+".png"))
        frames[0].save(ROOT / (slug+".webp"), save_all=True, append_images=frames[1:],
                       duration=TIMING, lossless=True, loop=0, method=1)
        # Adjacent transparent frames can be merged by WebP. Verify the encoded duration.
        with Image.open(ROOT / (slug+".webp")) as animation:
            durations = []
            for i in range(animation.n_frames):
                animation.seek(i)
                animation.load()
                durations.append(animation.info["duration"])
            assert sum(durations) == 3000, (slug, durations)
            animation.seek(0)
            assert animation.convert("RGBA").getbbox() is None
            animation.seek(animation.n_frames-1)
            assert animation.convert("RGBA").getbbox() is None
            print(slug, animation.n_frames, sum(durations), flush=True)
    (ROOT/"demo.json").write_text(json.dumps(dict(frameSize=SIZE, fps=30, sampledFrames=FRAMES,
        durationMs=3000, groundAnchor=[240,351], effects=catalog), ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    build()
