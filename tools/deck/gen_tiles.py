"""Draws the key images for the Directive 47 Development Stream Deck profile.

Each key is a 144x144 PNG: a near-black tile, a bright accent bar naming its category, a large
glyph, and the label baked in. The label is part of the image so the manifest sets ShowTitle
false — Stream Deck's own title rendering clips at this size and cannot be tracked or spaced.

Run it after changing a label, a colour or a glyph, then restart Stream Deck:

    python tools/deck/gen_tiles.py

Glyphs are drawn at 4x and downsampled, which is what makes the thin strokes clean.
"""

import math
import os

from PIL import Image, ImageDraw, ImageFont

SIZE = 144
SS = 4                      # supersample factor
S = SIZE * SS

BG = (14, 17, 21)
LABEL = (233, 238, 245)
DIM = (70, 79, 92)

# Category accents. Bright enough to read at arm's length on a lit desk.
VIOLET = (183, 148, 255)    # opens a Claude session
CYAN = (79, 209, 245)       # types into the focused terminal
GREEN = (91, 228, 155)      # runs a script
BLUE = (77, 140, 255)       # runs the app
AMBER = (255, 176, 32)      # release

FONT_PATH = os.path.join(os.environ.get('WINDIR', r'C:\Windows'), 'Fonts', 'arialbd.ttf')


# ---------------------------------------------------------------- glyph drawing
# Every glyph is drawn into a box centred on (cx, cy) with half-extent r, in 4x space.

def _ring(d, cx, cy, r, col, w):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=col, width=w)


def _dot(d, cx, cy, r, col):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col)


def _bar(d, cx, cy, half_w, half_h, col):
    d.rounded_rectangle([cx - half_w, cy - half_h, cx + half_w, cy + half_h],
                        radius=half_h, fill=col)


def g_triage(d, cx, cy, r, col):
    """Three bars, longest first: a ranked list."""
    gap = r * 0.62
    for i, frac in enumerate((1.0, 0.70, 0.42)):
        _bar(d, cx - r + r * frac, cy - gap + i * gap, r * frac, r * 0.15, col)


def g_hub(d, cx, cy, r, col):
    """A hub with three satellites: work being routed."""
    w = max(2, int(r * 0.13))
    pts = [(cx + r * 0.82 * math.cos(math.radians(a)),
            cy + r * 0.82 * math.sin(math.radians(a))) for a in (-90, 30, 150)]
    for x, y in pts:
        d.line([cx, cy, x, y], fill=col, width=w)
    for x, y in pts:
        _dot(d, x, y, r * 0.22, col)
    _dot(d, cx, cy, r * 0.30, col)


def g_compass(d, cx, cy, r, col):
    """Drafting compass: deciding the shape before building it."""
    w = max(2, int(r * 0.15))
    top = cy - r * 0.82
    d.line([cx, top, cx - r * 0.62, cy + r * 0.78], fill=col, width=w)
    d.line([cx, top, cx + r * 0.62, cy + r * 0.78], fill=col, width=w)
    d.arc([cx - r * 0.46, cy + r * 0.06, cx + r * 0.46, cy + r * 0.72],
          start=20, end=160, fill=col, width=max(2, int(w * 0.7)))
    _dot(d, cx, top, r * 0.20, col)


def g_issue(d, cx, cy, r, col):
    """The GitHub open-issue mark."""
    _ring(d, cx, cy, r * 0.86, col, max(2, int(r * 0.17)))
    _dot(d, cx, cy, r * 0.26, col)


def g_handoff(d, cx, cy, r, col):
    """A screen with an arrow entering it: the session moves to the desktop app."""
    w = max(2, int(r * 0.13))
    d.rounded_rectangle([cx - r * 0.92, cy - r * 0.78, cx + r * 0.92, cy + r * 0.38],
                        radius=r * 0.16, outline=col, width=w)
    d.line([cx - r * 0.30, cy + r * 0.72, cx + r * 0.30, cy + r * 0.72], fill=col, width=w)
    d.line([cx, cy + r * 0.38, cx, cy + r * 0.72], fill=col, width=w)
    # arrow into the middle of the screen
    d.line([cx - r * 0.46, cy - r * 0.20, cx + r * 0.30, cy - r * 0.20], fill=col, width=w)
    d.polygon([(cx + r * 0.52, cy - r * 0.20), (cx + r * 0.18, cy - r * 0.44),
               (cx + r * 0.18, cy + r * 0.04)], fill=col)


def g_magnifier(d, cx, cy, r, col):
    """Review."""
    w = max(2, int(r * 0.15))
    ox, oy = cx - r * 0.14, cy - r * 0.18
    _ring(d, ox, oy, r * 0.62, col, w)
    d.line([ox + r * 0.44, oy + r * 0.44, cx + r * 0.80, cy + r * 0.80], fill=col, width=int(w * 1.4))


def g_push(d, cx, cy, r, col):
    """Arrow leaving a line: push."""
    w = max(2, int(r * 0.15))
    d.line([cx - r * 0.80, cy + r * 0.78, cx + r * 0.80, cy + r * 0.78], fill=col, width=w)
    d.line([cx, cy + r * 0.42, cx, cy - r * 0.44], fill=col, width=w)
    d.polygon([(cx, cy - r * 0.92), (cx - r * 0.50, cy - r * 0.28),
               (cx + r * 0.50, cy - r * 0.28)], fill=col)


def g_wheel(d, cx, cy, r, col):
    """Steering wheel: take the build for a drive."""
    w = max(2, int(r * 0.15))
    _ring(d, cx, cy, r * 0.88, col, w)
    _dot(d, cx, cy, r * 0.24, col)
    for a in (90, 210, 330):
        d.line([cx + r * 0.20 * math.cos(math.radians(a)),
                cy + r * 0.20 * math.sin(math.radians(a)),
                cx + r * 0.80 * math.cos(math.radians(a)),
                cy + r * 0.80 * math.sin(math.radians(a))], fill=col, width=w)


def g_restart(d, cx, cy, r, col):
    """Circular arrow: close the test drive and bring it back up."""
    w = max(2, int(r * 0.15))
    d.arc([cx - r * 0.86, cy - r * 0.86, cx + r * 0.86, cy + r * 0.86],
          start=310, end=210, fill=col, width=w)
    # Arrowhead at the open end of the arc, pointing along the direction of travel.
    a = math.radians(310)
    hx, hy = cx + r * 0.86 * math.cos(a), cy + r * 0.86 * math.sin(a)
    h = r * 0.34
    d.polygon([(hx + h * math.cos(a - math.pi / 2), hy + h * math.sin(a - math.pi / 2)),
               (hx + h * math.cos(a + math.pi / 2 - 0.9),
                hy + h * math.sin(a + math.pi / 2 - 0.9)),
               (hx + h * math.cos(a + math.pi / 2 + 0.9),
                hy + h * math.sin(a + math.pi / 2 + 0.9))], fill=col)
    _dot(d, cx, cy, r * 0.20, col)


def g_branch(d, cx, cy, r, col):
    """Where the tree stands."""
    w = max(2, int(r * 0.14))
    d.line([cx - r * 0.42, cy - r * 0.60, cx - r * 0.42, cy + r * 0.62], fill=col, width=w)
    d.arc([cx - r * 0.42, cy - r * 0.30, cx + r * 0.86, cy + r * 0.62],
          start=270, end=360, fill=col, width=w)
    _dot(d, cx - r * 0.42, cy - r * 0.72, r * 0.26, col)
    _dot(d, cx - r * 0.42, cy + r * 0.74, r * 0.26, col)
    _dot(d, cx + r * 0.86, cy - r * 0.30, r * 0.26, col)


def g_semver(d, cx, cy, r, col, lit):
    """Three version fields with the one this key bumps lit."""
    step = r * 0.72
    for i in range(3):
        x = cx + (i - 1) * step
        c = col if i == lit else DIM
        d.rounded_rectangle([x - r * 0.24, cy - r * 0.24, x + r * 0.24, cy + r * 0.24],
                            radius=r * 0.08, fill=c)


def g_flag(d, cx, cy, r, col):
    """Wrap up."""
    w = max(2, int(r * 0.14))
    d.line([cx - r * 0.66, cy - r * 0.86, cx - r * 0.66, cy + r * 0.92], fill=col, width=w)
    cell = r * 0.42
    for row in range(2):
        for column in range(3):
            if (row + column) % 2:
                continue
            x0 = cx - r * 0.54 + column * cell
            y0 = cy - r * 0.74 + row * cell
            d.rectangle([x0, y0, x0 + cell, y0 + cell], fill=col)
    d.rectangle([cx - r * 0.54, cy - r * 0.74, cx - r * 0.54 + 3 * cell, cy - r * 0.74 + 2 * cell],
                outline=col, width=max(2, int(w * 0.6)))


# ---------------------------------------------------------------- tile assembly

def tile(path, glyph, accent, label, bar=None):
    img = Image.new('RGB', (S, S), BG)
    d = ImageDraw.Draw(img)

    d.rectangle([0, 0, S, int(11 * SS)], fill=bar or accent)

    glyph(d, S // 2, int(60 * SS), int(30 * SS), accent)

    font = ImageFont.truetype(FONT_PATH, int(15.5 * SS))
    text = label.upper()
    track = int(1.1 * SS)
    widths = [d.textlength(ch, font=font) for ch in text]
    total = sum(widths) + track * (len(text) - 1)
    x = (S - total) / 2
    y = int(108 * SS)
    for ch, cw in zip(text, widths):
        d.text((x, y), ch, font=font, fill=LABEL)
        x += cw + track

    img.resize((SIZE, SIZE), Image.LANCZOS).save(path)


# key name -> (glyph, accent, label)
KEYS = {
    'triage':     (g_triage, VIOLET, 'Triage'),
    'coord':      (g_hub, VIOLET, 'Coord'),
    'architect':  (g_compass, VIOLET, 'Architect'),
    'issue':      (g_issue, VIOLET, 'Issue'),
    'review':     (g_magnifier, VIOLET, 'Review'),

    'desktop':    (g_handoff, CYAN, 'Desktop'),
    'push':       (g_push, CYAN, 'Push'),
    'wrapup':     (g_flag, CYAN, 'Wrap up'),

    'testdrive':  (g_wheel, GREEN, 'Test drive'),
    'status':     (g_branch, GREEN, 'Status'),

    'restart':    (g_restart, BLUE, 'Restart'),

    'patch':      (lambda *a: g_semver(*a, lit=2), GREEN, 'Patch'),
    'minor':      (lambda *a: g_semver(*a, lit=1), AMBER, 'Minor'),
}


def main(out_dirs):
    for out in out_dirs:
        os.makedirs(out, exist_ok=True)
        for name, (glyph, accent, label) in KEYS.items():
            tile(os.path.join(out, name + '.png'), glyph, accent, label)
    print('wrote {0} tiles to {1} directories'.format(len(KEYS), len(out_dirs)))


if __name__ == '__main__':
    import sys
    main(sys.argv[1:] or ['.'])
