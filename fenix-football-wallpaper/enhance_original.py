#!/usr/bin/env python3
"""Enhance the user's original Fenix Football image for iPhone 17 Pro Max."""

from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageOps

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


def luminance_alpha(rgba: Image.Image, threshold: int = 18) -> Image.Image:
    """Build a soft alpha matte from dark-background artwork."""
    rgb = np.array(rgba.convert("RGB"), dtype=np.float32)
    lum = 0.2126 * rgb[:, :, 0] + 0.7152 * rgb[:, :, 1] + 0.0722 * rgb[:, :, 2]
    alpha = np.clip((lum - threshold) * 4.0, 0, 255).astype(np.uint8)
    alpha = Image.fromarray(alpha, mode="L").filter(ImageFilter.GaussianBlur(1.2))
    return alpha


def perspective_tilt(img: Image.Image, strength: float = 0.028) -> Image.Image:
    """Subtle 3D tilt — bottom edge closer to viewer."""
    w, h = img.size
    dx = int(w * strength)
    dy = int(h * strength * 0.35)
    # Narrower at top, wider at bottom.
    quad = (dx, dy, w - dx, dy, w, h, 0, h)
    return img.transform(
        (w, h),
        Image.Transform.QUAD,
        quad,
        resample=Image.Resampling.BICUBIC,
    )


def drop_shadow(layer: Image.Image, offset: tuple[int, int], blur: int, opacity: int) -> Image.Image:
    alpha = layer.split()[-1]
    shadow = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    shadow_alpha = alpha.filter(ImageFilter.GaussianBlur(blur))
    shadow_alpha = ImageEnhance.Brightness(shadow_alpha).enhance(opacity / 255)
    shadow.putalpha(shadow_alpha)
    canvas = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    canvas.alpha_composite(shadow, offset)
    return canvas


def floor_reflection(layer: Image.Image, fade: float = 0.28) -> Image.Image:
    """Soft mirror reflection beneath the graphic."""
    w, h = layer.size
    reflected = ImageOps.flip(layer)
    mask = Image.new("L", (w, h), 0)
    draw = ImageDraw.Draw(mask)
    for y in range(h):
        t = y / max(h - 1, 1)
        draw.line([(0, y), (w, y)], fill=int(255 * fade * (1 - t**1.6)))
    reflected.putalpha(ImageChops.multiply(reflected.split()[-1], mask))
    return reflected.filter(ImageFilter.GaussianBlur(2.5))


def rim_light(layer: Image.Image) -> Image.Image:
    """Warm edge highlight for pop-out depth."""
    rgb = layer.convert("RGB")
    edges = rgb.filter(ImageFilter.FIND_EDGES).convert("L")
    edges = ImageEnhance.Contrast(edges).enhance(2.2)
    edges = edges.filter(ImageFilter.GaussianBlur(1.5))
    warm = Image.new("RGBA", layer.size, (255, 170, 60, 0))
    warm.putalpha(edges.point(lambda p: min(int(p * 0.55), 110)))
    return Image.alpha_composite(layer, warm)


def depth_glow(width: int, height: int, cx: int, cy: int) -> Image.Image:
    """Radial light pool under the artwork."""
    glow = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(glow)
    for i, alpha in enumerate(range(90, 0, -6)):
        r = 120 + i * 38
        color = (255, 120, 30, alpha)
        draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=color)
    return glow.filter(ImageFilter.GaussianBlur(18))


def local_relief(layer: Image.Image) -> Image.Image:
    """Subtle high-pass relief to add surface depth without changing identity."""
    rgb = layer.convert("RGB")
    blur = rgb.filter(ImageFilter.GaussianBlur(3))
    detail = ImageChops.subtract(rgb, blur)
    detail = ImageEnhance.Contrast(detail).enhance(1.8)
    relief = Image.new("RGBA", layer.size, (255, 220, 180, 0))
    relief.putalpha(detail.convert("L").point(lambda p: min(int(p * 0.35), 70)))
    return Image.alpha_composite(layer, relief)


def apply_depth_3d(scaled: Image.Image) -> Image.Image:
    """Turn flat artwork into a floating 3D card while preserving the original photo."""
    rgba = scaled.convert("RGBA")
    alpha = luminance_alpha(rgba)
    rgba.putalpha(alpha)

    tilted = perspective_tilt(rgba)
    lit = rim_light(local_relief(tilted))

    w, h = lit.size
    pad = int(max(w, h) * 0.18)
    stage = Image.new("RGBA", (w + pad * 2, h + pad * 2), (0, 0, 0, 0))

    cx, cy = (w + pad * 2) // 2, h + pad - int(h * 0.04)
    stage.alpha_composite(depth_glow(stage.width, stage.height, cx, cy))

    # Layered shadows = depth.
    for blur, offset, opacity in ((36, (0, 22), 130), (20, (0, 12), 95), (10, (0, 5), 65)):
        stage.alpha_composite(drop_shadow(lit, offset, blur, opacity), (pad, pad))

    reflection = floor_reflection(lit, fade=0.16)
    stage.alpha_composite(reflection, (pad, pad + h + int(h * 0.015)))

    stage.alpha_composite(lit, (pad, pad))

    # Inner vignette on the card for volume.
    vignette = Image.new("L", (w, h), 255)
    draw = ImageDraw.Draw(vignette)
    draw.ellipse((-w * 0.08, -h * 0.08, w * 1.08, h * 1.08), fill=0)
    vignette = vignette.filter(ImageFilter.GaussianBlur(28))
    dark = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dark.putalpha(vignette.point(lambda p: int((255 - p) * 0.35)))
    card = stage.crop((pad, pad, pad + w, pad + h))
    card = Image.alpha_composite(card, dark)
    stage.paste(card, (pad, pad), card)

    return stage


def build_portrait_canvas(src: Image.Image) -> Image.Image:
    """Center the 3D-enhanced graphic on a portrait canvas sized for Pro Max."""
    src = src.convert("RGBA")
    sw, sh = src.size

    max_w = int(TARGET_W * 0.96)
    max_h = int(TARGET_H * 0.86)
    scale = min(max_w / sw, max_h / sh)
    new_size = (max(1, int(sw * scale)), max(1, int(sh * scale)))
    scaled = src.resize(new_size, Image.Resampling.LANCZOS)

    depth_layer = apply_depth_3d(scaled)

    rgb_src = src.convert("RGB")
    corner = rgb_src.getpixel((min(10, sw - 1), min(10, sh - 1)))
    if sum(corner) > 40:
        corner = (0, 0, 0)
    canvas = Image.new("RGBA", (TARGET_W, TARGET_H), corner + (255,))

    # Subtle atmospheric glow behind the card only.
    atm = Image.new("RGBA", (TARGET_W, TARGET_H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(atm)
    glow_y = int(TARGET_H * 0.55)
    draw.ellipse(
        (TARGET_W // 2 - 420, glow_y - 180, TARGET_W // 2 + 420, glow_y + 320),
        fill=(255, 100, 25, 45),
    )
    atm = atm.filter(ImageFilter.GaussianBlur(35))
    canvas = Image.alpha_composite(canvas, atm)

    x = (TARGET_W - depth_layer.width) // 2
    y = int(TARGET_H * 0.08) + (max_h - depth_layer.height) // 2
    canvas.alpha_composite(depth_layer, (x, y))
    return canvas.convert("RGB")


def enhance(img: Image.Image) -> Image.Image:
    img = ImageEnhance.Contrast(img).enhance(1.1)
    img = ImageEnhance.Color(img).enhance(1.08)
    img = ImageEnhance.Brightness(img).enhance(1.02)
    img = ImageEnhance.Sharpness(img).enhance(1.25)
    img = img.filter(ImageFilter.UnsharpMask(radius=1.4, percent=125, threshold=2))
    return img


def main() -> int:
    src_path = find_original()
    print(f"Using source: {src_path}")

    original = Image.open(src_path)
    original = ImageOps.exif_transpose(original)

    w, h = original.size
    if w / h > 0.55:
        work = build_portrait_canvas(original)
    else:
        work = original.convert("RGB")
        work = ImageOps.fit(
            work, (TARGET_W, TARGET_H), method=Image.Resampling.LANCZOS, centering=(0.5, 0.45)
        )

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
