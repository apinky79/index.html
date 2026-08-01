#!/usr/bin/env python3
"""
Generate Procreate-ready cushion template PNG layers for sofa and chair
seat/back styles used in upholstery ordering workflows.

Each style produces:
  - outline layer (main shape)
  - detail layer (boxing, channels, welt, seams)
  - label layer (style name + part type)
  - composite (all layers merged for quick import)
"""

from __future__ import annotations

import json
import math
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent / "procreate-cushion-templates"
CANVAS = 2400
MARGIN = 180
STROKE = 6
DETAIL_STROKE = 4

COLORS = {
    "outline": (43, 43, 43, 255),
    "detail": (0, 102, 204, 255),
    "welt": (204, 51, 0, 255),
    "fill": (245, 245, 245, 60),
    "label_bg": (255, 255, 255, 220),
    "label_text": (30, 30, 30, 255),
    "guide": (180, 180, 180, 180),
}


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = [
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    ]
    for path in candidates:
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


FONT_TITLE = load_font(52)
FONT_SUB = load_font(34)
FONT_SMALL = load_font(28)


@dataclass
class TemplateSpec:
    category: str  # sofa | chair
    part: str  # seat | back
    style_id: str
    display_name: str
    description: str
    draw_outline: Callable[[ImageDraw.ImageDraw, tuple[int, int, int, int]], list[tuple]]
    draw_details: Callable[[ImageDraw.ImageDraw, tuple[int, int, int, int]], None]


def bounds() -> tuple[int, int, int, int]:
    return (MARGIN, MARGIN + 120, CANVAS - MARGIN, CANVAS - MARGIN - 80)


def rect(w: float, h: float, cx: float, cy: float) -> tuple[int, int, int, int]:
    return (
        int(cx - w / 2),
        int(cy - h / 2),
        int(cx + w / 2),
        int(cy + h / 2),
    )


def draw_rounded_rect(draw: ImageDraw.ImageDraw, box: tuple, radius: int, **kwargs):
    draw.rounded_rectangle(box, radius=radius, **kwargs)


def dashed_line(draw, p1, p2, color, width=3, dash=18):
    x1, y1 = p1
    x2, y2 = p2
    length = math.hypot(x2 - x1, y2 - y1)
    if length == 0:
        return
    dx, dy = (x2 - x1) / length, (y2 - y1) / length
    pos = 0.0
    draw_on = True
    while pos < length:
        seg = min(dash, length - pos)
        if draw_on:
            sx, sy = x1 + dx * pos, y1 + dy * pos
            ex, ey = x1 + dx * (pos + seg), y1 + dy * (pos + seg)
            draw.line([(sx, sy), (ex, ey)], fill=color, width=width)
        pos += dash
        draw_on = not draw_on


def box_cushion_outline(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    inset = min(w, h) * 0.08
    outer = rect(w, h, cx, cy)
    inner = rect(w - inset * 2, h - inset * 2, cx, cy)
    draw.rectangle(outer, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    draw.rectangle(inner, outline=COLORS["outline"], width=STROKE)
    return [outer, inner]


def box_cushion_details(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    inset = min(w, h) * 0.08
    inner = rect(w - inset * 2, h - inset * 2, cx, cy)
    ix0, iy0, ix1, iy1 = inner
    # boxing strip corners
    for pt in [(ix0, iy0), (ix1, iy0), (ix1, iy1), (ix0, iy1)]:
        draw.ellipse([pt[0] - 8, pt[1] - 8, pt[0] + 8, pt[1] + 8], fill=COLORS["detail"])
    dashed_line(draw, (ix0, iy0), (ix1, iy0), COLORS["welt"], DETAIL_STROKE)
    dashed_line(draw, (ix0, iy1), (ix1, iy1), COLORS["welt"], DETAIL_STROKE)


def knife_edge_outline(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    taper = min(w, h) * 0.06
    pts = [
        (x0 + taper, y0),
        (x1 - taper, y0),
        (x1, y1),
        (x0, y1),
    ]
    draw.polygon(pts, fill=COLORS["fill"], outline=COLORS["outline"])
    draw.line(pts + [pts[0]], fill=COLORS["outline"], width=STROKE)
    return [pts]


def knife_edge_details(draw, b):
    x0, y0, x1, y1 = b
    mid_y = (y0 + y1) / 2
    draw.line([(x0, mid_y), (x1, mid_y)], fill=COLORS["guide"], width=2)
    draw.text((x0 + 20, mid_y + 10), "knife edge seam", fill=COLORS["detail"], font=FONT_SMALL)


def t_cushion_outline(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    arm_w = w * 0.22
    arm_h = h * 0.35
    body_w = w - arm_w
    body = rect(body_w, h, x0 + body_w / 2, (y0 + y1) / 2)
    arm = rect(arm_w, arm_h, x1 - arm_w / 2, y0 + arm_h / 2)
    draw.rectangle(body, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    draw.rectangle(arm, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    return [body, arm]


def t_cushion_details(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    arm_w = w * 0.22
    arm_h = h * 0.35
    join_x = x1 - arm_w
    draw.line([(join_x, y0), (join_x, y0 + arm_h)], fill=COLORS["detail"], width=DETAIL_STROKE)
    draw.text((join_x + 12, y0 + arm_h + 8), "arm wrap", fill=COLORS["detail"], font=FONT_SMALL)


def l_cushion_outline(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    leg = w * 0.28
    main = rect(w - leg, h - leg, x0 + (w - leg) / 2, y0 + (h - leg) / 2 + leg / 2)
    ext = rect(leg, h, x1 - leg / 2, (y0 + y1) / 2)
    draw.rectangle(main, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    draw.rectangle(ext, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    return [main, ext]


def l_cushion_details(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    leg = w * 0.28
    jx = x1 - leg
    jy = y0 + leg
    draw.line([(jx, y0), (jx, jy)], fill=COLORS["detail"], width=DETAIL_STROKE)
    draw.line([(x0, jy), (jx, jy)], fill=COLORS["detail"], width=DETAIL_STROKE)


def bullnose_outline(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    body = rect(w, h * 0.88, cx, y0 + (h * 0.88) / 2 + h * 0.04)
    bx0, by0, bx1, by1 = body
    draw.rectangle(body, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    r = w * 0.45
    draw.arc([cx - r, by1 - r * 0.3, cx + r, by1 + r * 0.7], 0, 180, fill=COLORS["outline"], width=STROKE)
    return [body]


def bullnose_details(draw, b):
    x0, y0, x1, y1 = b
    cx = (x0 + x1) / 2
    w = x1 - x0
    h = y1 - y0
    front_y = y0 + h * 0.92
    draw.text((cx - 120, front_y - 60), "rolled front", fill=COLORS["detail"], font=FONT_SMALL)
    dashed_line(draw, (x0 + 40, front_y), (x1 - 40, front_y), COLORS["welt"], DETAIL_STROKE)


def waterfall_outline(draw, b):
    x0, y0, x1, y1 = b
    pts = [
        (x0, y0 + (y1 - y0) * 0.08),
        (x1, y0),
        (x1, y1),
        (x0, y1),
    ]
    draw.polygon(pts, fill=COLORS["fill"], outline=COLORS["outline"])
    draw.line(pts + [pts[0]], fill=COLORS["outline"], width=STROKE)
    return [pts]


def waterfall_details(draw, b):
    x0, y0, x1, y1 = b
    draw.line([(x0, y0), (x1, y0 + (y1 - y0) * 0.08)], fill=COLORS["detail"], width=DETAIL_STROKE)
    draw.text((x0 + 30, y0 + 20), "continuous slope", fill=COLORS["detail"], font=FONT_SMALL)


def bench_seat_outline(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    bench = rect(w * 0.95, h * 0.55, cx, cy + h * 0.05)
    draw.rectangle(bench, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    return [bench]


def bench_seat_details(draw, b):
    x0, y0, x1, y1 = b
    w = x1 - x0
    cx = (x0 + x1) / 2
    cy = (y0 + y1) / 2 + (y1 - y0) * 0.05
    third = w * 0.95 / 3
    left = cx - w * 0.95 / 2
    for i in range(1, 3):
        x = left + third * i
        draw.line([(x, cy - (y1 - y0) * 0.275), (x, cy + (y1 - y0) * 0.275)], fill=COLORS["guide"], width=2)
    draw.text((x0 + 20, y0 + 10), "3-seat bench (adjust sections)", fill=COLORS["detail"], font=FONT_SMALL)


def chaise_outline(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    main = rect(w * 0.62, h * 0.7, x0 + w * 0.31, y0 + h * 0.55)
    ext = rect(w * 0.55, h * 0.35, x0 + w * 0.75, y0 + h * 0.22)
    draw.rectangle(main, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    draw.rectangle(ext, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    return [main, ext]


def chaise_details(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    jx = x0 + w * 0.62
    draw.line([(jx, y0 + h * 0.2), (jx, y0 + h * 0.7)], fill=COLORS["detail"], width=DETAIL_STROKE)


def channel_back_outline(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    outer = rect(w, h, cx, cy)
    draw.rectangle(outer, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    return [outer]


def channel_back_details(draw, b):
    x0, y0, x1, y1 = b
    channels = 5
    step = (x1 - x0) / (channels + 1)
    for i in range(1, channels + 1):
        x = x0 + step * i
        draw.line([(x, y0 + 20), (x, y1 - 20)], fill=COLORS["detail"], width=DETAIL_STROKE)
    draw.text((x0 + 20, y1 - 70), "channel / tufted", fill=COLORS["detail"], font=FONT_SMALL)


def scatter_back_outline(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    pad = rect(w * 0.55, h * 0.45, cx - w * 0.12, cy - h * 0.05)
    draw.rounded_rectangle(pad, radius=40, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    return [pad]


def scatter_back_details(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    pad = rect(w * 0.55, h * 0.45, cx - w * 0.12, cy - h * 0.05)
    ix0, iy0, ix1, iy1 = pad
    inset = 30
    draw.rounded_rectangle((ix0 + inset, iy0 + inset, ix1 - inset, iy1 - inset), radius=25, outline=COLORS["welt"], width=DETAIL_STROKE)
    draw.text((ix0 + 20, iy0 - 50), "loose scatter", fill=COLORS["detail"], font=FONT_SMALL)


def fixed_back_outline(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    panel = rect(w * 0.92, h, cx, cy)
    draw.rectangle(panel, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    return [panel]


def fixed_back_details(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx = (x0 + x1) / 2
    draw.line([(cx - w * 0.3, y0 + h * 0.15), (cx + w * 0.3, y0 + h * 0.15)], fill=COLORS["guide"], width=2)
    draw.text((x0 + 30, y0 + 20), "fixed upholstered panel", fill=COLORS["detail"], font=FONT_SMALL)


def round_seat_outline(draw, b):
    x0, y0, x1, y1 = b
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    r = min(x1 - x0, y1 - y0) * 0.42
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    return [(cx - r, cy - r, cx + r, cy + r)]


def round_seat_details(draw, b):
    x0, y0, x1, y1 = b
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    r = min(x1 - x0, y1 - y0) * 0.42
    draw.ellipse([cx - r * 0.85, cy - r * 0.85, cx + r * 0.85, cy + r * 0.85], outline=COLORS["welt"], width=DETAIL_STROKE)


def slip_seat_outline(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    top_w = w * 0.75
    bot_w = w * 0.95
    top_y = cy - h * 0.22
    bot_y = cy + h * 0.22
    pts = [
        (cx - top_w / 2, top_y),
        (cx + top_w / 2, top_y),
        (cx + bot_w / 2, bot_y),
        (cx - bot_w / 2, bot_y),
    ]
    draw.polygon(pts, fill=COLORS["fill"], outline=COLORS["outline"])
    draw.line(pts + [pts[0]], fill=COLORS["outline"], width=STROKE)
    return [pts]


def slip_seat_details(draw, b):
    x0, y0, x1, y1 = b
    cx = (x0 + x1) / 2
    cy = (y0 + y1) / 2
    draw.text((cx - 100, cy + (y1 - y0) * 0.28), "drop-in slip seat", fill=COLORS["detail"], font=FONT_SMALL)


def wing_back_outline(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx = (x0 + x1) / 2
    body = rect(w * 0.55, h * 0.85, cx, y0 + h * 0.52)
    bx0, by0, bx1, by1 = body
    draw.rectangle(body, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    # wings
    left_wing = [(bx0 - w * 0.18, by0 + h * 0.1), (bx0, by0), (bx0, by0 + h * 0.35)]
    right_wing = [(bx1, by0), (bx1 + w * 0.18, by0 + h * 0.1), (bx1, by0 + h * 0.35)]
    for wing in (left_wing, right_wing):
        draw.polygon(wing, fill=COLORS["fill"], outline=COLORS["outline"])
        draw.line(wing + [wing[0]], fill=COLORS["outline"], width=STROKE)
    return [body]


def wing_back_details(draw, b):
    x0, y0, x1, y1 = b
    w = x1 - x0
    cx = (x0 + x1) / 2
    draw.text((cx - 50, y0 + 10), "wing back", fill=COLORS["detail"], font=FONT_SMALL)
    draw.line([(cx - w * 0.1, y0 + (y1 - y0) * 0.2), (cx + w * 0.1, y0 + (y1 - y0) * 0.2)], fill=COLORS["guide"], width=2)


def round_back_outline(draw, b):
    x0, y0, x1, y1 = b
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2 + (y1 - y0) * 0.05
    rw = (x1 - x0) * 0.42
    rh = (y1 - y0) * 0.48
    draw.rounded_rectangle([cx - rw, cy - rh, cx + rw, cy + rh], radius=80, fill=COLORS["fill"], outline=COLORS["outline"], width=STROKE)
    return [(cx - rw, cy - rh, cx + rw, cy + rh)]


def round_back_details(draw, b):
    x0, y0, x1, y1 = b
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    draw.text((cx - 90, cy + (y1 - y0) * 0.3), "round / oval back", fill=COLORS["detail"], font=FONT_SMALL)


def bordered_back_details(draw, b):
    x0, y0, x1, y1 = b
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    outer = rect(w * 0.88, h * 0.92, cx, cy)
    ix0, iy0, ix1, iy1 = outer
    inset = 35
    draw.rectangle((ix0 + inset, iy0 + inset, ix1 - inset, iy1 - inset), outline=COLORS["welt"], width=DETAIL_STROKE)
    draw.text((ix0 + 20, iy0 - 50), "bordered / welted edge", fill=COLORS["detail"], font=FONT_SMALL)


TEMPLATES: list[TemplateSpec] = [
    # SOFA SEATS
    TemplateSpec("sofa", "seat", "box", "Box Cushion Seat", "Boxed edge with welt/boxing strip", box_cushion_outline, box_cushion_details),
    TemplateSpec("sofa", "seat", "knife-edge", "Knife Edge Seat", "Tapered seam, no boxing", knife_edge_outline, knife_edge_details),
    TemplateSpec("sofa", "seat", "t-cushion", "T-Cushion Seat", "Wraps around front arm", t_cushion_outline, t_cushion_details),
    TemplateSpec("sofa", "seat", "l-cushion", "L-Cushion Seat", "Corner sectional wrap", l_cushion_outline, l_cushion_details),
    TemplateSpec("sofa", "seat", "bullnose", "Bullnose Seat", "Rolled front edge", bullnose_outline, bullnose_details),
    TemplateSpec("sofa", "seat", "waterfall", "Waterfall Seat", "Continuous front-to-back slope", waterfall_outline, waterfall_details),
    TemplateSpec("sofa", "seat", "bench", "Bench Seat", "Single long seat cushion", bench_seat_outline, bench_seat_details),
    TemplateSpec("sofa", "seat", "chaise", "Chaise Extension Seat", "Extended leg-rest section", chaise_outline, chaise_details),
    # SOFA BACKS
    TemplateSpec("sofa", "back", "box", "Box Cushion Back", "Boxed back cushion", box_cushion_outline, box_cushion_details),
    TemplateSpec("sofa", "back", "knife-edge", "Knife Edge Back", "Tapered back cushion", knife_edge_outline, knife_edge_details),
    TemplateSpec("sofa", "back", "t-back", "T-Back Cushion", "Wraps around arm at back", t_cushion_outline, t_cushion_details),
    TemplateSpec("sofa", "back", "l-back", "L-Back Cushion", "Corner back wrap", l_cushion_outline, l_cushion_details),
    TemplateSpec("sofa", "back", "bullnose", "Bullnose Back", "Rolled top/front back", bullnose_outline, bullnose_details),
    TemplateSpec("sofa", "back", "channel-tufted", "Channel / Tufted Back", "Vertical channel lines", channel_back_outline, channel_back_details),
    TemplateSpec("sofa", "back", "scatter", "Loose Scatter Back", "Individual throw-back pillow", scatter_back_outline, scatter_back_details),
    TemplateSpec("sofa", "back", "fixed", "Fixed Upholstered Back", "Non-removable back panel", fixed_back_outline, fixed_back_details),
    TemplateSpec("sofa", "back", "bordered", "Bordered / Welted Back", "Back with welt border", box_cushion_outline, bordered_back_details),
    # CHAIR SEATS
    TemplateSpec("chair", "seat", "box", "Box Cushion Seat", "Boxed dining/lounge seat", box_cushion_outline, box_cushion_details),
    TemplateSpec("chair", "seat", "knife-edge", "Knife Edge Seat", "Simple tapered seat", knife_edge_outline, knife_edge_details),
    TemplateSpec("chair", "seat", "round", "Round Seat", "Circular dining seat", round_seat_outline, round_seat_details),
    TemplateSpec("chair", "seat", "slip", "Slip / Drop-In Seat", "Trapezoid drop-in pad", slip_seat_outline, slip_seat_details),
    TemplateSpec("chair", "seat", "t-cushion", "T-Cushion Seat", "Armchair T-wrap seat", t_cushion_outline, t_cushion_details),
    # CHAIR BACKS
    TemplateSpec("chair", "back", "box", "Box Cushion Back", "Boxed chair back", box_cushion_outline, box_cushion_details),
    TemplateSpec("chair", "back", "knife-edge", "Knife Edge Back", "Tapered chair back", knife_edge_outline, knife_edge_details),
    TemplateSpec("chair", "back", "scatter", "Loose Back Cushion", "Removable back pillow", scatter_back_outline, scatter_back_details),
    TemplateSpec("chair", "back", "wing", "Wing Back", "Wing chair silhouette", wing_back_outline, wing_back_details),
    TemplateSpec("chair", "back", "round", "Round / Oval Back", "Curved top chair back", round_back_outline, round_back_details),
    TemplateSpec("chair", "back", "fixed", "Fixed Upholstered Back", "Fixed chair back panel", fixed_back_outline, fixed_back_details),
]


def render_layer(spec: TemplateSpec, layer: str) -> Image.Image:
    img = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    b = bounds()

    if layer == "outline":
        spec.draw_outline(draw, b)
    elif layer == "detail":
        spec.draw_details(draw, b)
    elif layer == "label":
        title = f"{spec.display_name}"
        subtitle = f"{spec.category.title()} · {spec.part.title()} · {spec.style_id}"
        desc = spec.description
        pad = 24
        tw = max(
            draw.textlength(title, font=FONT_TITLE),
            draw.textlength(subtitle, font=FONT_SUB),
            draw.textlength(desc, font=FONT_SMALL),
        )
        box_h = 170
        draw.rounded_rectangle(
            [MARGIN, 30, MARGIN + tw + pad * 2, 30 + box_h],
            radius=16,
            fill=COLORS["label_bg"],
            outline=COLORS["outline"],
            width=2,
        )
        draw.text((MARGIN + pad, 48), title, fill=COLORS["label_text"], font=FONT_TITLE)
        draw.text((MARGIN + pad, 108), subtitle, fill=COLORS["detail"], font=FONT_SUB)
        draw.text((MARGIN + pad, 152), desc, fill=COLORS["label_text"], font=FONT_SMALL)
        # bottom scale note
        note = "Template layer — scale to your measurements in Procreate"
        nw = draw.textlength(note, font=FONT_SMALL)
        draw.rounded_rectangle(
            [CANVAS // 2 - nw // 2 - 16, CANVAS - 70, CANVAS // 2 + nw // 2 + 16, CANVAS - 24],
            radius=10,
            fill=COLORS["label_bg"],
        )
        draw.text((CANVAS // 2 - nw // 2, CANVAS - 62), note, fill=COLORS["label_text"], font=FONT_SMALL)
    return img


def composite_layers(*layers: Image.Image) -> Image.Image:
    base = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    for layer in layers:
        base = Image.alpha_composite(base, layer)
    return base


def save_template(spec: TemplateSpec) -> dict:
    base_dir = ROOT / spec.category / spec.part / spec.style_id
    layers_dir = base_dir / "layers"
    layers_dir.mkdir(parents=True, exist_ok=True)

    outline = render_layer(spec, "outline")
    detail = render_layer(spec, "detail")
    label = render_layer(spec, "label")
    composite = composite_layers(outline, detail, label)

    layer_files = {
        "01-outline": outline,
        "02-detail": detail,
        "03-label": label,
    }
    paths = {}
    for name, img in layer_files.items():
        p = layers_dir / f"{name}.png"
        img.save(p, "PNG")
        paths[name] = str(p.relative_to(ROOT))

    comp_path = base_dir / f"{spec.style_id}-composite.png"
    composite.save(comp_path, "PNG")
    paths["composite"] = str(comp_path.relative_to(ROOT))
    return paths


def write_manifest(all_paths: dict) -> None:
    manifest = {
        "canvas_size_px": CANVAS,
        "format": "PNG RGBA",
        "recommended_import": "Import layer PNGs into Procreate; stack outline → detail → label",
        "layer_key": {
            "01-outline": "Main cushion shape (black)",
            "02-detail": "Boxing, channels, welt, seams (blue/red)",
            "03-label": "Style name and notes",
        },
        "templates": all_paths,
    }
    (ROOT / "manifest.json").write_text(json.dumps(manifest, indent=2))


def write_readme() -> None:
    readme = """# Procreate Cushion Templates

Labeled layer templates for **sofa and chair cushion seats and backs**, ready to import into Procreate for client orders and workshop specs.

## What's included

| Category | Part | Styles |
|----------|------|--------|
| **Sofa** | Seat | box, knife-edge, t-cushion, l-cushion, bullnose, waterfall, bench, chaise |
| **Sofa** | Back | box, knife-edge, t-back, l-back, bullnose, channel-tufted, scatter, fixed, bordered |
| **Chair** | Seat | box, knife-edge, round, slip, t-cushion |
| **Chair** | Back | box, knife-edge, scatter, wing, round, fixed |

Each style folder contains:

```
{category}/{part}/{style-id}/
├── layers/
│   ├── 01-outline.png   ← main shape
│   ├── 02-detail.png    ← boxing, welt, channels, notes
│   └── 03-label.png     ← style name label
└── {style-id}-composite.png   ← all layers merged (quick import)
```

## Procreate workflow

1. **Sync this folder to your cloud** (iCloud, Dropbox, Google Drive, etc.).
2. In Procreate: **Actions → Add → Insert a file** (or drag from Files app).
3. For full control, import the three files from `layers/` as separate layers (bottom to top: outline → detail → label).
4. For speed, import the `-composite.png` as a single reference layer.
5. **Transform → Freeform** to scale the template to your measured width/depth.
6. Draw fabric/piping notes on new layers above the template.
7. Export or save the canvas to your order folder.

## Layer colour key

| Colour | Meaning |
|--------|---------|
| Dark grey | Cut outline / main shape |
| Blue | Construction detail (boxing, channels, arm wrap) |
| Red dashed | Welt / piping line |
| Light grey fill | Cushion face (20% opacity) |

## Canvas

All templates are **2400 × 2400 px** with transparent backgrounds — high enough resolution for iPad Procreate without bloating file size.

## Regenerating

```bash
python3 scripts/generate_cushion_templates.py
```

## File naming for orders

Suggested Procreate layer names when saving:

`{client}-{piece}-sofa-seat-box`  
`{client}-{piece}-chair-back-wing`

Keep the style id from the folder name for consistency with your supplier vocabulary.
"""
    (ROOT / "README.md").write_text(readme)


def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    all_paths = {}
    for spec in TEMPLATES:
        key = f"{spec.category}/{spec.part}/{spec.style_id}"
        all_paths[key] = {
            "display_name": spec.display_name,
            "description": spec.description,
            "files": save_template(spec),
        }
        print(f"Generated {key}")

    write_manifest(all_paths)
    write_readme()
    print(f"\nDone — {len(TEMPLATES)} styles → {ROOT}")


if __name__ == "__main__":
    main()
