"""Generate About/Preview.png and About/thumb.png.

LaunchPad needs both to register the mod; they live in Assets/About/. Run from the repo root:

    python tools/make_images.py

Needs Pillow. The picture is a bundle of power cables, each with a label: they come in from
different rows on the left, run parallel through the middle so the labels stack in a tidy
column, and leave by different rows on the right.

The cables follow the game's geometry. Rows are one 0.5 m cell apart. A standard cable is
5 cm across, a tenth of a cell, and turns on a 25 cm radius, half a cell, so a jog to the next
row is two quarter arcs back to back. The labels are drawn far larger than in game, to be
readable.

Colours follow the player convention: orange for power generation, purple for battery
connections, red for everything else (grey would be data only). Preview.png has all five
runs; thumb.png only the three red ones, since five labels are unreadable that small. Even
Preview.png is shown about 125 px wide on the Mod Info tab, where only the shapes read.
"""

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
FONT_DIRS = [Path("C:/Windows/Fonts"), Path("/usr/share/fonts/truetype/dejavu")]

BG_TOP = (22, 27, 36)
BG_BOTTOM = (12, 15, 21)
LABEL = (236, 232, 214)
LABEL_EDGE = (170, 165, 148)
INK = (24, 26, 30)
AMBER = (255, 165, 0)

ORANGE = ((236, 134, 36), (170, 92, 20))
PURPLE = ((150, 86, 206), (100, 54, 146))
RED = ((210, 60, 50), (150, 40, 34))

# (label, colour, row it enters on the left, row it leaves by on the right), top to bottom in
# the bundle. The bundle's runs sit in rows 1, 2, ...; each end is the run's own row, one above
# (-1) or one below (+1). Each side keeps the bundle's order, so no two runs cross.
FIVE = [
    ("SOLAR FARM", ORANGE, -1, -1),
    ("BATTERIES", PURPLE, -1, +1),
    ("LIFE SUPPORT", RED, +1, +1),
    ("HABITAT", RED, +1, +1),
    ("LANDING PAD", RED, +1, +1),
]
THREE = [
    ("LIFE SUPPORT", RED, -1, +1),
    ("HABITAT", RED, +1, +1),
    ("LANDING PAD", RED, +1, +1),
]


def font(size, bold=False):
    names = ["segoeuib.ttf", "arialbd.ttf", "DejaVuSans-Bold.ttf"] if bold else [
        "segoeui.ttf",
        "arial.ttf",
        "DejaVuSans.ttf",
    ]
    for d in FONT_DIRS:
        for n in names:
            if (d / n).exists():
                return ImageFont.truetype(str(d / n), size)
    return ImageFont.load_default()


def path(x0, y0, x1, y1, bends, radius):
    """Points along a run from (x0, y0) to (x1, y1): horizontal, with an S-bend of two quarter
    arcs at each x in bends taking it to the next height in turn."""
    points = [(x0, y0)]
    y = y0
    for bx, target in bends:
        if target == y:
            continue
        s = 1 if target > y else -1
        steps = 24
        # Turn from heading right to heading along s, about a centre beside the bend.
        cx, cy = bx, y + s * radius
        for k in range(steps + 1):
            a = math.pi / 2 * k / steps
            points.append((cx + radius * math.sin(a), cy - s * radius * math.cos(a)))
        # Then straight on, if the jog is longer than two radii, and turn back to the right.
        cx, cy = bx + 2 * radius, target - s * radius
        for k in range(steps + 1):
            a = math.pi / 2 * k / steps
            points.append((cx - radius * math.cos(a), cy + s * radius * math.sin(a)))
        y = target
    points.append((x1, y))
    return points


def cable(draw, points, thick, colours):
    """A run through points: dark underneath, light offset up and left, so each cable keeps a
    shaded lower or right edge."""
    light, dark = colours
    shade = thick / 4
    for colour, off, t in ((dark, 0, thick), (light, -shade / 2, thick - shade)):
        draw.line([(x + off, y + off) for x, y in points], fill=colour, width=max(1, round(t)),
                  joint="curve")


def render(w, h, runs):
    # Drawn at three times the size and scaled down, to smooth the arcs.
    k = 3
    w, h = w * k, h * k
    img = Image.new("RGB", (w, h))
    draw = ImageDraw.Draw(img)
    for row in range(h):
        t = row / (h - 1)
        draw.line([(0, row), (w, row)],
                  fill=tuple(int(a + (b - a) * t) for a, b in zip(BG_TOP, BG_BOTTOM)))

    title = int(h * 0.085)
    draw.text((w / 2, h * 0.075), "CABLE LABELS", font=font(title, bold=True),
              fill=AMBER, anchor="mm")

    # Rows a cell apart, the bundle's runs in rows 1 to n and a spare row above and below.
    top, bottom = h * 0.18, h * 0.975
    cell = (bottom - top) / (len(runs) + 1)
    row = lambda r: top + r * cell
    thick = cell / 10
    radius = cell / 2
    label_h = cell * 0.62
    text = font(int(label_h * 0.56), bold=True)
    label_w = max(draw.textlength(r[0], font=text) for r in runs) + label_h * 1.1

    # Runs jogging the same way bend at the same x, nested as a real bundle is.
    left_bend = w / 2 - label_w / 2 - w * 0.06 - 2 * radius
    right_bend = w / 2 + label_w / 2 + w * 0.06
    for i, (name, colours, end_in, end_out) in enumerate(runs):
        r = i + 1
        y_in, y, y_out = row(r + end_in), row(r), row(r + end_out)
        cable(draw, path(-thick, y_in, w + thick, y_out,
                         [(left_bend, y), (right_bend, y_out)], radius), thick, colours)

    for i, (name, _, _, _) in enumerate(runs):
        y = row(i + 1)
        box = [w / 2 - label_w / 2, y - label_h / 2, w / 2 + label_w / 2, y + label_h / 2]
        draw.rounded_rectangle(box, radius=label_h / 6, fill=LABEL, outline=LABEL_EDGE,
                               width=max(1, int(label_h / 20)))
        draw.text((w / 2, y), name, font=text, fill=INK, anchor="mm")
    return img.resize((w // k, h // k), Image.LANCZOS)


def main():
    about = ROOT / "Assets" / "About"
    render(1280, 720, FIVE).save(about / "Preview.png", optimize=True)
    render(640, 360, THREE).save(about / "thumb.png", optimize=True)


if __name__ == "__main__":
    main()
