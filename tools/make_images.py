"""Generate About/Preview.png and About/thumb.png.

LaunchPad needs both to register the mod. Run from the repo root:

    python tools/make_images.py

Needs Pillow. The picture is a pipe with its pipe label above a cable with the thinner cable
label, each label reading its name. Keep small print out of it: the Mod Info tab shows it about
125 px wide.
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
FONT_DIRS = [Path("C:/Windows/Fonts"), Path("/usr/share/fonts/truetype/dejavu")]

BG_TOP = (22, 27, 36)
BG_BOTTOM = (12, 15, 21)
PIPE = (150, 156, 165)
PIPE_DARK = (104, 110, 120)
CABLE = (210, 60, 50)
CABLE_DARK = (150, 40, 34)
LABEL = (236, 232, 214)
LABEL_EDGE = (170, 165, 148)
INK = (24, 26, 30)
AMBER = (255, 165, 0)


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


def run(draw, w, y, thick, light, dark):
    """A horizontal pipe or cable across the whole width, centred on y."""
    draw.rectangle([0, y - thick / 2, w, y + thick / 2], fill=light)
    draw.rectangle([0, y + thick / 6, w, y + thick / 2], fill=dark)


def sleeve(draw, cx, y, length, thick, text, size):
    """A label sleeve wrapped round a run, with its text."""
    box = [cx - length / 2, y - thick / 2, cx + length / 2, y + thick / 2]
    draw.rounded_rectangle(box, radius=thick / 6, fill=LABEL, outline=LABEL_EDGE,
                           width=max(1, int(thick / 20)))
    f = font(size, bold=True)
    draw.text((cx, y), text, font=f, fill=INK, anchor="mm")


def render(w, h):
    img = Image.new("RGB", (w, h))
    draw = ImageDraw.Draw(img)
    for row in range(h):
        t = row / (h - 1)
        draw.line([(0, row), (w, row)],
                  fill=tuple(int(a + (b - a) * t) for a, b in zip(BG_TOP, BG_BOTTOM)))
    s = w / 1280

    draw.text((w / 2, 110 * s), "CABLE LABELS", font=font(int(84 * s), bold=True),
              fill=AMBER, anchor="mm")

    pipe_y, pipe_t = 330 * s, 110 * s
    run(draw, w, pipe_y, pipe_t * 0.75, PIPE, PIPE_DARK)
    sleeve(draw, w / 2, pipe_y, 520 * s, pipe_t, "O2 SUPPLY", int(56 * s))

    cable_y, cable_t = 540 * s, 64 * s
    run(draw, w, cable_y, cable_t * 0.55, CABLE, CABLE_DARK)
    sleeve(draw, w / 2, cable_y, 520 * s, cable_t, "MAIN BUS", int(40 * s))
    return img


def main():
    about = ROOT / "About"
    render(1280, 720).save(about / "Preview.png", optimize=True)
    render(640, 360).save(about / "thumb.png", optimize=True)


if __name__ == "__main__":
    main()
