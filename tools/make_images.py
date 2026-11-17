"""Generate About/Preview.png and About/thumb.png.

LaunchPad needs both to register the mod. Run from the repo root:

    python tools/make_images.py

Needs Pillow. The picture is a bundle of power cables, each with a label: they come in from
different places on the left, run parallel through the middle so the labels stack in a tidy
column, and leave at different places on the right. Bends are square, as cables are in game.

Colours follow the player convention: orange for power generation, purple for battery
connections, red for everything else (grey would be data only). Preview.png has all five
runs; thumb.png only the three red ones, since five labels are unreadable that small. Even
Preview.png is shown about 125 px wide on the Mod Info tab, where only the shapes read.
"""

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

# (label, colour, where it enters on the left, where it leaves on the right), top to bottom
# in the bundle. Entry and exit heights are fractions of the image height. Each list keeps
# the bundle's order on both sides, so no two runs cross, and puts every end at least ~0.07
# from its bundle height (Preview: 0.277, 0.431, 0.585, 0.739, 0.893; thumb: 0.385, 0.585,
# 0.785), so every bend reads as a bend.
FIVE = [
    ("SOLAR FARM", ORANGE, 0.17, 0.20),
    ("BATTERIES", PURPLE, 0.36, 0.51),
    ("LIFE SUPPORT", RED, 0.50, 0.66),
    ("HABITAT", RED, 0.82, 0.81),
    ("LANDING PAD", RED, 0.96, 0.955),
]
THREE = [
    ("LIFE SUPPORT", RED, 0.24, 0.48),
    ("HABITAT", RED, 0.69, 0.66),
    ("LANDING PAD", RED, 0.95, 0.90),
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


def cable(draw, points, thick, colours):
    """A run through points with square corners: dark underneath, light offset up and left,
    so each cable keeps a shaded lower or right edge."""
    light, dark = colours
    shade = thick / 4
    for colour, off, t in ((dark, 0, thick), (light, -shade / 2, thick - shade)):
        for (x0, y0), (x1, y1) in zip(points, points[1:]):
            h = t / 2
            draw.rectangle([min(x0, x1) - h + off, min(y0, y1) - h + off,
                            max(x0, x1) + h + off, max(y0, y1) + h + off], fill=colour)


def render(w, h, runs):
    img = Image.new("RGB", (w, h))
    draw = ImageDraw.Draw(img)
    for row in range(h):
        t = row / (h - 1)
        draw.line([(0, row), (w, row)],
                  fill=tuple(int(a + (b - a) * t) for a, b in zip(BG_TOP, BG_BOTTOM)))

    title = int(h * 0.085)
    draw.text((w / 2, h * 0.075), "CABLE LABELS", font=font(title, bold=True),
              fill=AMBER, anchor="mm")

    # The bundle: evenly spaced, centred in the space under the title.
    top, bottom = h * 0.20, h * 0.97
    pitch = min((bottom - top) / len(runs), h * 0.2)
    first = (top + bottom) / 2 - pitch * (len(runs) - 1) / 2
    label_h = pitch * 0.62
    thick = label_h * 0.36
    text = font(int(label_h * 0.56), bold=True)
    label_w = max(draw.textlength(r[0], font=text) for r in runs) + label_h * 1.1

    # Bends: the outermost runs turn nearest the bundle, so no vertical crosses another run.
    centre = (len(runs) - 1) / 2
    left_bus, right_bus = w / 2 - label_w / 2 - w * 0.06, w / 2 + label_w / 2 + w * 0.06
    step = w * 0.045
    for i, (name, colours, y_in, y_out) in enumerate(runs):
        y = first + i * pitch
        reach = centre - abs(i - centre)
        x_in = left_bus - step * (1 + reach)
        x_out = right_bus + step * (1 + reach)
        cable(draw, [(-thick, h * y_in), (x_in, h * y_in), (x_in, y), (x_out, y),
                     (x_out, h * y_out), (w + thick, h * y_out)], thick, colours)

    for i, (name, _, _, _) in enumerate(runs):
        y = first + i * pitch
        box = [w / 2 - label_w / 2, y - label_h / 2, w / 2 + label_w / 2, y + label_h / 2]
        draw.rounded_rectangle(box, radius=label_h / 6, fill=LABEL, outline=LABEL_EDGE,
                               width=max(1, int(label_h / 20)))
        draw.text((w / 2, y), name, font=text, fill=INK, anchor="mm")
    return img


def main():
    about = ROOT / "About"
    render(1280, 720, FIVE).save(about / "Preview.png", optimize=True)
    render(640, 360, THREE).save(about / "thumb.png", optimize=True)


if __name__ == "__main__":
    main()
