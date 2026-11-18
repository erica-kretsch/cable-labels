"""Generate the cable label kit's texture, Assets/Art/CableLabelKit.png.

Run from the repo root:

    python tools/make_art.py

Needs Pillow. "Cable Label" as the vanilla pipe label kit's texture has "Pipe Label": white
letters with a black outline and drop shadow on white. The label mesh maps the whole texture
onto each panel (Assets/Editor/BuildPrefabs.cs), so it is drawn at the panel's shape, about
4:1, not square as vanilla's is.

The icons are camera renders of the mesh, made in Unity by Assets/Editor/RenderIcons.cs, as
the game's are; this draws only the texture they show. Edit this script, not the PNG.
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "Art"
FONT = Path("C:/Windows/Fonts/bahnschrift.ttf")

WHITE = (246, 246, 242, 255)


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


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    kit_texture()


if __name__ == "__main__":
    main()
