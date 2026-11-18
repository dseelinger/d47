#!/usr/bin/env python3
"""Regenerate the splash card images.

Not part of the build. The output is committed under `assets/splash/`:

    python tools/gen-splash.py

Requires Pillow. Writes splash-100.png, splash-150.png and splash-200.png (480x300, 720x450, 960x600).
Each is rendered from the sources at its own scale. The image holds everything on the card except the
version line, which is drawn at run time from top 220, 16px tall at 100%.
"""

import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
ICON = os.path.join(ROOT, "assets", "directive-47.ico")
FONT = os.path.join(ROOT, "assets", "fonts", "Saira-SemiBold.ttf")
OUT = os.path.join(ROOT, "assets", "splash")

WIDTH, HEIGHT = 480, 300
GROUND = (0x07, 0x06, 0x06)
BORDER = (0x83, 0x42, 0x1A)
NAME_COLOUR = (0xED, 0xE9, 0xE3)
GLOW_COLOUR = (0xFF, 0x7A, 0x1A)
GLOW_OPACITY = 0.55
GLOW_BLUR = 12
MARK_SIZE, MARK_TOP = 96, 64
NAME, NAME_SIZE, NAME_TRACKING = "DIRECTIVE 47", 22, 0.12
LINE_TOP, LINE_HEIGHT = 184, 28
SCANLINE_PERIOD = 3
SCANLINE_OPACITY = 0.30 * 0.50

SCALES = {100: 1.0, 150: 1.5, 200: 2.0}


def mark(scale):
    source = Image.open(ICON)
    source.size = (256, 256)
    source.load()
    size = round(MARK_SIZE * scale)
    return source.convert("RGBA").resize((size, size), Image.LANCZOS)


def name_layer(scale, colour):
    """The name as an RGBA layer the size of the card, one glyph at a time with tracking."""
    font = ImageFont.truetype(FONT, round(NAME_SIZE * scale))
    tracking = NAME_TRACKING * NAME_SIZE * scale
    advances = [font.getlength(c) for c in NAME]
    total = sum(advances) + tracking * (len(NAME) - 1)
    ascent, descent = font.getmetrics()
    x = (round(WIDTH * scale) - total) / 2
    y = round(LINE_TOP * scale) + (round(LINE_HEIGHT * scale) - (ascent + descent)) / 2
    layer = Image.new("RGBA", (round(WIDTH * scale), round(HEIGHT * scale)), colour + (0,))
    draw = ImageDraw.Draw(layer)
    for char, advance in zip(NAME, advances):
        draw.text((round(x), round(y)), char, font=font, fill=colour + (255,))
        x += advance + tracking
    return layer


def scanlines(width, height, scale):
    layer = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    thickness = max(1, round(scale))
    alpha = round(255 * SCANLINE_OPACITY)
    k = 0
    while round(k * SCANLINE_PERIOD * scale) < height:
        top = round(k * SCANLINE_PERIOD * scale)
        draw.rectangle([0, top, width - 1, top + thickness - 1], fill=(0, 0, 0, alpha))
        k += 1
    return layer


def render(scale):
    width, height = round(WIDTH * scale), round(HEIGHT * scale)
    card = Image.new("RGBA", (width, height), GROUND + (255,))

    edge = max(1, round(scale))
    ImageDraw.Draw(card).rectangle([0, 0, width - 1, height - 1], outline=BORDER + (255,), width=edge)

    glow = name_layer(scale, GLOW_COLOUR).filter(ImageFilter.GaussianBlur(GLOW_BLUR * scale))
    glow.putalpha(glow.getchannel("A").point(lambda a: round(a * GLOW_OPACITY)))
    card = Image.alpha_composite(card, glow)
    card = Image.alpha_composite(card, name_layer(scale, NAME_COLOUR))

    icon = mark(scale)
    card.alpha_composite(icon, ((width - icon.width) // 2, round(MARK_TOP * scale)))

    card = Image.alpha_composite(card, scanlines(width, height, scale))
    return card.convert("RGB")


def main():
    os.makedirs(OUT, exist_ok=True)
    for percent, scale in SCALES.items():
        image = render(scale)
        image.save(os.path.join(OUT, f"splash-{percent}.png"), format="PNG")
        print(f"wrote assets/splash/splash-{percent}.png ({image.width}x{image.height})")


if __name__ == "__main__":
    main()
