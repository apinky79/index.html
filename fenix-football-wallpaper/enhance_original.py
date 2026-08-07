#!/usr/bin/env python3
"""Enhance the user's original Fenix Football image for iPhone 17 Pro Max."""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageEnhance, ImageFilter, ImageOps

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


def build_portrait_wallpaper(src: Image.Image) -> Image.Image:
    """
    Fill the full portrait screen with the original artwork.

    Uses 'cover' scaling (like iPhone wallpaper zoom): scales up uniformly,
    then center-crops — no rotation, no layer splitting, no overlap.
    """
    src = src.convert("RGB")

    # Uniform scale until the image covers the entire portrait canvas.
    scale = max(TARGET_W / src.width, TARGET_H / src.height)
    scaled_w = max(1, int(src.width * scale))
    scaled_h = max(1, int(src.height * scale))
    scaled = src.resize((scaled_w, scaled_h), Image.Resampling.LANCZOS)

    # Center-crop to exact phone resolution.
    left = (scaled_w - TARGET_W) // 2
    top = (scaled_h - TARGET_H) // 2
    cropped = scaled.crop((left, top, left + TARGET_W, top + TARGET_H))
    return cropped


def enhance(img: Image.Image) -> Image.Image:
    img = ImageEnhance.Contrast(img).enhance(1.08)
    img = ImageEnhance.Color(img).enhance(1.06)
    img = ImageEnhance.Brightness(img).enhance(1.02)
    img = ImageEnhance.Sharpness(img).enhance(1.15)
    img = img.filter(ImageFilter.UnsharpMask(radius=1.2, percent=115, threshold=2))
    return img


def main() -> int:
    src_path = find_original()
    print(f"Using source: {src_path}")

    original = Image.open(src_path)
    original = ImageOps.exif_transpose(original)

    work = build_portrait_wallpaper(original)
    enhanced = enhance(work)

    # New filename so phones don't serve a cached broken version.
    png_out = OUT_DIR / "fenix-portrait-fullscreen-iphone17.png"
    jpg_out = OUT_DIR / "fenix-portrait-fullscreen-iphone17.jpg"
    enhanced.save(png_out, "PNG", optimize=True, dpi=(460, 460))
    enhanced.save(jpg_out, "JPEG", quality=93, optimize=True, dpi=(460, 460))

    # Keep legacy names in sync.
    legacy_png = OUT_DIR / "fenix-original-iphone17-pro-max.png"
    legacy_jpg = OUT_DIR / "fenix-original-iphone17-pro-max.jpg"
    enhanced.save(legacy_png, "PNG", optimize=True, dpi=(460, 460))
    enhanced.save(legacy_jpg, "JPEG", quality=93, optimize=True, dpi=(460, 460))

    print(f"Saved: {png_out} ({enhanced.size})")
    print(f"Saved: {jpg_out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
