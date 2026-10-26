"""Assemble actual browser captures into a review clip; no second VFX renderer."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
source = ROOT / "build-check/four-elements/cast-directions"
cases = [("thach_dan", "THẠCH ĐẠN · TRÁI → PHẢI"), ("son_nhac", "SƠN NHẠC · TRÊN ↓ DƯỚI"),
         ("dong_dia", "ĐÔNG ĐỊA · DƯỚI ↑ TRÊN"), ("tho_giap", "THỔ GIÁP · TÂM QUÁI")]
font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 15)
frames = []
for frame in range(24):
    canvas = Image.new("RGB", (840, 480), "#101a25")
    draw = ImageDraw.Draw(canvas)
    for i, (key, label) in enumerate(cases):
        tile = Image.open(source / f"{key}-{frame:02}.png").convert("RGBA")
        tile.thumbnail((420, 210), Image.Resampling.LANCZOS)
        x, y = i % 2 * 420, i // 2 * 240
        draw.text((x + 12, y + 8), label, fill="#d9e5eb", font=font)
        canvas.paste(tile, (x, y + 30), tile)
    frames.append(canvas)
output = ROOT / "wwwroot/fx-preview/four-directions-demo.webp"
frames[0].save(output, save_all=True, append_images=frames[1:], duration=100, loop=0, lossless=True, method=4)
print("Exported four-direction runtime demo: " + str(output))
