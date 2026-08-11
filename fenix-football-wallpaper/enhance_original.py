#!/usr/bin/env python3
"""Professionally enhance the user's Fenix Football photo for iPhone 17 Pro Max."""

from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageOps

TARGET_W, TARGET_H = 1320, 2868
OUT_DIR = Path(__file__).resolve().parent


def find_original() -> Path:
    for name in ("original.png", "original.jpg", "original.jpeg", "original.webp"):
        path = OUT_DIR / name
        if path.exists():
            return path
    raise FileNotFoundError("Add your photo as fenix-football-wallpaper/original.jpg")


def fit_portrait(
    src: Image.Image,
    width_pct: float = 0.72,
    height_pct: float = 0.48,
    vertical_anchor: float = 0.44,
) -> Image.Image:
    """
    Place artwork on portrait canvas.

    vertical_anchor: 0 = top, 0.5 = centre, 1 = bottom.
    Default 0.44 balances lock screen (clock at top) and home screen (icons mid).
    """
    max_w = int(TARGET_W * width_pct)
    max_h = int(TARGET_H * height_pct)
    scale = min(max_w / src.width, max_h / src.height)
    w, h = max(1, int(src.width * scale)), max(1, int(src.height * scale))
    artwork = src.resize((w, h), Image.Resampling.LANCZOS)

    canvas = Image.new("RGB", (TARGET_W, TARGET_H), (0, 0, 0))
    x = (TARGET_W - w) // 2
    free_y = TARGET_H - h
    y = int(free_y * vertical_anchor)
    canvas.paste(artwork, (x, y))
    return canvas


def rgb_to_hsv(arr: np.ndarray) -> np.ndarray:
    arr = arr.astype(np.float32) / 255.0
    r, g, b = arr[..., 0], arr[..., 1], arr[..., 2]
    mx = np.max(arr, axis=-1)
    mn = np.min(arr, axis=-1)
    diff = mx - mn + 1e-6
    h = np.zeros_like(mx)
    mask = mx == r
    h[mask] = ((g - b)[mask] / diff[mask]) % 6
    mask = mx == g
    h[mask] = ((b - r)[mask] / diff[mask]) + 2
    mask = mx == b
    h[mask] = ((r - g)[mask] / diff[mask]) + 4
    h = h / 6.0
    s = diff / (mx + 1e-6)
    v = mx
    return np.stack([h, s, v], axis=-1)


def hsv_to_rgb(hsv: np.ndarray) -> np.ndarray:
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    i = np.floor(h * 6).astype(int)
    f = h * 6 - i
    p = v * (1 - s)
    q = v * (1 - f * s)
    t = v * (1 - (1 - f) * s)
    out = np.zeros((*h.shape, 3), dtype=np.float32)
    for idx, (r, g, b) in enumerate([
        (v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q),
    ]):
        m = i % 6 == idx
        out[m, 0], out[m, 1], out[m, 2] = r[m], g[m], b[m]
    return (np.clip(out, 0, 1) * 255).astype(np.uint8)


def professional_grade(img: Image.Image) -> Image.Image:
    """Visible polish: richer fire, deeper blacks, sharper subject, cinematic tone."""
    arr = np.array(img.convert("RGB"), dtype=np.float32)
    hsv = rgb_to_hsv(arr.astype(np.uint8))

    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]

    # Boost warm fire / embers without touching neutral blacks.
    warm = ((h < 0.12) | (h > 0.92)) & (s > 0.15) & (v > 0.08)
    s[warm] = np.clip(s[warm] * 1.35, 0, 1)
    v[warm] = np.clip(v[warm] * 1.18, 0, 1)

    # Deepen shadows for punch (OLED-friendly).
    dark = v < 0.18
    v[dark] *= 0.72

    # Lift midtones on the white kit slightly.
    mid = (v > 0.35) & (v < 0.85) & (s < 0.45)
    v[mid] = np.clip(v[mid] * 1.06, 0, 1)

    graded = Image.fromarray(hsv_to_rgb(np.stack([h, s, v], axis=-1)))

    # Split-tone: warm highlights, cool shadow tint.
    warm_glow = Image.new("RGB", graded.size, (255, 140, 40))
    cool_shadow = Image.new("RGB", graded.size, (20, 30, 60))
    lum = graded.convert("L")
    hi = lum.point(lambda p: int(max(0, (p - 140) * 1.8)))
    lo = lum.point(lambda p: int(max(0, (100 - p) * 1.2)))
    graded = Image.composite(warm_glow, graded, hi)
    graded = Image.composite(cool_shadow, graded, lo)

    graded = ImageEnhance.Contrast(graded).enhance(1.14)
    graded = ImageEnhance.Color(graded).enhance(1.12)
    return graded


def boost_fire_glow(img: Image.Image) -> Image.Image:
    """Make flames and stadium lights bloom — the hero energy of the poster."""
    arr = np.array(img.convert("RGB"), dtype=np.float32)
    r, g, b = arr[..., 0], arr[..., 1], arr[..., 2]
    fire = (r > 90) & (g > 40) & (r > g) & (g > b * 0.7)
    glow = np.zeros_like(arr)
    glow[..., 0] = np.where(fire, r * 0.6, 0)
    glow[..., 1] = np.where(fire, g * 0.35, 0)
    glow[..., 2] = np.where(fire, b * 0.08, 0)
    glow_img = Image.fromarray(np.clip(glow, 0, 255).astype(np.uint8))
    glow_img = glow_img.filter(ImageFilter.GaussianBlur(12))
    return Image.blend(img, ImageChops.add(img, glow_img), 0.55)


def clarity_and_sharpen(img: Image.Image) -> Image.Image:
    """Crisp detail on jersey, face, and number."""
    blur = img.filter(ImageFilter.GaussianBlur(2.2))
    detail = ImageChops.subtract(img, blur)
    detail = ImageEnhance.Contrast(detail).enhance(1.6)
    sharp = ImageChops.add(img, detail)
    sharp = sharp.filter(ImageFilter.UnsharpMask(radius=1.8, percent=160, threshold=2))
    return ImageEnhance.Sharpness(sharp).enhance(1.2)


def subtle_depth_lighting(img: Image.Image) -> Image.Image:
    """Single-layer 3D feel: light from above-left, no duplicated layers."""
    w, h = img.size
    light = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(light)
    draw.ellipse((-w * 0.2, -h * 0.35, w * 0.85, h * 0.55), fill=(255, 210, 140, 45))
    draw.ellipse((w * 0.15, h * 0.55, w * 1.1, h * 1.2), fill=(0, 0, 0, 80))
    light = light.filter(ImageFilter.GaussianBlur(40))
    result = Image.alpha_composite(img.convert("RGBA"), light)
    return result.convert("RGB")


def edge_vignette(img: Image.Image) -> Image.Image:
    w, h = img.size
    mask = Image.new("L", (w, h), 255)
    draw = ImageDraw.Draw(mask)
    draw.ellipse((-w * 0.05, -h * 0.02, w * 1.05, h * 1.02), fill=180)
    mask = mask.filter(ImageFilter.GaussianBlur(50))
    dark = Image.new("RGB", (w, h), (0, 0, 0))
    return Image.composite(img, dark, mask)


def supersample_enhance(src: Image.Image) -> Image.Image:
    """2× supersample for cleaner detail, then fit centered on portrait canvas."""
    big = src.resize((src.width * 2, src.height * 2), Image.Resampling.LANCZOS)
    big = professional_grade(big)
    big = boost_fire_glow(big)
    big = clarity_and_sharpen(big)
    big = subtle_depth_lighting(big)
    big = edge_vignette(big)
    return fit_portrait(big)


def main() -> int:
    src = ImageOps.exif_transpose(Image.open(find_original()))
    print(f"Source: {src.size}")

    enhanced = supersample_enhance(src.convert("RGB"))

    out_png = OUT_DIR / "fenix-both-screens-iphone17.png"
    out_jpg = OUT_DIR / "fenix-both-screens-iphone17.jpg"
    enhanced.save(out_png, "PNG", optimize=True, dpi=(460, 460))
    enhanced.save(out_jpg, "JPEG", quality=94, optimize=True, dpi=(460, 460))

    print(f"Saved: {out_png} ({enhanced.size})")
    print(f"Optimised for lock screen + home screen (anchor 44%, 72% width)")
    print(f"Saved: {out_jpg}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
