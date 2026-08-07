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
    rgb = np.array(rgba.convert("RGB"), dtype=np.float32)
    lum = 0.2126 * rgb[:, :, 0] + 0.7152 * rgb[:, :, 1] + 0.0722 * rgb[:, :, 2]
    alpha = np.clip((lum - threshold) * 4.0, 0, 255).astype(np.uint8)
    return Image.fromarray(alpha, mode="L").filter(ImageFilter.GaussianBlur(1.2))


def perspective_tilt(img: Image.Image, strength: float = 0.055) -> Image.Image:
    """Strong tilt — bottom edge leaps toward the viewer."""
    w, h = img.size
    dx = int(w * strength)
    dy = int(h * strength * 0.4)
    quad = (dx, dy, w - dx, dy, w + dx // 2, h, -dx // 2, h)
    return img.transform((w + dx, h), Image.Transform.QUAD, quad, resample=Image.Resampling.BICUBIC)


def drop_shadow(layer: Image.Image, offset: tuple[int, int], blur: int, opacity: int) -> Image.Image:
    alpha = layer.split()[-1]
    shadow = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    shadow_alpha = alpha.filter(ImageFilter.GaussianBlur(blur))
    shadow_alpha = ImageEnhance.Brightness(shadow_alpha).enhance(opacity / 255)
    shadow.putalpha(shadow_alpha)
    canvas = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    canvas.alpha_composite(shadow, offset)
    return canvas


def rim_light(layer: Image.Image, strength: float = 0.55) -> Image.Image:
    rgb = layer.convert("RGB")
    edges = rgb.filter(ImageFilter.FIND_EDGES).convert("L")
    edges = ImageEnhance.Contrast(edges).enhance(2.4)
    edges = edges.filter(ImageFilter.GaussianBlur(1.2))
    warm = Image.new("RGBA", layer.size, (255, 200, 90, 0))
    warm.putalpha(edges.point(lambda p: min(int(p * strength), 140)))
    return Image.alpha_composite(layer, warm)


def foreground_mask(w: int, h: int) -> Image.Image:
    """Mask the running player + lower fire — the part that breaks out of the screen."""
    mask = Image.new("L", (w, h), 0)
    draw = ImageDraw.Draw(mask)
    # Torso, legs, and embers at the bottom of the circle.
    draw.ellipse((w * 0.14, h * 0.38, w * 0.86, h * 0.98), fill=255)
    draw.ellipse((w * 0.28, h * 0.48, w * 0.72, h * 1.02), fill=255)
    return mask.filter(ImageFilter.GaussianBlur(10))


def burst_mask(w: int, h: int) -> Image.Image:
    """Fiery wings + sparks that spill forward."""
    mask = Image.new("L", (w, h), 0)
    draw = ImageDraw.Draw(mask)
    draw.ellipse((w * 0.02, h * 0.30, w * 0.98, h * 0.82), fill=200)
    draw.ellipse((w * 0.18, h * 0.52, w * 0.82, h * 0.95), fill=255)
    return mask.filter(ImageFilter.GaussianBlur(14))


def scale_from_bottom_center(layer: Image.Image, scale: float) -> tuple[Image.Image, tuple[int, int]]:
    w, h = layer.size
    nw, nh = max(1, int(w * scale)), max(1, int(h * scale))
    scaled = layer.resize((nw, nh), Image.Resampling.LANCZOS)
    ox = (w - nw) // 2
    oy = h - nh
    return scaled, (ox, oy)


def phone_screen_vignette(width: int, height: int) -> Image.Image:
    """Dark phone bezels — content breaks out through the center."""
    vignette = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(vignette)
    for i, alpha in enumerate(range(0, 200, 8)):
        inset = i * 3
        draw.rectangle(
            (inset, inset, width - inset, height - inset),
            outline=(0, 0, 0, alpha),
            width=8,
        )
    # Stronger side and bottom bezels (phone in hand).
    grad = Image.new("L", (width, height), 0)
    gdraw = ImageDraw.Draw(grad)
    for x in range(width):
        t = min(x, width - x) / (width * 0.22)
        t = max(0.0, min(1.0, t))
        gdraw.line([(x, 0), (x, height)], fill=int(255 * (1 - t**1.3)))
    for y in range(height):
        t = (height - y) / (height * 0.18)
        t = max(0.0, min(1.0, t))
        row = grad.crop((0, y, width, y + 1))
        boosted = row.point(lambda p: max(p, int(255 * (1 - t**1.1) * 0.85)))
        grad.paste(boosted, (0, y))
    vignette.putalpha(grad.filter(ImageFilter.GaussianBlur(22)))
    return vignette


def screen_surface_glow(width: int, height: int, cx: int, cy: int) -> Image.Image:
    glow = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(glow)
    draw.ellipse((cx - 520, cy - 120, cx + 520, cy + 280), fill=(255, 110, 25, 70))
    draw.ellipse((cx - 300, cy + 40, cx + 300, cy + 220), fill=(255, 60, 10, 90))
    return glow.filter(ImageFilter.GaussianBlur(28))


def apply_breakout_3d(scaled: Image.Image) -> Image.Image:
    """Make the player burst out of the phone screen toward the viewer."""
    rgba = scaled.convert("RGBA")
    alpha = luminance_alpha(rgba)
    rgba.putalpha(alpha)

    w, h = rgba.size
    pad = int(max(w, h) * 0.28)
    stage_w, stage_h = w + pad * 2, h + pad * 2 + int(h * 0.14)
    stage = Image.new("RGBA", (stage_w, stage_h), (0, 0, 0, 0))

    # --- Layer 1: recessed "inside the screen" artwork ---
    recessed = rgba.copy()
    recessed.putalpha(alpha)
    recessed = ImageEnhance.Brightness(recessed).enhance(0.88)
    recessed_rgb = recessed.convert("RGB").filter(ImageFilter.GaussianBlur(1.6))
    recessed = Image.merge("RGBA", (*recessed_rgb.split(), recessed.split()[-1]))
    recessed = perspective_tilt(recessed, strength=0.022)
    rw, rh = recessed.size
    rx = (stage_w - rw) // 2
    ry = pad + int(h * 0.02)
    stage.alpha_composite(recessed, (rx, ry))

    # Screen-plane shadow (cast onto the phone surface behind the pop-out figure).
    screen_shadow = Image.new("RGBA", (stage_w, stage_h), (0, 0, 0, 0))
    sdraw = ImageDraw.Draw(screen_shadow)
    sx = stage_w // 2
    sy = ry + rh - int(rh * 0.08)
    for i, a in enumerate(range(120, 0, -5)):
        rx_e = 80 + i * 16
        ry_e = 24 + i * 7
        sdraw.ellipse((sx - rx_e, sy - ry_e, sx + rx_e, sy + ry_e), fill=(0, 0, 0, a))
    stage.alpha_composite(screen_shadow.filter(ImageFilter.GaussianBlur(10)))

    # --- Layer 2: fire burst popping forward ---
    burst = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    bmask = burst_mask(w, h)
    burst.paste(rgba, (0, 0), ImageChops.multiply(alpha, bmask))
    burst_big, (box, boy) = scale_from_bottom_center(burst, 1.14)
    bx = pad + box
    by = pad + boy + int(h * 0.04)
    for blur, off, op in ((28, (0, 16), 120), (14, (0, 8), 80)):
        stage.alpha_composite(drop_shadow(burst_big, off, blur, op), (bx, by))
    stage.alpha_composite(rim_light(burst_big, 0.45), (bx, by))

    # --- Layer 3: running player breaking OUT toward viewer ---
    fg = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    fmask = foreground_mask(w, h)
    fg.paste(rgba, (0, 0), ImageChops.multiply(alpha, fmask))
    fg_big, (fox, foy) = scale_from_bottom_center(fg, 1.38)
    fg_big = rim_light(fg_big, 0.75)
    fg_big = ImageEnhance.Contrast(fg_big).enhance(1.12)
    fg_big = ImageEnhance.Sharpness(fg_big).enhance(1.35)
    fx = pad + fox
    fy = pad + foy + int(h * 0.14)

    # Deep contact shadow where figure lifts off the screen.
    for blur, off, op in ((48, (0, 28), 170), (24, (0, 14), 120), (10, (0, 4), 90)):
        stage.alpha_composite(drop_shadow(fg_big, off, blur, op), (fx, fy))

    stage.alpha_composite(fg_big, (fx, fy))

    return stage


def build_portrait_canvas(src: Image.Image) -> Image.Image:
    src = src.convert("RGBA")
    sw, sh = src.size

    max_w = int(TARGET_W * 0.98)
    max_h = int(TARGET_H * 0.82)
    scale = min(max_w / sw, max_h / sh)
    new_size = (max(1, int(sw * scale)), max(1, int(sh * scale)))
    scaled = src.resize(new_size, Image.Resampling.LANCZOS)

    breakout = apply_breakout_3d(scaled)

    canvas = Image.new("RGBA", (TARGET_W, TARGET_H), (0, 0, 0, 255))

    # Light pool on the phone "screen" behind the breakout.
    glow_y = int(TARGET_H * 0.52)
    canvas.alpha_composite(screen_surface_glow(TARGET_W, TARGET_H, TARGET_W // 2, glow_y))

    # Place lower on screen so the figure emerges toward the viewer's hands.
    x = (TARGET_W - breakout.width) // 2
    y = int(TARGET_H * 0.18) + (max_h - breakout.height) // 2
    canvas.alpha_composite(breakout, (x, y))

    # Phone bezel — frame the screen, subject breaks through center.
    canvas = Image.alpha_composite(canvas, phone_screen_vignette(TARGET_W, TARGET_H))
    return canvas.convert("RGB")


def enhance(img: Image.Image) -> Image.Image:
    img = ImageEnhance.Contrast(img).enhance(1.12)
    img = ImageEnhance.Color(img).enhance(1.1)
    img = ImageEnhance.Brightness(img).enhance(1.03)
    img = ImageEnhance.Sharpness(img).enhance(1.3)
    img = img.filter(ImageFilter.UnsharpMask(radius=1.6, percent=135, threshold=2))
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
