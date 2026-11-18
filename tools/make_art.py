"""Generate the cable label's textures and icons into Assets/Art/.

Run from the repo root:

    python tools/make_art.py

Needs Pillow. Writes:

- CableLabelKit.png: the kit's label texture, "Cable Label" as the vanilla pipe label kit's
  texture has "Pipe Label": white letters with a black outline and drop shadow on white. The
  label mesh maps the whole texture onto each panel (Assets/Editor/BuildPrefabs.cs), so it is
  drawn at the panel's shape, about 4:1, not square as vanilla's is.
- ThumbCableLabel.png: the placed label's icon, a sleeve on a short run of red cable.
- ThumbCableLabelKit.png: the kit's icon, the sleeve alone.

The icons must not be the vanilla pipe label's: the creative spawn menu's ImGui buttons take
their ID from the icon's texture, so two things sharing one icon share one button and only one
of them can be picked.

Assets/Editor/BuildPrefabs.cs sets the import settings (sprites for the icons) and wires them
up; edit this script, not the PNGs.
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "Art"
FONT = Path("C:/Windows/Fonts/bahnschrift.ttf")

WHITE = (246, 246, 242, 255)
SHADE = (196, 198, 200, 255)
EDGE = (120, 124, 130, 255)
CABLE = (210, 60, 50, 255)
CABLE_DARK = (150, 40, 34, 255)


def condensed(size):
    f = ImageFont.truetype(str(FONT), size)
    f.set_variation_by_name("Bold Condensed")
    return f


def kit_texture():
    w, h = 1024, 256
    img = Image.new("RGBA", (w, h), WHITE)
    f = condensed(196)
    text = "Cable Label"
    # Drop shadow, then a black outline, then white letters, as vanilla's.
    shadow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).text((w / 2 + 10, h / 2 + 12), text, font=f, fill=(0, 0, 0, 255),
                                anchor="mm", stroke_width=6, stroke_fill=(0, 0, 0, 255))
    img.alpha_composite(shadow)
    ImageDraw.Draw(img).text((w / 2, h / 2), text, font=f, fill=WHITE, anchor="mm",
                             stroke_width=6, stroke_fill=(0, 0, 0, 255))
    img.convert("RGB").save(OUT / "CableLabelKit.png", optimize=True)


def sleeve(draw, x0, x1, cy, r):
    """A short white cylinder seen from the side, lit from above."""
    draw.rounded_rectangle([x0, cy - r, x1, cy + r], radius=r * 0.35, fill=WHITE,
                           outline=EDGE, width=max(2, int(r / 9)))
    draw.rectangle([x0 + r * 0.25, cy + r * 0.35, x1 - r * 0.25, cy + r * 0.75], fill=SHADE)
    # The faint text band.
    for i, frac in enumerate((0.22, 0.42, 0.62)):
        draw.rectangle([x0 + (x1 - x0) * frac, cy - r * 0.25,
                        x0 + (x1 - x0) * (frac + 0.14), cy + r * 0.05], fill=SHADE)


def icon(with_cable):
    size = 256
    big = 4 * size
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cy = big / 2
    if with_cable:
        t = big * 0.035
        draw.rectangle([big * 0.02, cy - t, big * 0.98, cy + t], fill=CABLE)
        draw.rectangle([big * 0.02, cy + t * 0.3, big * 0.98, cy + t], fill=CABLE_DARK)
    sleeve(draw, big * 0.2, big * 0.8, cy, big * (0.1 if with_cable else 0.12))
    img = img.rotate(35, resample=Image.BICUBIC)
    img = img.resize((size, size), Image.LANCZOS)
    # A soft dark halo, so the white sleeve reads on the game's light and dark panels.
    halo = img.split()[3].filter(ImageFilter.GaussianBlur(3)).point(lambda a: int(a * 0.55))
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.putalpha(halo)
    out.alpha_composite(img)
    return out


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    kit_texture()
    icon(True).save(OUT / "ThumbCableLabel.png", optimize=True)
    icon(False).save(OUT / "ThumbCableLabelKit.png", optimize=True)


if __name__ == "__main__":
    main()
