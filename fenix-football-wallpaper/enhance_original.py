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


def build_portrait_canvas(src: Image.Image) -> Image.Image:
    """Center the source graphic on a portrait canvas sized for Pro Max."""
    src = src.convert("RGBA")
    sw, sh = src.size

    # Scale to fit width with breathing room; keep full graphic visible.
    max_w = int(TARGET_W * 0.92)
    max_h = int(TARGET_H * 0.78)
    scale = min(max_w / sw, max_h / sh)
    new_size = (max(1, int(sw * scale)), max(1, int(sh * scale)))
    scaled = src.resize(new_size, Image.Resampling.LANCZOS)

    # Dark background pulled from image corners (preserves black/fire look).
    rgb_src = src.convert("RGB")
    corner = rgb_src.getpixel((min(10, sw - 1), min(10, sh - 1)))
    canvas = Image.new("RGB", (TARGET_W, TARGET_H), corner)

    # Soft vertical glow extension from lower fire tones.
    glow = Image.new("RGBA", (TARGET_W, TARGET_H), (0, 0, 0, 0))
    for y in range(TARGET_H // 2, TARGET_H):
        alpha = int(40 * (y - TARGET_H // 2) / (TARGET_H // 2))
        for x in range(TARGET_W):
            glow.putpixel((x, y), (180, 70, 20, alpha))
    canvas = Image.alpha_composite(canvas.convert("RGBA"), glow).convert("RGB")

    x = (TARGET_W - scaled.width) // 2
    y = int(TARGET_H * 0.12) + (max_h - scaled.height) // 2
    canvas.paste(scaled, (x, y), scaled)
    return canvas


def enhance(img: Image.Image) -> Image.Image:
    img = ImageEnhance.Contrast(img).enhance(1.08)
    img = ImageEnhance.Color(img).enhance(1.06)
    img = ImageEnhance.Brightness(img).enhance(1.02)
    img = ImageEnhance.Sharpness(img).enhance(1.2)
    # Mild local contrast without changing composition.
    img = img.filter(ImageFilter.UnsharpMask(radius=1.2, percent=110, threshold=3))
    return img


def main() -> int:
    src_path = find_original()
    print(f"Using source: {src_path}")

    original = Image.open(src_path)
    original = ImageOps.exif_transpose(original)

    # If already portrait-ish and large enough, upscale directly; else compose.
    w, h = original.size
    if w / h > 0.55:
        work = build_portrait_canvas(original)
    else:
        work = original.convert("RGB")
        work = ImageOps.fit(work, (TARGET_W, TARGET_H), method=Image.Resampling.LANCZOS, centering=(0.5, 0.45))

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
