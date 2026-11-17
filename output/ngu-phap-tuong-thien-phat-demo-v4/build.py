"""Ngũ Pháp Tướng · Thiên Phạt v4 — giữ chuyển động procedural mượt của v1, nâng độ nét/chi tiết theo ảnh mẫu.

Mở rộng khuôn Kim Long Thiên Ấn · Thiên Quang v4 sang bốn pháp tướng còn lại.
Mỗi pháp tướng có kiểu đánh riêng (model khác nhau, không chỉ đổi màu):

- loi-than    : ba tia sét phân nhánh giáng từ thiên ấn (tham khảo Sprite Fusion Lightning).
- hoa-phuong  : hỏa phượng lao xuống như sao băng, đuôi lửa + cột lửa hạt (Fire / Embers).
- huyen-vu    : thủy triền xoáy tụ quanh mục tiêu rồi phun thủy trụ (Bubbles / Rain splash).
- nguyet-kiem : trăng khuyết kết trong thiên ấn, ba nhát nguyệt trảm + cự trảm dọc (Slash).

Chạy: py build.py [id ...]
"""
from pathlib import Path
from bisect import bisect_right
from itertools import accumulate
from math import atan2, cos, sin, pi, tau, hypot
import json
import random
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps

from stage import CASTER, TARGET, VIEWPORT, EFFECT_SCALE, stage

ROOT = Path(__file__).parent
SOURCE = ROOT / "source"
PHAP_TUONG = ROOT.parent / "phap-tuong-ngu-tuong-demo-v3"
FPS = 30
TOTAL = 105
CELL = (400, 320)
COLUMNS = 10
SEAL_START = 23
SKY_Y = 54
RESAMPLE = Image.Resampling.LANCZOS
TX, TY = TARGET

VARIANTS = {
    "loi-than": dict(name="Lôi Thần · Cửu Tiêu Thiên Phạt", element="loi", impact=45,
                     color=(150, 120, 255), core=(236, 236, 255), sky_beam=True,
                     poster=52, desc="Ba tia sét phân nhánh giáng từ thiên ấn tím."),
    "hoa-phuong": dict(name="Hỏa Phượng · Liệt Diễm Phần Thiên", element="hoa", impact=47,
                       color=(255, 110, 36), core=(255, 240, 190), sky_beam=True,
                       poster=55, desc="Hỏa phượng lao xuống như sao băng, nổ thành cột lửa."),
    "huyen-vu": dict(name="Huyền Vũ · Trấn Hải Thủy Trụ", element="thuy", impact=46,
                     color=(40, 200, 220), core=(225, 250, 255), sky_beam=False,
                     poster=54, desc="Thủy triền xoáy tụ dưới địa ấn rồi phun thủy trụ."),
    "nguyet-kiem": dict(name="Nguyệt Kiếm · Tàn Nguyệt Tứ Trảm", element="bang", impact=43,
                        color=(170, 150, 255), core=(245, 245, 255), sky_beam=True,
                        poster=58, desc="Trăng khuyết kết trong thiên ấn, ba nhát nguyệt trảm và một cự trảm."),
}
EXPLOSION_ANCHOR = (180, 113)
EXPLOSION_SCALE = .88


# ---------------------------------------------------------------- helpers
def ease(a, b, t):
    x = max(0.0, min(1.0, (t - a) / (b - a)))
    return x * x * (3 - 2 * x)


def frames(file, size, n, columns):
    w, h = size
    with Image.open(file) as src:
        atlas = src.convert("RGBA")
    return [atlas.crop((i % columns * w, i // columns * h, (i % columns + 1) * w, (i // columns + 1) * h))
            for i in range(n)]


def place(dst, image, anchor, point, scale):
    scaled = image.resize(tuple(round(a * scale) for a in image.size), RESAMPLE)
    pos = tuple(round(p - s * scale) for p, s in zip(point, anchor))
    dst.alpha_composite(scaled, pos)


def tint(image, color):
    """Đổi màu thiên ấn vàng của Holy Light sang màu hệ, giữ độ sáng và alpha."""
    gray = ImageOps.grayscale(image.convert("RGB"))
    out = ImageOps.colorize(gray, black=(0, 0, 0), mid=color, white=(255, 255, 255)).convert("RGBA")
    out.putalpha(image.getchannel("A"))
    return out


def glow(dst, mask, color, core, strength=1.0, blurs=((16, .75), (6, .9)), core_gain=1.0):
    if strength <= 0:
        return
    for radius, gain in blurs:
        layer = Image.new("RGBA", dst.size, color)
        layer.putalpha(mask.filter(ImageFilter.GaussianBlur(radius)).point(
            lambda a, g=gain * strength: min(255, round(a * g))))
        dst.alpha_composite(layer)
    layer = Image.new("RGBA", dst.size, core)
    layer.putalpha(mask.point(lambda a, g=core_gain * strength: min(255, round(a * g))))
    dst.alpha_composite(layer)


def mix(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def star(ink, x, y, r, fill):
    """Sao bốn cánh lấp lánh."""
    t = r * .22
    ink.polygon([(x, y - r), (x + t, y - t), (x + r, y), (x + t, y + t), (x, y + r), (x - t, y + t),
                 (x - r, y), (x - t, y - t)], fill=fill)


def leaf(ink, root, angle, length, width, fill, vein=None, edge=None):
    """Lông vũ / lưỡi lửa hình lá: nhọn hai đầu, phình ở 40%."""
    left, right = [], []
    dx, dy = cos(angle), sin(angle)
    for k in range(13):
        t = k / 12
        w = width * sin(pi * t) ** .75 * (1 - .35 * t)
        cx, cy = root[0] + dx * length * t, root[1] + dy * length * t
        left.append((cx - dy * w, cy + dx * w))
        right.append((cx + dy * w, cy - dx * w))
    ink.polygon(left + right[::-1], fill=fill, outline=edge)
    if vein is not None:
        ink.line((root, (root[0] + dx * length * .85, root[1] + dy * length * .85)), fill=vein, width=1)


# ---------------------------------------------------------------- Lôi Thần
def bolt_path(rng, start, end, depth=6, spread=.32):
    pts = [start, end]
    for d in range(depth):
        nxt = [pts[0]]
        for p, q in zip(pts, pts[1:]):
            length = hypot(q[0] - p[0], q[1] - p[1])
            off = rng.uniform(-1, 1) * length * spread / (1 + d * .35)
            nxt += [((p[0] + q[0]) / 2 + off, (p[1] + q[1]) / 2), q]
        pts = nxt
    return pts


SEAL_ROOT = (TX, SKY_Y + 6)   # tâm thiên ấn: mọi tia sét xuất phát từ đây


def make_bolts(seed):
    rng = random.Random(seed)
    bolts = []
    for k, dx in enumerate((-6, 8, -2)):
        main = bolt_path(rng, (SEAL_ROOT[0] + dx, SEAL_ROOT[1]), (TX + rng.uniform(-8, 8), TY - 6), depth=7)
        branches, twigs = [], []
        for _ in range(7 + k):
            i = rng.randrange(len(main) // 6, len(main) * 9 // 10)
            sx, sy = main[i]
            ex = sx + rng.choice((-1, 1)) * rng.uniform(22, 60)
            ey = min(sy + rng.uniform(35, 95), TY - 4)
            br = bolt_path(rng, (sx, sy), (ex, ey), depth=5, spread=.42)
            branches.append(br)
            for _ in range(2):
                j = rng.randrange(1, len(br) - 1)
                tx, ty = br[j]
                twigs.append(bolt_path(rng, (tx, ty), (tx + rng.uniform(-28, 28), min(ty + rng.uniform(14, 40), TY - 2)), 3, .5))
        bolts.append((main, branches, twigs))
    return bolts


BOLTS = make_bolts(29)
FLICKER = [1, .6, 1, .75, .5, .28, .12]


def ground_plasma(fx, i, strength, color, core):
    """Lôi bộc ở chân mục tiêu: nhiều tia nhỏ dày đặc bắn lên, giữ trong bề ngang hẹp."""
    rng = random.Random(3000 + i)
    body = Image.new("L", VIEWPORT)
    hot = Image.new("L", VIEWPORT)
    bk, hk = ImageDraw.Draw(body), ImageDraw.Draw(hot)
    for _ in range(14):
        x0 = TX + rng.gauss(0, 22)
        top = TY - rng.uniform(30, 105) * (1 - abs(x0 - TX) / 90)
        pts = bolt_path(rng, (x0, TY - 2), (x0 + rng.uniform(-18, 18), top), 4, .45)
        bk.line(pts, fill=230, width=5, joint="curve")
        hk.line(pts, fill=255, width=1)
    for _ in range(18):
        a = rng.uniform(0, tau)
        r = rng.uniform(8, 50)
        p0 = (TX + cos(a) * r, TY - 4 + sin(a) * r * .3)
        hk.line(bolt_path(rng, p0, (p0[0] + rng.uniform(-16, 16), p0[1] - rng.uniform(4, 22)), 2, .6), fill=230, width=1)
    star_ink = ImageDraw.Draw(hot)
    for _ in range(4):
        star(star_ink, TX + rng.gauss(0, 18), TY - rng.uniform(2, 14), rng.uniform(3, 6), 255)
    glow(fx, body.filter(ImageFilter.GaussianBlur(2)), color, mix(color, core, .3), strength,
         blurs=((16, .75), (6, .8)), core_gain=.8)
    glow(fx, hot, color, (255, 255, 255), strength, blurs=((2, .9),), core_gain=1)


def loi_strike(fx, i, cfg):
    imp = cfg["impact"]
    color, core = cfg["color"], cfg["core"]
    # Tụ lôi trong thiên ấn: tia dẫn mọc ra từ tâm trận pháp, dài dần.
    if imp - 9 <= i < imp:
        rng = random.Random(i * 7)
        mask = Image.new("L", VIEWPORT)
        ink = ImageDraw.Draw(mask)
        reach = (i - imp + 9) / 9
        for _ in range(5):
            end = (TX + rng.uniform(-40, 40), SKY_Y + 14 + reach * rng.uniform(40, 120))
            ink.line(bolt_path(rng, SEAL_ROOT, end, 4, .45), fill=210, width=2)
        glow(fx, mask, color, core, .9, blurs=((6, .9),))
        pulse = Image.new("L", VIEWPORT)
        ImageDraw.Draw(pulse).ellipse((TX - 60, SKY_Y - 10, TX + 60, SKY_Y + 22), fill=round(150 * reach))
        glow(fx, pulse.filter(ImageFilter.GaussianBlur(10)), color, core, 1, blurs=((10, .6),), core_gain=.25)
    for n, start in enumerate((imp, imp + 6, imp + 12)):
        age = i - start
        if 0 <= age < len(FLICKER):
            s = FLICKER[age]
            main, branches, twigs = BOLTS[n]
            rng = random.Random(500 + n * 17 + age)
            braid = [(x + rng.uniform(-3, 3) + sin(y * .06 + age) * 3, y) for x, y in main]
            body = Image.new("L", VIEWPORT)
            mid = Image.new("L", VIEWPORT)
            hot = Image.new("L", VIEWPORT)
            bk, mk, hk = ImageDraw.Draw(body), ImageDraw.Draw(mid), ImageDraw.Draw(hot)
            for line, wb, wm, wh in ((main, 22, 9, 3), (braid, 12, 4, 1)):
                bk.line(line, fill=255, width=wb, joint="curve")
                mk.line(line, fill=255, width=wm, joint="curve")
                hk.line(line, fill=255, width=wh, joint="curve")
            for b in branches:
                bk.line(b, fill=230, width=9, joint="curve")
                mk.line(b, fill=235, width=3, joint="curve")
                hk.line(b, fill=255, width=1)
            for t in twigs:
                bk.line(t, fill=170, width=3)
                hk.line(t, fill=220, width=1)
            glow(fx, body.filter(ImageFilter.GaussianBlur(3)), color, mix(color, (110, 50, 220), .35), s,
                 blurs=((30, .85), (12, 1.0)), core_gain=1.0)
            glow(fx, mid, color, mix(color, core, .6), s, blurs=((3, .7),), core_gain=.95)
            glow(fx, hot, color, (255, 255, 255), s, blurs=((1, .6),), core_gain=1)
            # Thiên ấn lóe sáng tại gốc sét.
            seal = Image.new("L", VIEWPORT)
            ImageDraw.Draw(seal).ellipse((TX - 75, SKY_Y - 12, TX + 75, SKY_Y + 24), fill=round(220 * s))
            glow(fx, seal.filter(ImageFilter.GaussianBlur(9)), color, core, s, blurs=((12, .7),), core_gain=.4)
            flash = Image.new("L", VIEWPORT)
            ImageDraw.Draw(flash).ellipse((TX - 70, TY - 26, TX + 70, TY + 16), fill=round(200 * s))
            glow(fx, flash.filter(ImageFilter.GaussianBlur(14)), color, core, .7 * s,
                 blurs=((10, .6),), core_gain=.35)
    if imp <= i < imp + 26:
        ground_plasma(fx, i, 1 - ease(imp + 14, imp + 26, i), color, core)


# ---------------------------------------------------------------- Hỏa Phượng
def phoenix_pos(u):
    # Chui ra từ thiên ấn, lượn vòng về phía tu sĩ rồi bổ thẳng xuống mục tiêu.
    x = TX - 120 * sin(u * pi) * (1 - u) ** .6
    y = SKY_Y + 22 + (TY - SKY_Y - 30) * u ** 1.5
    return x, y


FIRE_RAMP = [(255, 252, 230), (255, 222, 110), (255, 150, 40), (226, 64, 22), (120, 24, 18)]


def fire_color(t):
    t = max(0, min(.999, t)) * (len(FIRE_RAMP) - 1)
    k = int(t)
    return mix(FIRE_RAMP[k], FIRE_RAMP[k + 1], t - k)


def dot(dst, x, y, r, color, alpha):
    if alpha <= 0 or r <= 0:
        return
    layer = Image.new("RGBA", (int(r * 2 + 4), int(r * 2 + 4)))
    ImageDraw.Draw(layer).ellipse((2, 2, 2 + r * 2, 2 + r * 2), fill=color + (round(255 * alpha),))
    dst.alpha_composite(layer, (round(x - r - 2), round(y - r - 2)))


def hoa_strike(fx, i, cfg):
    imp = cfg["impact"]
    start = imp - 14
    if start <= i <= imp:
        u = (i - start) / (imp - start)
        hx, hy = phoenix_pos(u)
        prev = phoenix_pos(max(0, u - .04))
        ang = (hy - prev[1], hx - prev[0])
        heading = atan2(ang[0], ang[1])
        heat = Image.new("L", VIEWPORT)
        ink = ImageDraw.Draw(heat)
        # Đuôi phượng: năm dải lửa hình lá nối nhau, uốn sóng, đầu đuôi cuộn.
        for ribbon in (-2, -1, 0, 1, 2):
            prev_pt = (hx, hy)
            for k in range(1, 16):
                uu = max(0, u - k * .022)
                x, y = phoenix_pos(uu)
                spread = ribbon * (3 + k * 1.6)
                x += spread * cos(heading) + sin(k * .55 + i * .45 + ribbon) * (3 + k * .5)
                y -= spread * sin(heading)
                seg = atan2(y - prev_pt[1], x - prev_pt[0])
                leaf(ink, prev_pt, seg, hypot(x - prev_pt[0], y - prev_pt[1]) + 6,
                     (7 - abs(ribbon)) * (1 - k / 18), round(235 - k * 7 - abs(ribbon) * 15))
                prev_pt = (x, y)
            curl = heading + pi + ribbon * .5 + i * .3
            for c in range(5):
                a = curl + c * .7
                leaf(ink, prev_pt, a, 9 - c, 3, 150)
        # Đôi cánh: ba tầng lông vũ (ngoài đỏ cam, giữa cam vàng, trong vàng trắng), vỗ theo nhịp.
        flap = sin(i * 1.1) * .4
        back = heading + pi
        for side in (-1, 1):
            for layer, (count, length, width, fill) in enumerate(((10, 86, 6.5, 160), (8, 62, 6, 200), (6, 40, 5, 235))):
                for f in range(count):
                    t = f / (count - 1)
                    a = back + side * (.25 + t * 1.45 + flap * t)
                    root = (hx + cos(a) * layer * 4, hy + sin(a) * layer * 4 - 3)
                    tip_len = length * (.72 + .28 * sin(pi * (.2 + .8 * t))) * (1 - .1 * layer)
                    leaf(ink, root, a - side * .06, tip_len, width, fill, vein=min(255, fill + 35), edge=fill - 110)
        # Mào lửa và thân: lưỡi lửa nhỏ hướng ngược chiều bay, mỏ sáng nhất.
        for c in range(7):
            a = back + (c - 3) * .16
            leaf(ink, (hx, hy), a, 30 - abs(c - 3) * 4, 5, 230)
        leaf(ink, (hx - cos(heading) * 10, hy - sin(heading) * 10), heading, 26, 7, 255)
        heat = heat.filter(ImageFilter.GaussianBlur(.7))
        for thr, color in ((20, (170, 30, 14)), (75, (235, 80, 24)), (130, (255, 140, 36)), (180, (255, 205, 80)), (225, (255, 248, 210))):
            layer = Image.new("RGBA", VIEWPORT, color)
            layer.putalpha(heat.point(lambda a, t=thr: 0 if a < t else min(255, (a - t) * 3)))
            fx.alpha_composite(layer)
        halo = Image.new("L", VIEWPORT)
        ImageDraw.Draw(halo).ellipse((hx - 40, hy - 40, hx + 40, hy + 40), fill=150)
        glow(fx, halo.filter(ImageFilter.GaussianBlur(18)), cfg["color"], cfg["core"], .8,
             blurs=((12, .7),), core_gain=.15)
    # Tàn lửa rơi dọc quỹ đạo + cột lửa hạt sau va chạm.
    rng = random.Random(77)
    for p in range(70):
        born = rng.uniform(start, imp)
        u = (born - start) / (imp - start)
        x, y = phoenix_pos(u)
        vx, vy = rng.uniform(-1.5, 1.5), rng.uniform(-1.2, .4)
        life = rng.uniform(8, 18)
        age = i - born
        if 0 <= age < life:
            t = age / life
            dot(fx, x + vx * age, y + vy * age - .05 * age * age, 2.2 * (1 - t) + .6, fire_color(t), 1 - t)
    for p in range(220):
        born = imp + rng.uniform(0, 26)
        life = rng.uniform(10, 22)
        age = i - born
        if 0 <= age < life:
            t = age / life
            x = TX + rng.gauss(0, 16) + sin(age * .4 + p) * 4
            y = TY - 6 - rng.uniform(3, 7) * age - 40 * t * t
            dot(fx, x, y, (12 * (1 - t) + 2) * rng.uniform(.7, 1.2), fire_color(t * 1.1), (1 - t) * .9)
    if imp <= i < imp + 8:
        s = 1 - (i - imp) / 8
        flash = Image.new("L", VIEWPORT)
        ImageDraw.Draw(flash).ellipse((TX - 80, TY - 60, TX + 80, TY + 20), fill=round(230 * s))
        glow(fx, flash.filter(ImageFilter.GaussianBlur(20)), cfg["color"], cfg["core"], s,
             blurs=((14, .7),), core_gain=.3)


# ---------------------------------------------------------------- Huyền Vũ
def thuy_strike(fx, i, cfg):
    imp = cfg["impact"]
    color, core = cfg["color"], cfg["core"]
    # Thủy triền xoáy tụ quanh mục tiêu.
    if imp - 14 <= i < imp + 2:
        u = ease(imp - 14, imp, i)
        mask = Image.new("L", VIEWPORT)
        ink = ImageDraw.Draw(mask)
        for lane in range(3):
            for k in range(26):
                a = lane * tau / 3 + i * .45 - k * .07
                r = 110 * (1 - u) + 18 - k * .6
                x, y = TX + cos(a) * r, TY - 10 + sin(a) * r * .3 - u * k * .8
                w = max(1, 4 - k // 8)
                ink.ellipse((x - w, y - w, x + w, y + w), fill=round(255 * (1 - k / 26)))
        glow(fx, mask, color, core, 1, blurs=((7, .9), (2, .8)))
    # Thủy trụ phun lên tới thiên ấn rồi sụp: khối nhiều lớp, vân chảy, hai dải xoắn, bọt đỉnh và vương miện đáy.
    if imp <= i < imp + 34:
        rise = ease(imp, imp + 7, i)
        fall = ease(imp + 20, imp + 34, i)
        top = TY - 8 - (TY - SKY_Y - 30) * rise * (1 - fall * .9)
        bottom = TY - 2
        base_w = 34 * (1 - fall * .7)

        def outline(scale, phase_shift=0.0):
            left, right = [], []
            for st in range(41):
                y = bottom - (bottom - top) * st / 40
                taper = .55 + .45 * (1 - st / 40) + .3 * (st / 40) ** 4
                wob = sin(y * .07 + i * .8 + phase_shift) * 5 + sin(y * .19 - i * 1.3) * 2.5
                w = base_w * taper * scale
                left.append((TX - w + wob, y))
                right.append((TX + w + wob * .6, y))
            return left + right[::-1]

        body = Image.new("L", VIEWPORT)
        ImageDraw.Draw(body).polygon(outline(1), fill=170)
        inner = Image.new("L", VIEWPORT)
        ImageDraw.Draw(inner).polygon(outline(.58, .6), fill=150)
        core_m = Image.new("L", VIEWPORT)
        ImageDraw.Draw(core_m).polygon(outline(.22, 1.1), fill=200)
        glow(fx, body, (20, 140, 185), (40, 175, 205), 1, blurs=((14, .55), (4, .5)), core_gain=1)
        glow(fx, inner.filter(ImageFilter.GaussianBlur(3)), (70, 215, 230), (90, 225, 238), 1, blurs=(), core_gain=1)
        glow(fx, core_m.filter(ImageFilter.GaussianBlur(4)), core, core, 1, blurs=(), core_gain=.8)
        clip = body.point(lambda a: 255 if a else 0)
        # Vân nước chảy lên: nhiều sợi mảnh theo đường uốn của khối.
        streak = Image.new("L", VIEWPORT)
        sk = ImageDraw.Draw(streak)
        rng = random.Random(9)
        for k in range(16):
            lane = rng.uniform(-.85, .85)
            speed = rng.uniform(9, 16)
            length = rng.uniform(18, 46)
            for seg in range(3):
                y0 = bottom - ((seg * 110 + i * speed + k * 37) % 330)
                if top + 6 < y0 < bottom:
                    pts = [(TX + lane * base_w * .9 + sin((y0 - d) * .07 + i * .8) * 5, y0 - d) for d in range(0, int(length), 4)]
                    if len(pts) > 1:
                        sk.line(pts, fill=rng.randint(150, 255), width=rng.choice((1, 1, 2)))
        glow(fx, ImageChops.multiply(streak, clip), (120, 230, 240), (240, 255, 255), .9, blurs=((2, .6),))
        # Hai dải nước xoắn quanh thân (mặt trước sáng, mặt sau mờ).
        for back_pass in (True, False):
            rib = Image.new("L", VIEWPORT)
            rk = ImageDraw.Draw(rib)
            for strand in range(2):
                pts = []
                for st in range(60):
                    t = st / 59
                    y = bottom - 10 - (bottom - top - 20) * t
                    ph = strand * pi + t * 3.2 * pi - i * .35
                    front = cos(ph) > 0
                    x = TX + sin(ph) * base_w * (1.55 - .35 * t) + sin(y * .07 + i * .8) * 5
                    if front != back_pass:
                        pts.append((x, y, cos(ph)))
                    elif len(pts) > 1:
                        rk.line([(a, b) for a, b, _ in pts], fill=90 if back_pass else 180, width=2, joint="curve")
                        pts = []
                    else:
                        pts = []
                if len(pts) > 1:
                    rk.line([(a, b) for a, b, _ in pts], fill=90 if back_pass else 180, width=2, joint="curve")
            glow(fx, rib, (60, 200, 225), (170, 240, 250), (1 - fall) * (.5 if back_pass else .9),
                 blurs=((5, .9),), core_gain=.75)
        # Bọt đỉnh: lưỡi nước cong ra ngoài + bọt trắng.
        foam = Image.new("L", VIEWPORT)
        fk = ImageDraw.Draw(foam)
        rng = random.Random(i)
        for k in range(9):
            side = -1 if k % 2 else 1
            a = -pi / 2 + side * rng.uniform(.25, 1.1)
            leaf(fk, (TX + rng.gauss(0, base_w * .5), top + 8), a, rng.uniform(16, 32) * (1 - fall * .5), 4, 210)
        for _ in range(18):
            x = TX + rng.gauss(0, base_w * .7)
            y = top + rng.uniform(-6, 16)
            r = rng.uniform(2, 6)
            fk.ellipse((x - r, y - r, x + r, y + r), fill=245)
        # Vương miện bọt nước ở đáy.
        for k in range(12):
            t = k / 11
            a = -pi / 2 + (t - .5) * 2.6
            h = (22 + 14 * sin(pi * t)) * ease(imp, imp + 5, i) * (1 - fall)
            leaf(fk, (TX + (t - .5) * base_w * 3.2, bottom), a, h, 3.5, 225)
        glow(fx, foam.filter(ImageFilter.GaussianBlur(.6)), (110, 225, 240), (245, 255, 255), 1 - fall * .7,
             blurs=((6, .6),), core_gain=1)
        # Bong bóng nhỏ nổi lên trong thân.
        bub = Image.new("L", VIEWPORT)
        bk = ImageDraw.Draw(bub)
        rng = random.Random(21)
        for k in range(22):
            y = bottom - ((i * rng.uniform(5, 9) + k * 29) % max(1, bottom - top))
            x = TX + rng.uniform(-.8, .8) * base_w + sin(y * .07 + i * .8) * 5
            r = rng.uniform(1.2, 2.8)
            bk.ellipse((x - r, y - r, x + r, y + r), outline=230)
        glow(fx, ImageChops.multiply(bub, clip), (150, 235, 245), (255, 255, 255), 1, blurs=(), core_gain=.9)
    # Giọt nước văng theo trọng lực + sóng gợn mặt đất.
    rng = random.Random(55)
    for p in range(110):
        born = imp + rng.uniform(4, 24)
        life = rng.uniform(12, 22)
        age = i - born
        if 0 <= age < life:
            x0 = TX + rng.gauss(0, 12)
            y0 = TY - 40 - (TY - SKY_Y - 60) * rng.uniform(.3, 1)
            vx = rng.uniform(-4.2, 4.2)
            vy = rng.uniform(-3, .5)
            x, y = x0 + vx * age, y0 + vy * age + .55 * age * age
            if y < TY:
                t = age / life
                dot(fx, x, y, 2.4 * (1 - t) + .8, mix(core, color, t), 1 - t * .8)
    for k, start in enumerate((imp, imp + 5, imp + 10)):
        age = i - start
        if 0 <= age < 22:
            t = age / 22
            r = 30 + 150 * t
            ring = Image.new("L", VIEWPORT)
            ImageDraw.Draw(ring).ellipse((TX - r, TY - 4 - r * .22, TX + r, TY - 4 + r * .22),
                                         outline=round(220 * (1 - t)), width=2)
            glow(fx, ring, color, core, 1, blurs=((4, .8),))


# ---------------------------------------------------------------- Nguyệt Kiếm
def crescent_mask(cx, cy, r, phase, angle=0.0, size=VIEWPORT):
    big = Image.new("L", size)
    ImageDraw.Draw(big).ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    cut = Image.new("L", size)
    ox, oy = cos(angle) * r * phase, sin(angle) * r * phase
    ImageDraw.Draw(cut).ellipse((cx - r + ox, cy - r + oy, cx + r + ox, cy + r + oy), fill=255)
    return ImageChops.subtract(big, cut)


def arc_points(center, rx, ry, a0, a1, rot, steps, shrink=0.0):
    cx, cy = center
    pts = []
    for st in range(steps + 1):
        a = a0 + (a1 - a0) * st / max(1, steps)
        x, y = cos(a) * (rx - shrink), sin(a) * (ry - shrink * ry / rx)
        pts.append((cx + x * cos(rot) - y * sin(rot), cy + x * sin(rot) + y * cos(rot)))
    return pts


def slash_layers(center, rx, ry, a0, a1, head, width, rot, seed):
    """Lưỡi nguyệt trảm chi tiết: thân trăng khuyết, các sợi lụa sáng, vệt phụ trong/ngoài, sao lấp lánh."""
    end = a0 + (a1 - a0) * head
    steps = 56
    body = Image.new("L", VIEWPORT)
    fiber = Image.new("L", VIEWPORT)
    sparkle = Image.new("L", VIEWPORT)
    bk, fk, sk = ImageDraw.Draw(body), ImageDraw.Draw(fiber), ImageDraw.Draw(sparkle)
    outer, inner = [], []
    for st in range(steps + 1):
        k = st / steps
        w = width * sin(pi * k) ** .8 * (.35 + .65 * k)
        a = a0 + (end - a0) * k
        outer += arc_points(center, rx, ry, a, a, rot, 0)
        inner += arc_points(center, rx, ry, a, a, rot, 0, w)
    bk.polygon(outer + inner[::-1], fill=185)
    rng = random.Random(seed)
    # Sợi lụa: nhiều cung mảnh lệch vào trong, bắt đầu/kết thúc so le.
    for f in range(7):
        depth = rng.uniform(.05, .85) * width
        s0 = rng.uniform(0, .35)
        pts = arc_points(center, rx, ry, a0 + (end - a0) * s0, end - (end - a0) * rng.uniform(0, .08), rot, 40,
                         depth * (.4 + .6 * sin(pi * .5)))
        fk.line(pts, fill=rng.randint(170, 255), width=1 if f % 3 else 2, joint="curve")
    fk.line(arc_points(center, rx, ry, a0 + (end - a0) * .1, end, rot, 48, width * .12), fill=255,
            width=max(2, round(width * .14)), joint="curve")
    # Vệt phụ: một cung ngoài và một cung trong mảnh, trễ hơn lưỡi chính.
    lag = a0 + (end - a0) * .82
    fk.line(arc_points(center, rx * 1.07, ry * 1.18, a0 + (lag - a0) * .25, lag, rot, 36), fill=140, width=1)
    fk.line(arc_points(center, rx * .8, ry * .7, a0 + (lag - a0) * .35, lag, rot, 36), fill=110, width=1)
    # Sao lấp lánh dọc lưỡi.
    for _ in range(7):
        k = rng.uniform(.15, 1)
        a = a0 + (end - a0) * k
        x, y = arc_points(center, rx, ry, a, a, rot, 0, rng.uniform(0, width * .9))[0]
        star(sk, x, y, rng.uniform(3, 7), 255)
    return body, fiber, sparkle


def nguyet_strike(fx, i, cfg):
    imp = cfg["impact"]
    color, core = cfg["color"], cfg["core"]
    # Trăng khuyết kết trong thiên ấn.
    if SEAL_START + 4 <= i < imp + 22:
        grow = ease(SEAL_START + 4, imp - 6, i)
        fade = 1 - ease(imp + 10, imp + 22, i)
        moon = crescent_mask(TX, SKY_Y + 30, 30 * grow + 2, .42, -.5)
        glow(fx, moon, color, core, fade * grow, blurs=((16, .8), (5, .8)))
    slashes = [  # (khung bắt đầu, rx, ry, a0, a1, xoay, rộng)
        (imp, 125, 40, pi * 1.05, pi * 1.95, .55, 24),
        (imp + 4, 125, 40, pi * 1.05, pi * 1.95, -.55, 24),
        (imp + 8, 145, 36, pi * .95, pi * 2.05, 0.0, 26),
    ]
    for n, (start, rx, ry, a0, a1, rot, width) in enumerate(slashes):
        age = i - start
        if 0 <= age < 9:
            head = min(1, (age + 1) / 3)
            fade = 1 if age < 3 else 1 - (age - 3) / 6
            body, fiber, sparkle = slash_layers((TX, TY - 46), rx, ry, a0, a1, head, width, rot, 70 + n)
            glow(fx, body, color, mix(color, core, .35), fade, blurs=((14, .75), (5, .8)), core_gain=.9)
            glow(fx, fiber, color, core, fade, blurs=((2, .7),), core_gain=1)
            glow(fx, sparkle, color, (255, 255, 255), fade * (.6 + .4 * sin(age * 2.1 + n)), blurs=((3, .8),))
    big = imp + 14
    age = i - big
    if 0 <= age < 14:
        head = min(1, (age + 1) / 4)
        fade = 1 if age < 4 else 1 - (age - 4) / 10
        body, fiber, sparkle = slash_layers((TX - 40, (SKY_Y + TY) / 2), 95, (TY - SKY_Y) / 2 + 6,
                                            -pi * .5, pi * .5, head, 34, 0.0, 99)
        glow(fx, body, color, mix(color, core, .35), fade, blurs=((24, .7), (8, .85)), core_gain=.9)
        glow(fx, fiber, color, core, fade, blurs=((2, .7),), core_gain=1)
        glow(fx, sparkle, color, (255, 255, 255), fade, blurs=((3, .8),))
        if age < 6:
            flash = Image.new("L", VIEWPORT)
            ImageDraw.Draw(flash).ellipse((TX - 90, TY - 40, TX + 90, TY + 14), fill=200)
            glow(fx, flash.filter(ImageFilter.GaussianBlur(18)), color, core, 1 - age / 6,
                 blurs=((10, .6),), core_gain=.3)
    # Sương nguyệt quang lấp lánh.
    rng = random.Random(91)
    for p in range(60):
        born = imp + rng.uniform(0, 30)
        life = rng.uniform(10, 20)
        age = i - born
        if 0 <= age < life:
            t = age / life
            x = TX + rng.gauss(0, 60) + rng.uniform(-.6, .6) * age
            y = TY - rng.uniform(10, 150) - .6 * age
            dot(fx, x, y, 1.6, core, (1 - t) * (.5 + .5 * sin(age * 1.7 + p)))


STRIKES = {"loi-than": loi_strike, "hoa-phuong": hoa_strike, "huyen-vu": thuy_strike, "nguyet-kiem": nguyet_strike}


# ---------------------------------------------------------------- build
def portal_cutoff():
    mask = Image.new("L", VIEWPORT, 255)
    ink = ImageDraw.Draw(mask)
    ink.rectangle((0, 0, VIEWPORT[0], SKY_Y), fill=0)
    for o in range(1, 12):
        ink.line((0, SKY_Y + o, VIEWPORT[0], SKY_Y + o), fill=round(o * 255 / 12))
    return mask


PORTAL_CUTOFF = portal_cutoff()


def load_explosion(element):
    with Image.open(ROOT / "explosions" / f"{element}-ngu-chuong-chi-no-v3.webp") as clip:
        images, delays = [], []
        for k in range(clip.n_frames):
            clip.seek(k)
            clip.load()
            images.append(clip.convert("RGBA"))
            delays.append(clip.info["duration"])
    return images, list(accumulate(delays))


def build(vid, bg, holy):
    cfg = VARIANTS[vid]
    imp = cfg["impact"]
    out = ROOT / vid
    out.mkdir(exist_ok=True)
    meta_src = json.loads((PHAP_TUONG / vid / "demo.json").read_text(encoding="utf-8"))
    guardian = frames(PHAP_TUONG / vid / "atlas.png", meta_src["cellSize"], meta_src["frameCount"],
                      meta_src["columns"])
    rt = holy["runtime"]
    seals = [tint(s, cfg["color"]) for s in
             frames(SOURCE / "holy-light-formation.png", (rt["frameWidth"], rt["frameHeight"]),
                    holy["frameCount"], holy["columns"])]
    rows = (TOTAL + COLUMNS - 1) // COLUMNS
    timing = [round((k + 1) * 1000 / FPS) - round(k * 1000 / FPS) for k in range(TOTAL)]
    atlas = Image.new("RGBA", (COLUMNS * CELL[0], rows * CELL[1]))
    bursts, ends = load_explosion(cfg["element"])
    previews = []
    strike = STRIKES[vid]
    for i in range(TOTAL):
        fx = Image.new("RGBA", VIEWPORT)
        if i < len(guardian):
            place(fx, guardian[i], meta_src["anchor"], CASTER, EFFECT_SCALE * 1.5)
        seal_index = i - imp + holy["impactFrame"]
        seal = None
        if SEAL_START <= i and seal_index < len(seals):
            src = (round(holy["impactFrame"] * (i - SEAL_START) / (imp - SEAL_START)) if i < imp else seal_index)
            seal = seals[src]
            place(fx, seal.crop((0, 97, seal.width, seal.height)), (rt["anchor"][0], 52), TARGET,
                  1.25 if vid == "huyen-vu" else 1.10)
        strike_layer = Image.new("RGBA", VIEWPORT)
        strike(strike_layer, i, cfg)
        if cfg["sky_beam"]:
            # Đòn đánh từ trời không lộ phía trên thiên ấn.
            strike_layer.putalpha(ImageChops.multiply(strike_layer.getchannel("A"), PORTAL_CUTOFF))
        fx.alpha_composite(strike_layer)
        if seal is not None and cfg["sky_beam"]:
            place(fx, seal.crop((0, 0, seal.width, 65)), (rt["anchor"][0], 25), (TX, SKY_Y), 1.46)
        atlas.paste(fx.resize(CELL, RESAMPLE), (i % COLUMNS * CELL[0], i // COLUMNS * CELL[1]))
        shot = Image.alpha_composite(bg, fx)
        if i >= imp:
            blast = bisect_right(ends, (i - imp + .5) * 1000 / FPS)
            if blast < len(bursts):
                place(shot, bursts[blast], EXPLOSION_ANCHOR, TARGET, EXPLOSION_SCALE)
        shot = shot.convert("RGB")
        ImageDraw.Draw(shot).point((519 + i % 39, 152 - i // 3), fill=(72, 92, 106))
        previews.append(shot)
    atlas.save(out / "atlas.png")
    previews[cfg["poster"]].save(out / "poster.png")
    preview = out / "preview.webp"
    previews[0].save(preview, save_all=True, append_images=previews[1:], duration=timing, loop=0,
                     quality=88, method=4)
    with Image.open(preview) as clip:
        assert clip.n_frames == TOTAL and clip.size == VIEWPORT
        total = 0
        for k in range(clip.n_frames):
            clip.seek(k)
            clip.load()
            total += clip.info["duration"]
        assert total == 3500, total
    with Image.open(out / "atlas.png") as sheet:
        assert sheet.mode == "RGBA" and sheet.size == (4000, 3520)
        assert sheet.crop((0, 0, 400, 320)).getbbox() is None, "khung 0 phải trong suốt"
        assert sheet.crop((1600, 3200, 2000, 3520)).getbbox() is None, "khung cuối phải trong suốt"
    meta = {
        "id": vid, "name": cfg["name"], "version": 4, "element": cfg["element"],
        "fps": FPS, "frameCount": TOTAL, "durationMs": sum(timing),
        "impactFrame": imp, "impactMs": round(imp * 1000 / FPS),
        "atlas": "atlas.png", "atlasIncludesExplosion": False,
        "cellSize": list(CELL), "columns": COLUMNS, "rows": rows, "loop": False,
        "anchor": [108, 257.3333], "targetAnchor": [280, 257.3333],
        "guardian": f"phap-tuong-ngu-tuong-demo-v3/{vid}/atlas.png",
        "targetFormation": {"source": "Sprite Fusion Holy Light formation (tinted)",
                            "startFrame": SEAL_START, "skyCenter": [TX, SKY_Y] if cfg["sky_beam"] else None},
        "explosionOverlay": {"file": f"explosions/{cfg['element']}-ngu-chuong-chi-no-v3.webp",
                             "sourceAnchor": list(EXPLOSION_ANCHOR), "previewScale": EXPLOSION_SCALE,
                             "durationMs": ends[-1], "startFrame": imp, "loop": False},
        "previewLayout": {"size": list(VIEWPORT), "caster": list(CASTER), "target": list(TARGET)},
        "description": cfg["desc"],
    }
    (out / "demo.json").write_text(json.dumps(meta, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"{vid}: {TOTAL} khung, 3500 ms, va chạm khung {imp}")


if __name__ == "__main__":
    holy = json.loads((SOURCE / "holy-light.json").read_text(encoding="utf-8"))
    bg = stage()
    for vid in (sys.argv[1:] or VARIANTS):
        build(vid, bg, holy)
