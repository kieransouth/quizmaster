"""
Generate the Open Graph social card at web/public/og-image.png.

Run from the repo root:
    python docs/social/build_og_card.py

Output is a 1200×630 PNG sized for Twitter / Discord / LinkedIn.
The placeholder shipped in the repo is the home screenshot — replace
it by running this script. Re-run whenever the tagline or URL changes.
"""

from __future__ import annotations

import os
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

# ---------------------------------------------------------------- config

W, H = 1200, 630

# Match the app's dark-theme palette.
BG       = (15, 23, 42)      # slate-900
ACCENT   = (79, 70, 229)     # indigo-600 — same value as meta theme-color
WORDMARK = (255, 255, 255)
TAGLINE  = (226, 232, 240)   # slate-200
DIM      = (100, 116, 139)   # slate-500

WORDMARK_TEXT = "Quizmaster"
TAGLINE_TEXT  = "AI quiz wizard for team trivia nights"
SUB_TEXT      = "Generate · Fact-check · Host on a Discord screen-share"
URL_TEXT      = "quizmaster.spicy.gg"

OUTPUT = Path(__file__).resolve().parents[2] / "web" / "public" / "og-image.png"

# ---------------------------------------------------------------- fonts

# Pillow's bundled font is unusable for marketing assets; try common
# system-installed sans-serifs and fall back gracefully.
BOLD_CANDIDATES = [
    "C:/Windows/Fonts/segoeuib.ttf",
    "C:/Windows/Fonts/arialbd.ttf",
    "/System/Library/Fonts/Supplemental/Helvetica.ttc",
    "/System/Library/Fonts/HelveticaNeue.ttc",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
]
REGULAR_CANDIDATES = [
    "C:/Windows/Fonts/segoeui.ttf",
    "C:/Windows/Fonts/arial.ttf",
    "/System/Library/Fonts/Supplemental/Helvetica.ttc",
    "/System/Library/Fonts/HelveticaNeue.ttc",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
]


def font(size: int, *, bold: bool) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = BOLD_CANDIDATES if bold else REGULAR_CANDIDATES
    for path in candidates:
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    print(f"[warn] no system font found for {'bold' if bold else 'regular'}; using default (will look bad)")
    return ImageFont.load_default()


# ---------------------------------------------------------------- compose

def main() -> None:
    img = Image.new("RGB", (W, H), BG)

    # Soft indigo glow in the upper-right — adds depth without competing
    # with the text. Drawn as a solid-ish ellipse then heavily Gaussian-
    # blurred so the edges fade smoothly into the slate background.
    blob = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(blob).ellipse(
        [W - 500, -150, W + 100, 450],
        fill=(*ACCENT, 130),
    )
    blob = blob.filter(ImageFilter.GaussianBlur(radius=120))
    img  = Image.alpha_composite(img.convert("RGBA"), blob).convert("RGB")

    draw = ImageDraw.Draw(img)

    margin_x = 80

    # Wordmark — anchored toward the upper-left.
    wordmark_font = font(120, bold=True)
    wordmark_y    = 170
    draw.text((margin_x, wordmark_y), WORDMARK_TEXT, fill=WORDMARK, font=wordmark_font)

    # Indigo accent rule directly under the wordmark.
    accent_y = wordmark_y + 150
    draw.rectangle(
        [margin_x, accent_y, margin_x + 96, accent_y + 6],
        fill=ACCENT,
    )

    # Tagline.
    tagline_font = font(44, bold=False)
    draw.text(
        (margin_x, accent_y + 30),
        TAGLINE_TEXT,
        fill=TAGLINE,
        font=tagline_font,
    )

    # Sub-features bullet line.
    sub_font = font(26, bold=False)
    draw.text(
        (margin_x, accent_y + 100),
        SUB_TEXT,
        fill=DIM,
        font=sub_font,
    )

    # URL stamp — bottom-right.
    url_font = font(26, bold=False)
    url_bbox = draw.textbbox((0, 0), URL_TEXT, font=url_font)
    url_w    = url_bbox[2] - url_bbox[0]
    draw.text(
        (W - margin_x - url_w, H - margin_x),
        URL_TEXT,
        fill=DIM,
        font=url_font,
    )

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    img.save(OUTPUT, optimize=True)
    print(f"wrote {OUTPUT.relative_to(Path.cwd())}  ({OUTPUT.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
