#!/usr/bin/env python3
"""Regenerate the splash card images.

Not part of the build. The output is committed under `assets/splash/`:

    python tools/gen-splash.py

Requires Pillow. Writes splash-100.png, splash-150.png and splash-200.png (480x360, 720x540, 960x720).
Each is rendered from the sources at its own scale. The image holds everything on the card except the
version line, which is drawn at run time from top 278, 16px tall at 100%.

Boot bar track, at scale s: edge = max(1, round(s)), the border width. It runs x from edge to
width - edge - 1 and y from height - edge - round(4 * s) to height - edge - 1, under the scanlines.
The bar's fill is drawn at run time with the same arithmetic.
"""

import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
ICON = os.path.join(ROOT, "assets", "directive-47.ico")
FONT = os.path.join(ROOT, "assets", "fonts", "Saira-SemiBold.ttf")
SUBTITLE_FONT = os.path.join(ROOT, "assets", "fonts", "Saira-Medium.ttf")
DESCRIPTION_FONT = os.path.join(ROOT, "assets", "fonts", "Sintony-Regular.ttf")
OUT = os.path.join(ROOT, "assets", "splash")

WIDTH, HEIGHT = 480, 360
GROUND = (0x07, 0x06, 0x06)
BORDER = (0x83, 0x42, 0x1A)
NAME_COLOUR = (0xED, 0xE9, 0xE3)
GLOW_COLOUR = (0xFF, 0x7A, 0x1A)
GLOW_OPACITY = 0.55
GLOW_BLUR = 12
MARK_SIZE, MARK_TOP = 96, 64
NAME, NAME_SIZE, NAME_TRACKING = "DIRECTIVE 47", 22, 0.12
LINE_TOP, LINE_HEIGHT = 184, 28
SUBTITLE, SUBTITLE_SIZE, SUBTITLE_TRACKING = "OPTIMIZE INFERIOR SYSTEMS", 13, 0.08
SUBTITLE_TOP, SUBTITLE_COLOUR = 218, (0xED, 0xE9, 0xE3)
DESCRIPTION, DESCRIPTION_SIZE = "Elite Dangerous companion app", 13
DESCRIPTION_TOP, DESCRIPTION_COLOUR = 244, (0xA0, 0x9B, 0x94)
TEXT_LINE = 18
TRACK_COLOUR = (0x41, 0x23, 0x12)
TRACK_HEIGHT = 4
SCANLINE_PERIOD = 3
SCANLINE_OPACITY = 0.30 * 0.50

SCALES = {100: 1.0, 150: 1.5, 200: 2.0}


def mark(scale):
    source = Image.open(ICON)
    source.size = (256, 256)
    source.load()
    size = round(MARK_SIZE * scale)
    return source.convert("RGBA").resize((size, size), Image.LANCZOS)


def text_layer(scale, text, font_path, size, tracking_em, top, line, colour):
    """Text as an RGBA layer the size of the card, centred, one glyph at a time with tracking."""
    font = ImageFont.truetype(font_path, round(size * scale))
    tracking = tracking_em * size * scale
    advances = [font.getlength(c) for c in text]
    total = sum(advances) + tracking * (len(text) - 1)
    ascent, descent = font.getmetrics()
    x = (round(WIDTH * scale) - total) / 2
    y = round(top * scale) + (round(line * scale) - (ascent + descent)) / 2
    layer = Image.new("RGBA", (round(WIDTH * scale), round(HEIGHT * scale)), colour + (0,))
    draw = ImageDraw.Draw(layer)
    for char, advance in zip(text, advances):
        draw.text((round(x), round(y)), char, font=font, fill=colour + (255,))
        x += advance + tracking
    return layer


def name_layer(scale, colour):
    return text_layer(scale, NAME, FONT, NAME_SIZE, NAME_TRACKING, LINE_TOP, LINE_HEIGHT, colour)


def description_layer(scale):
    """The description as an RGBA layer the size of the card, centred, set as one run."""
    font = ImageFont.truetype(DESCRIPTION_FONT, round(DESCRIPTION_SIZE * scale))
    ascent, descent = font.getmetrics()
    width = round(WIDTH * scale)
    x = (width - font.getlength(DESCRIPTION)) / 2
    y = round(DESCRIPTION_TOP * scale) + (round(TEXT_LINE * scale) - (ascent + descent)) / 2
    layer = Image.new("RGBA", (width, round(HEIGHT * scale)), DESCRIPTION_COLOUR + (0,))
    ImageDraw.Draw(layer).text((round(x), round(y)), DESCRIPTION, font=font, fill=DESCRIPTION_COLOUR + (255,))
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

    ImageDraw.Draw(card).rectangle(
        [edge, height - edge - round(TRACK_HEIGHT * scale), width - edge - 1, height - edge - 1],
        fill=TRACK_COLOUR + (255,),
    )

    glow = name_layer(scale, GLOW_COLOUR).filter(ImageFilter.GaussianBlur(GLOW_BLUR * scale))
    glow.putalpha(glow.getchannel("A").point(lambda a: round(a * GLOW_OPACITY)))
    card = Image.alpha_composite(card, glow)
    card = Image.alpha_composite(card, name_layer(scale, NAME_COLOUR))
    subtitle = text_layer(
        scale, SUBTITLE, SUBTITLE_FONT, SUBTITLE_SIZE, SUBTITLE_TRACKING, SUBTITLE_TOP, TEXT_LINE, SUBTITLE_COLOUR)
    card = Image.alpha_composite(card, subtitle)
    card = Image.alpha_composite(card, description_layer(scale))

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
