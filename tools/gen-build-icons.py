#!/usr/bin/env python3
"""Regenerate the icons that mark a test-drive build and a local-release build.

Not part of the build. The output is committed under `assets/`:

    python tools/gen-build-icons.py

Requires Pillow. Each icon is `assets/directive-47.ico` with its digits recoloured and its frame
replaced by a thicker band of diagonal hazard stripes, frame by frame, so every size keeps the
original's hand-hinted digits. Debug builds take the test-drive icon (`D47.App.csproj`); the
local-release skill's targets file gives its build the local-release icon.
"""

import os

from PIL import Image, ImageDraw

ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets")
SOURCE = os.path.join(ASSETS, "directive-47.ico")

ICONS = {
    "directive-47-test-drive.ico": (0x29, 0xB6, 0xF6),
    "directive-47-local-release.ico": (0x4C, 0xD9, 0x64),
}

ORANGE_RED = 255
DARK = (18, 24, 31)
STRIPE_DARK = (11, 11, 11)
SUPERSAMPLE = 8


def band_width(size):
    """Frame thickness in pixels: about 11% of the side, never under 2."""
    return max(2, round(size * 0.11))


def stripe_period(size):
    """One light and one dark stripe, in pixels: 40 at 256, never under 6."""
    return max(6, round(size * 40 / 256))


def coverage(size, draw_shape):
    """A 0-255 mask drawn at SUPERSAMPLE times the size and reduced, for anti-aliased edges."""
    big = Image.new("L", (size * SUPERSAMPLE,) * 2, 0)
    draw_shape(ImageDraw.Draw(big), size * SUPERSAMPLE)
    return big.resize((size, size), Image.LANCZOS)


def stripes(size, colour):
    big_size = size * SUPERSAMPLE
    period = stripe_period(size) * SUPERSAMPLE
    big = Image.new("RGB", (big_size, big_size), STRIPE_DARK)
    draw = ImageDraw.Draw(big)
    # 45-degree bands: a pixel is light where (x + y) mod period falls in the first half.
    for start in range(-big_size, 2 * big_size, period):
        draw.polygon(
            [(start, 0), (start + period // 2, 0), (start + period // 2 - big_size, big_size), (start - big_size, big_size)],
            fill=colour,
        )
    return big.resize((size, size), Image.LANCZOS)


def recolour(frame, colour):
    size = frame.width
    band = band_width(size)
    inner = coverage(
        size,
        lambda d, s: d.rounded_rectangle(
            [band * SUPERSAMPLE, band * SUPERSAMPLE, s - band * SUPERSAMPLE - 1, s - band * SUPERSAMPLE - 1],
            radius=max(1, round(s * 30 / 256)),
            fill=255,
        ),
    )
    stripe = stripes(size, colour)

    out = Image.new("RGBA", (size, size))
    src = frame.load()
    ins = inner.load()
    st = stripe.load()
    dst = out.load()
    for y in range(size):
        for x in range(size):
            r, _, _, a = src[x, y]
            # The source is orange over dark; the red channel says how much orange.
            t = min(1.0, max(0.0, (r - DARK[0]) / (ORANGE_RED - DARK[0])))
            content = [t * colour[i] + (1 - t) * DARK[i] for i in range(3)]
            k = ins[x, y] / 255
            rgb = tuple(round(k * content[i] + (1 - k) * st[x, y][i]) for i in range(3))
            dst[x, y] = rgb + (a,)
    return out


def main():
    source = Image.open(SOURCE)
    sizes = sorted(source.info["sizes"])
    frames = {}
    for size in sizes:
        source.size = size
        source.load()
        frames[size] = source.convert("RGBA")

    for name, colour in ICONS.items():
        made = [recolour(frames[size], colour) for size in sizes]
        largest = made[-1]
        largest.save(
            os.path.join(ASSETS, name),
            format="ICO",
            sizes=sizes,
            append_images=made[:-1],
            bitmap_format="bmp",
        )
        print(f"wrote assets/{name} ({', '.join(str(s[0]) for s in sizes)})")


if __name__ == "__main__":
    main()
