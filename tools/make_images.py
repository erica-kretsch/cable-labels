"""Generate About/Preview.png and About/thumb.png.

LaunchPad needs both to register the mod; they live in Assets/About/. Run from the repo root:

    python tools/make_images.py

Needs Pillow. The picture is a bundle of power cables, each with a label, laid out as a player
might run them in game, with the labels stacked in a tidy column where the runs are parallel:

- orange comes down from the top, runs across and goes back up;
- purple runs straight across;
- the reds come up from the bottom and turn right in concentric corners. The top one runs
  straight off the side, the middle one drops two rows and then leaves by the side, and the
  bottom one turns down and leaves by the bottom.

The cables follow the game's geometry. Rows and columns are one 0.5 m cell apart. A standard
cable is 5 cm across, a tenth of a cell, and turns on a 25 cm radius, half a cell, so
neighbouring corners are the same size, stepped a cell apart. The labels are drawn far larger
than in game, to be readable.

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


# Each run is (label, colour, route). A route gives the run's corners in grid terms, given:
#   row(n)  the height of row n; the labelled runs sit in rows 1, 2, ...
#   left(k) the k-th column left of the labels, right(k) the k-th to their right (k = 0
#           nearest), each far enough out that a corner there clears the labels;
#   top, bottom, side_l, side_r  just off each edge of the picture.
# The runs here keep clear of each other: the reds' corners nest a column apart.
def reds(first):
    """The three red runs, the top one in row `first`."""
    a, b, c = first, first + 1, first + 2
    return [
        ("LIFE SUPPORT", RED, lambda g: [(g.left(2), g.bottom), (g.left(2), g.row(a)),
                                         (g.side_r, g.row(a))]),
        ("HABITAT", RED, lambda g: [(g.left(1), g.bottom), (g.left(1), g.row(b)),
                                    (g.right(1), g.row(b)), (g.right(1), g.row(c + 1)),
                                    (g.side_r, g.row(c + 1))]),
        ("LANDING PAD", RED, lambda g: [(g.left(0), g.bottom), (g.left(0), g.row(c)),
                                        (g.right(0), g.row(c)), (g.right(0), g.bottom)]),
    ]


FIVE = [
    ("SOLAR FARM", ORANGE, lambda g: [(g.left(1), g.top), (g.left(1), g.row(1)),
                                      (g.right(1), g.row(1)), (g.right(1), g.top)]),
    ("BATTERIES", PURPLE, lambda g: [(g.side_l, g.row(2)), (g.side_r, g.row(2))]),
] + reds(3)
THREE = reds(1)


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


def rounded(corners, radius, steps=24):
    """Points along a route through axis-aligned corners, each corner turned on a quarter arc
    of the given radius."""
    points = [corners[0]]
    for prev, here, nxt in zip(corners, corners[1:], corners[2:]):
        d1 = unit(prev, here)
        d2 = unit(here, nxt)
        start = (here[0] - d1[0] * radius, here[1] - d1[1] * radius)
        end = (here[0] + d2[0] * radius, here[1] + d2[1] * radius)
        centre = (start[0] + d2[0] * radius, start[1] + d2[1] * radius)
        u = (start[0] - centre[0], start[1] - centre[1])
        v = (end[0] - centre[0], end[1] - centre[1])
        for k in range(steps + 1):
            a = math.pi / 2 * k / steps
            points.append((centre[0] + math.cos(a) * u[0] + math.sin(a) * v[0],
                           centre[1] + math.cos(a) * u[1] + math.sin(a) * v[1]))
    points.append(corners[-1])
    return points


def unit(a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dy)
    return dx / length, dy / length


def cable(draw, points, thick, colours):
    """A run through points: dark underneath, light offset up and left, so each cable keeps a
    shaded lower or right edge."""
    light, dark = colours
    shade = thick / 4
    for colour, off, t in ((dark, 0, thick), (light, -shade / 2, thick - shade)):
        draw.line([(x + off, y + off) for x, y in points], fill=colour, width=max(1, round(t)),
                  joint="curve")


class Grid:
    """Where rows, columns and the picture's edges are, in pixels."""

    def __init__(self, w, h, rows, cell, label_left, label_right, thick):
        self.cell = cell
        self._top_row = rows
        margin = cell * 0.5
        self._left0 = label_left - margin - cell / 2
        self._right0 = label_right + margin + cell / 2
        self.top, self.bottom = -thick, h + thick
        self.side_l, self.side_r = -thick, w + thick

    def row(self, n):
        return self._top_row + n * self.cell

    def left(self, k):
        return self._left0 - k * self.cell

    def right(self, k):
        return self._right0 + k * self.cell


def render(w, h, runs, spare_rows):
    # Drawn at three times the size and scaled down, to smooth the arcs.
    k = 3
    w, h = w * k, h * k
    img = Image.new("RGB", (w, h))
    draw = ImageDraw.Draw(img)
    for y in range(h):
        t = y / (h - 1)
        draw.line([(0, y), (w, y)],
                  fill=tuple(int(a + (b - a) * t) for a, b in zip(BG_TOP, BG_BOTTOM)))

    title = int(h * 0.085)
    draw.text((w / 2, h * 0.075), "CABLE LABELS", font=font(title, bold=True),
              fill=AMBER, anchor="mm")

    # Rows a cell apart: row 0 just under the title, the labelled runs in rows 1 to n, and
    # spare rows below for runs that drop.
    top, bottom = h * 0.18, h * 0.975
    cell = (bottom - top) / (len(runs) + spare_rows)
    thick = cell / 10
    label_h = cell * 0.62
    text = font(int(label_h * 0.56), bold=True)
    label_w = max(draw.textlength(r[0], font=text) for r in runs) + label_h * 1.1
    grid = Grid(w, h, top, cell, w / 2 - label_w / 2, w / 2 + label_w / 2, thick)

    for name, colours, route in runs:
        cable(draw, rounded(route(grid), cell / 2), thick, colours)

    for i, (name, _, _) in enumerate(runs):
        y = grid.row(i + 1)
        box = [w / 2 - label_w / 2, y - label_h / 2, w / 2 + label_w / 2, y + label_h / 2]
        draw.rounded_rectangle(box, radius=label_h / 6, fill=LABEL, outline=LABEL_EDGE,
                               width=max(1, int(label_h / 20)))
        draw.text((w / 2, y), name, font=text, fill=INK, anchor="mm")
    return img.resize((w // k, h // k), Image.LANCZOS)


def main():
    about = ROOT / "Assets" / "About"
    # The middle red drops two rows below the bottom one's, so each picture has a spare row
    # below the labels besides row 0 above.
    render(1280, 720, FIVE, 2).save(about / "Preview.png", optimize=True)
    render(640, 360, THREE, 2).save(about / "thumb.png", optimize=True)


if __name__ == "__main__":
    main()
