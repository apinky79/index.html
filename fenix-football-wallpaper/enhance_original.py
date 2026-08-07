#!/usr/bin/env python3
"""Enhance the user's original Fenix Football image for iPhone 17 Pro Max."""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageOps

TARGET_W, TARGET_H = 1320, 2868  # iPhone 17 Pro Max native resolution
OUT_DIR = Path(__file__).resolve().parent


def find_original() -> Path:
    for name in (
        "original.png",
        "original.jpg",
        "original.jpeg",
        "original.webp",
        "source.png",
        "source.jpg",
    ):
        path = OUT_DIR / name
        if path.exists():
            return path
    raise FileNotFoundError(
        "No source image found. Add your photo as fenix-football-wallpaper/original.jpg"
    )


def soft_shadow(size: tuple[int, int], blur: int = 40, opacity: int = 100) -> Image.Image:
    w, h = size
    shadow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(shadow)
    draw.ellipse((w * 0.08, h * 0.82, w * 0.92, h * 0.98), fill=(0, 0, 0, opacity))
    return shadow.filter(ImageFilter.GaussianBlur(blur))


def build_portrait_wallpaper(src: Image.Image) -> Image.Image:
    """Place the full original artwork on a portrait canvas — one layer, no overlap."""
    src = src.convert("RGB")
    sw, sh = src.size

    # Portrait canvas, black background.
    canvas = Image.new("RGB", (TARGET_W, TARGET_H), (0, 0, 0))

    # Scale to fit width; keep the entire square graphic visible.
    max_w = int(TARGET_W * 0.94)
    max_h = int(TARGET_H * 0.72)
    scale = min(max_w / sw, max_h / sh)
    new_w = max(1, int(sw * scale))
    new_h = max(1, int(sh * scale))
    artwork = src.resize((new_w, new_h), Image.Resampling.LANCZOS)

    # Center horizontally; sit in the middle third vertically (room for clock at top).
    x = (TARGET_W - new_w) // 2
    y = (TARGET_H - new_h) // 2

    # Single soft glow beneath the whole graphic (depth, no extra layers).
    glow = Image.new("RGBA", (TARGET_W, TARGET_H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(glow)
    cx = TARGET_W // 2
    cy = y + new_h - int(new_h * 0.08)
    draw.ellipse((cx - 340, cy - 80, cx + 340, cy + 120), fill=(255, 90, 20, 55))
    glow = glow.filter(ImageFilter.GaussianBlur(30))
    canvas = canvas.convert("RGBA")
    canvas = Image.alpha_composite(canvas, glow)

    # One soft shadow under the entire image.
    shadow = soft_shadow((new_w, new_h), blur=35, opacity=90)
    canvas.alpha_composite(shadow, (x, y))

    # Paste the complete original artwork once — no masks, no pop-out layers.
    canvas.paste(artwork, (x, y))
    return canvas.convert("RGB")


def enhance(img: Image.Image) -> Image.Image:
    img = ImageEnhance.Contrast(img).enhance(1.1)
    img = ImageEnhance.Color(img).enhance(1.08)
    img = ImageEnhance.Brightness(img).enhance(1.02)
    img = ImageEnhance.Sharpness(img).enhance(1.2)
    img = img.filter(ImageFilter.UnsharpMask(radius=1.3, percent=120, threshold=2))
    return img


def main() -> int:
    src_path = find_original()
    print(f"Using source: {src_path}")

    original = Image.open(src_path)
    original = ImageOps.exif_transpose(original)

    work = build_portrait_wallpaper(original)
    enhanced = enhance(work)

    png_out = OUT_DIR / "fenix-original-iphone17-pro-max.png"
    jpg_out = OUT_DIR / "fenix-original-iphone17-pro-max.jpg"
    enhanced.save(png_out, "PNG", optimize=True, dpi=(460, 460))
    enhanced.save(jpg_out, "JPEG", quality=93, optimize=True, dpi=(460, 460))

    print(f"Saved: {png_out} ({enhanced.size})")
    print(f"Saved: {jpg_out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
