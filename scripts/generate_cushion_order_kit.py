#!/usr/bin/env python3
"""
Cushion order kit: Dudgeon-style purchase order + editable SVG cushion drawings.

Outputs:
  - Fillable PDF (form fields for To, From, order no, design, qty, notes)
  - PNG form background (Procreate locked layer)
  - SVG form (Affinity Designer / Concepts / Illustrator)
  - SVG cushion drawings with separate groups per outline + dimension line + label
"""

from __future__ import annotations

import json
import math
import textwrap
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable
from xml.etree.ElementTree import Element, SubElement, tostring

from PIL import Image, ImageDraw, ImageFont
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.pdfgen import canvas as pdf_canvas

ROOT = Path(__file__).resolve().parent.parent / "cushion-order-kit"
FORM_W, FORM_H = 2480, 3508  # A4 @ 300dpi

# --- Form layout constants (shared PDF / PNG / SVG) ---

COMPANY = "DUDGEON"
SUBTITLE = "London Sofamakers"
ADDRESS = "Unit 9A, Chiswick Studios, 9 Power Road, London W4 5PY"
TITLE = "CUSHION PURCHASE ORDER"

FILLING_OPTIONS = ["Feather/Down", "Fibre/Feather", "Fibre", "Foam", "Wrap", "Other"]
BORDER_OPTIONS = ['2.5"', '3.75"', '4.25"', '5.25"', '6.25"']
SIZE_OPTIONS = [
    "Outer case finished size, With barrier",
    "Outer case finished size, No barrier",
    "Inner case finished size, With barrier",
    "Inner case finished size, No barrier",
    "Foam size",
]
ITEM_OPTIONS = ["sofa", "chair", "other"]


def load_font(size: int, bold: bool = False):
    names = (
        ["DejaVuSans-Bold.ttf", "LiberationSans-Bold.ttf"]
        if bold
        else ["DejaVuSans.ttf", "LiberationSans.ttf"]
    )
    for name in names:
        for base in ["/usr/share/fonts/truetype/dejavu", "/usr/share/fonts/truetype/liberation"]:
            p = Path(base) / name
            if p.exists():
                return ImageFont.truetype(str(p), size)
    return ImageFont.load_default()


# --- Cushion geometry (inches, centred at 0,0) ---

@dataclass
class DimSpec:
    id: str
    x1: float
    y1: float
    x2: float
    y2: float
    label: str
    lx: float
    ly: float
    ext1: tuple[float, float] | None = None
    ext2: tuple[float, float] | None = None


@dataclass
class CushionStyle:
    category: str
    part: str
    style_id: str
    name: str
    points_fn: Callable[[], list[tuple[float, float]]]
    dims: list[DimSpec] = field(default_factory=list)


def t_seat_pts():
    hw_back, hw_front, depth, wrap = 10, 16, 28, 6.5
    y0, y_wrap, y1 = -depth / 2, -depth / 2 + wrap, depth / 2
    return [
        (-hw_back, y0), (hw_back, y0), (hw_back, y_wrap), (hw_front, y_wrap),
        (hw_front, y1), (-hw_front, y1), (-hw_front, y_wrap), (-hw_back, y_wrap),
    ]


T_SEAT_DIMS = [
    DimSpec("back-width", -10, -18, 10, -18, '20"', 0, -22, (-10, -14), (10, -14)),
    DimSpec("front-width", -16, 18, 16, 18, '32"', 0, 24, (-16, 14), (16, 14)),
    DimSpec("depth", 20, -14, 20, 14, '28"', 28, 0, (16, -14), (16, 14)),
    DimSpec("wrap-depth", -22, -14, -22, -1.5, '6.5"', -30, -8, (-16, -14), (-16, -1.5)),
]


def t_back_pts():
    hw_top, hw_bot, total_h, wing_h = 14, 10, 23, 13
    y0, y_wing, y1 = -total_h / 2, -total_h / 2 + wing_h, total_h / 2
    return [
        (-hw_top, y0), (hw_top, y0), (hw_top, y_wing), (hw_bot, y_wing),
        (hw_bot, y1), (-hw_bot, y1), (-hw_bot, y_wing), (-hw_top, y_wing),
    ]


T_BACK_DIMS = [
    DimSpec("top-width", -14, -16, 14, -16, '28"', 0, -22, (-14, -11.5), (14, -11.5)),
    DimSpec("bottom-width", -10, 15, 10, 15, '20"', 0, 22, (-10, 11.5), (10, 11.5)),
    DimSpec("wing-height", 18, -11.5, 18, 0, '13"', 30, -6, (14, -11.5), (10, 0)),
    DimSpec("total-height", -20, -11.5, -20, 11.5, '23"', -36, 0, (-14, -11.5), (-10, 11.5)),
]


def box_pts():
    return [(-12, -11), (12, -11), (12, 11), (-12, 11)]


BOX_DIMS = [
    DimSpec("width", -12, -15, 12, -15, '24"', 0, -19, (-12, -11), (12, -11)),
    DimSpec("depth", 17, -11, 17, 11, '22"', 24, 0, (12, -11), (12, 11)),
]


def l_seat_pts():
    return [(-9, -11), (9, -11), (9, 0), (16, 0), (16, 11), (-9, 11)]


L_DIMS = [
    DimSpec("main-width", -9, -15, 9, -15, '18"', 0, -19, (-9, -11), (9, -11)),
    DimSpec("main-depth", -13, -11, -13, 11, '22"', -20, 0, (-9, -11), (-9, 11)),
    DimSpec("leg-width", 9, 15, 16, 15, '14"', 12, 19, (9, 11), (16, 11)),
]


def round_pts(n=48, r=11):
    return [(r * math.cos(2 * math.pi * i / n), r * math.sin(2 * math.pi * i / n)) for i in range(n)]


ROUND_DIMS = [
    DimSpec("diameter-h", -11, 14, 11, 14, 'Ø 22"', 0, 18),
    DimSpec("diameter-v", 0, -11, 0, 11, '22"', 18, 0),
]


STYLES: list[CushionStyle] = [
    CushionStyle("chair", "seat", "t-cushion", "T-Cushion Seat", t_seat_pts, T_SEAT_DIMS),
    CushionStyle("chair", "back", "t-back", "T-Back Cushion", t_back_pts, T_BACK_DIMS),
    CushionStyle("chair", "seat", "box", "Box Cushion Seat", box_pts, BOX_DIMS),
    CushionStyle("chair", "back", "box", "Box Cushion Back", box_pts, BOX_DIMS),
    CushionStyle("sofa", "seat", "t-cushion", "T-Cushion Seat", t_seat_pts, T_SEAT_DIMS),
    CushionStyle("sofa", "back", "t-back", "T-Back Cushion", t_back_pts, T_BACK_DIMS),
    CushionStyle("sofa", "seat", "box", "Box Cushion Seat", box_pts, BOX_DIMS),
    CushionStyle("sofa", "back", "box", "Box Cushion Back", box_pts, BOX_DIMS),
    CushionStyle("sofa", "seat", "l-cushion", "L-Cushion Seat", l_seat_pts, L_DIMS),
    CushionStyle("sofa", "back", "l-back", "L-Back Cushion", l_seat_pts, L_DIMS),
    CushionStyle("chair", "seat", "round", "Round Seat", round_pts, ROUND_DIMS),
]


# --- SVG helpers ---

def svg_el(tag: str, **attrs) -> Element:
    el = Element(tag)
    for k, v in attrs.items():
        if v is not None:
            el.set(k.replace("_", "-"), str(v))
    return el


def svg_line(parent: Element, x1, y1, x2, y2, **attrs):
    SubElement(parent, "line", x1=str(x1), y1=str(y1), x2=str(x2), y2=str(y2), **{k: str(v) for k, v in attrs.items()})


def svg_text(parent: Element, x, y, text, **attrs):
    t = SubElement(parent, "text", x=str(x), y=str(y), **{k: str(v) for k, v in attrs.items()})
    t.text = text
    return t


def svg_rect(parent: Element, x, y, width, height, **attrs):
    SubElement(parent, "rect", x=str(x), y=str(y), width=str(width), height=str(height), **{k: str(v) for k, v in attrs.items()})


def pts_str(points: list[tuple[float, float]], scale: float, cx: float, cy: float) -> str:
    return " ".join(f"{cx + x * scale},{cy + y * scale}" for x, y in points)


def arrow_marker(parent: Element, mid: str):
    m = SubElement(parent, "marker", id=mid, markerWidth="6", markerHeight="6", refX="3", refY="3", orient="auto")
    SubElement(m, "path", d="M0,0 L6,3 L0,6 z", fill="#111")


def build_cushion_svg(style: CushionStyle, scale: float = 14.0) -> str:
    w, h = 520, 420
    cx, cy = w / 2, h / 2 + 10
    pts = style.points_fn()

    xs: list[float] = []
    ys: list[float] = []

    def add_xy(x: float, y: float) -> None:
        xs.append(x)
        ys.append(y)

    for x, y in pts:
        add_xy(cx + x * scale, cy + y * scale)

    dim_groups: list[tuple[Element, DimSpec]] = []
    for d in style.dims:
        x1, y1 = cx + d.x1 * scale, cy + d.y1 * scale
        x2, y2 = cx + d.x2 * scale, cy + d.y2 * scale
        add_xy(x1, y1)
        add_xy(x2, y2)
        add_xy(cx + d.lx * scale, cy + d.ly * scale)
        if d.ext1:
            add_xy(cx + d.ext1[0] * scale, cy + d.ext1[1] * scale)
        if d.ext2:
            add_xy(cx + d.ext2[0] * scale, cy + d.ext2[1] * scale)

    pad = 24
    min_x, max_x = min(xs) - pad, max(xs) + pad
    min_y, max_y = min(ys) - pad, max(ys) + pad
    vb_w, vb_h = max_x - min_x, max_y - min_y

    svg = svg_el(
        "svg",
        xmlns="http://www.w3.org/2000/svg",
        width=w,
        height=h,
        viewBox=f"{min_x} {min_y} {vb_w} {vb_h}",
    )
    defs = SubElement(svg, "defs")
    arrow_marker(defs, "arrow")
    SubElement(defs, "style").text = (
        "text{font-family:Helvetica,Arial,sans-serif;font-size:14px;fill:#111;"
        "font-weight:bold}"
    )

    g_outline = SubElement(svg, "g", id="outline")
    SubElement(
        g_outline,
        "polygon",
        points=pts_str(pts, scale, cx, cy),
        fill="none",
        stroke="#111",
        **{"stroke-width": "2.5"},
    )

    for d in style.dims:
        g = SubElement(svg, "g", id=f"dimension-{d.id}")
        x1, y1 = cx + d.x1 * scale, cy + d.y1 * scale
        x2, y2 = cx + d.x2 * scale, cy + d.y2 * scale
        if d.ext1:
            ex1, ey1 = cx + d.ext1[0] * scale, cy + d.ext1[1] * scale
            svg_line(g, ex1, ey1, x1, y1, stroke="#111", **{"stroke-width": 1})
        if d.ext2:
            ex2, ey2 = cx + d.ext2[0] * scale, cy + d.ext2[1] * scale
            svg_line(g, ex2, ey2, x2, y2, stroke="#111", **{"stroke-width": 1})
        svg_line(
            g,
            x1,
            y1,
            x2,
            y2,
            stroke="#111",
            **{"stroke-width": 1, "marker-start": "url(#arrow)", "marker-end": "url(#arrow)"},
        )
        tx, ty = cx + d.lx * scale, cy + d.ly * scale
        svg_text(g, tx, ty, d.label, **{"text-anchor": "middle"})

    return '<?xml version="1.0" encoding="UTF-8"?>\n' + tostring(svg, encoding="unicode")


def build_form_svg() -> str:
    svg = svg_el("svg", xmlns="http://www.w3.org/2000/svg", width=FORM_W, height=FORM_H, viewBox=f"0 0 {FORM_W} {FORM_H}")
    svg_rect(svg, 0, 0, FORM_W, FORM_H, fill="#fff")
    style = SubElement(svg, "style")
    style.text = (
        ".h{font-family:Helvetica,Arial,sans-serif;font-size:52px;font-weight:bold;fill:#111}"
        ".sub{font-family:Helvetica,Arial,sans-serif;font-size:28px;fill:#333}"
        ".lbl{font-family:Helvetica,Arial,sans-serif;font-size:24px;fill:#111}"
        ".fld{font-family:Helvetica,Arial,sans-serif;font-size:26px;fill:#666}"
        ".box{fill:none;stroke:#111;stroke-width:2}"
        ".cb{fill:none;stroke:#111;stroke-width:1.5}"
    )

    svg_text(svg, 120, 130, COMPANY, **{"class": "h"})
    svg_text(svg, 120, 175, SUBTITLE, **{"class": "sub"})
    svg_text(svg, 120, 215, ADDRESS, **{"class": "sub"})
    svg_text(svg, FORM_W / 2, 280, TITLE, **{"class": "h", "text-anchor": "middle"})

    y = 340
    svg_text(svg, 120, y, "To:", **{"class": "lbl"})
    svg_line(svg, 180, y + 8, 700, y + 8, stroke="#111", **{"stroke-width": 1})
    svg_text(svg, 120, y + 50, "From:", **{"class": "lbl"})
    svg_line(svg, 220, y + 58, 700, y + 58, stroke="#111", **{"stroke-width": 1})
    svg_text(svg, 1500, y, "Cushion Order No.", **{"class": "lbl"})
    svg_rect(svg, 1500, y - 25, 700, 45, **{"class": "box"})

    y0 = 430
    svg_rect(svg, 100, y0, FORM_W - 200, 520, **{"class": "box"})
    rows = [
        ("Design", 80),
        ("Item", 80),
        ("Qty", 80),
        ("Filling", 200),
        ("Cushion Border", 200),
        ("Size Type", 280),
    ]
    x = 120
    for label, rh in rows:
        svg_text(svg, x, y0 + 35, label, **{"class": "lbl"})
        if label == "Item":
            for i, opt in enumerate(ITEM_OPTIONS):
                oy = y0 + 60 + i * 40
                svg_rect(svg, x, oy, 22, 22, **{"class": "cb"})
                svg_text(svg, x + 32, oy + 18, opt, **{"class": "fld"})
        elif label == "Filling":
            for i, opt in enumerate(FILLING_OPTIONS):
                col, row = i % 2, i // 2
                ox, oy = x + col * 280, y0 + 55 + row * 38
                svg_rect(svg, ox, oy, 20, 20, **{"class": "cb"})
                svg_text(svg, ox + 28, oy + 16, opt, **{"class": "fld", "font-size": 20})
        elif label == "Cushion Border":
            for i, opt in enumerate(BORDER_OPTIONS):
                ox, oy = x + (i % 3) * 130, y0 + 55 + (i // 3) * 38
                svg_rect(svg, ox, oy, 20, 20, **{"class": "cb"})
                svg_text(svg, ox + 28, oy + 16, opt, **{"class": "fld"})
        elif label == "Size Type":
            for i, opt in enumerate(SIZE_OPTIONS):
                oy = y0 + 55 + i * 36
                svg_rect(svg, x, oy, 20, 20, **{"class": "cb"})
                svg_text(svg, x + 28, oy + 16, opt, **{"class": "fld", "font-size": 18})
        else:
            svg_line(svg, x, y0 + rh - 10, FORM_W - 140, y0 + rh - 10, stroke="#111", **{"stroke-width": 1})
        y0 += rh

    draw_y = 980
    svg_text(svg, 120, draw_y, "Cushion Drawing below", **{"class": "lbl", "font-size": 28})
    svg_rect(svg, 100, draw_y + 20, FORM_W - 200, 2300, **{"class": "box"})
    svg_text(
        svg, FORM_W / 2, draw_y + 120,
        "Place / draw / import cushion drawings here — seat left, back right (or as needed)",
        **{"class": "fld", "text-anchor": "middle", "font-size": 22},
    )

    return '<?xml version="1.0" encoding="UTF-8"?>\n' + tostring(svg, encoding="unicode")


def draw_form_png() -> Image.Image:
    img = Image.new("RGB", (FORM_W, FORM_H), "white")
    draw = ImageDraw.Draw(img)
    h_font = load_font(48, bold=True)
    sub_font = load_font(26)
    lbl_font = load_font(24, bold=True)
    fld_font = load_font(22)

    draw.text((120, 60), COMPANY, fill="#111", font=h_font)
    draw.text((120, 115), SUBTITLE, fill="#333", font=sub_font)
    draw.text((120, 150), ADDRESS, fill="#333", font=sub_font)
    tw = draw.textlength(TITLE, font=h_font)
    draw.text((FORM_W / 2 - tw / 2, 220), TITLE, fill="#111", font=h_font)

    y = 300
    draw.text((120, y), "To:", fill="#111", font=lbl_font)
    draw.line([(200, y + 30), (720, y + 30)], fill="#111", width=2)
    draw.text((120, y + 55), "From:", fill="#111", font=lbl_font)
    draw.line([(230, y + 85), (720, y + 85)], fill="#111", width=2)
    draw.text((1500, y), "Cushion Order No.", fill="#111", font=lbl_font)
    draw.rectangle([1500, y + 10, 2200, y + 55], outline="#111", width=2)

    y0 = 400
    draw.rectangle([100, y0, FORM_W - 100, y0 + 620], outline="#111", width=2)

    def row(label, y, extra_fn=None):
        draw.text((120, y), label, fill="#111", font=lbl_font)
        if extra_fn:
            extra_fn(y)
        else:
            draw.line([(120, y + 45), (FORM_W - 120, y + 45)], fill="#111", width=1)

    row("Design", y0 + 15)
    row("Item", y0 + 95, lambda y: [
        draw.rectangle([120, y + 30 + i * 42, 142, y + 52 + i * 42], outline="#111")
        or draw.text((155, y + 28 + i * 42), opt, fill="#555", font=fld_font)
        for i, opt in enumerate(ITEM_OPTIONS)
    ])
    row("Qty", y0 + 230)
    row("Filling", y0 + 310, lambda y: [
        (draw.rectangle([120 + (i % 2) * 300, y + 28 + (i // 2) * 36, 140 + (i % 2) * 300, y + 48 + (i // 2) * 36], outline="#111"),
         draw.text((150 + (i % 2) * 300, y + 24 + (i // 2) * 36), opt, fill="#555", font=fld_font))
        for i, opt in enumerate(FILLING_OPTIONS)
    ])
    row("Cushion Border", y0 + 430, lambda y: [
        (draw.rectangle([120 + (i % 3) * 200, y + 28 + (i // 3) * 36, 140 + (i % 3) * 200, y + 48 + (i // 3) * 36], outline="#111"),
         draw.text((150 + (i % 3) * 200, y + 24 + (i // 3) * 36), opt, fill="#555", font=fld_font))
        for i, opt in enumerate(BORDER_OPTIONS)
    ])
    row("Size Type", y0 + 520, lambda y: [
        (draw.rectangle([120, y + 28 + i * 34, 140, y + 48 + i * 34], outline="#111"),
         draw.text((150, y + 24 + i * 34), textwrap.shorten(opt, 42), fill="#555", font=load_font(18)))
        for i, opt in enumerate(SIZE_OPTIONS)
    ])

    draw_y = 1040
    draw.text((120, draw_y), "Cushion Drawing below", fill="#111", font=lbl_font)
    draw.rectangle([100, draw_y + 25, FORM_W - 100, FORM_H - 100], outline="#111", width=2)

    note = "Import SVG cushion drawing here · draw dimension lines on new layers · add text with Procreate Text tool"
    nw = draw.textlength(note, font=fld_font)
    draw.text((FORM_W / 2 - nw / 2, draw_y + 80), note, fill="#888", font=fld_font)
    return img


def build_fillable_pdf(path: Path):
    c = pdf_canvas.Canvas(str(path), pagesize=A4)
    w, h = A4

    def t(x, y, txt, size=10, bold=False):
        c.setFont("Helvetica-Bold" if bold else "Helvetica", size)
        c.drawString(x, y, txt)

    t(20 * mm, h - 20 * mm, COMPANY, 16, True)
    t(20 * mm, h - 27 * mm, SUBTITLE, 9)
    t(20 * mm, h - 32 * mm, ADDRESS, 8)
    t(w / 2 - 45 * mm, h - 42 * mm, TITLE, 14, True)

    form = c.acroForm
    y = h - 55 * mm
    t(20 * mm, y, "To:")
    form.textfield(name="to", x=35 * mm, y=y - 4 * mm, width=70 * mm, height=8 * mm, borderStyle="underlined")
    t(20 * mm, y - 12 * mm, "From:")
    form.textfield(name="from", x=38 * mm, y=y - 16 * mm, width=67 * mm, height=8 * mm, borderStyle="underlined")
    t(130 * mm, y, "Cushion Order No.")
    form.textfield(name="order_no", x=130 * mm, y=y - 4 * mm, width=55 * mm, height=8 * mm, borderStyle="solid")

    box_top = h - 68 * mm
    c.rect(15 * mm, box_top - 75 * mm, w - 30 * mm, 115 * mm)

    t(18 * mm, box_top - 8 * mm, "Design", 9, True)
    form.textfield(name="design", x=18 * mm, y=box_top - 18 * mm, width=w - 36 * mm, height=7 * mm, borderStyle="underlined")

    t(18 * mm, box_top - 26 * mm, "Item:", 9, True)
    form.checkbox(name="item_sofa", x=35 * mm, y=box_top - 28 * mm, size=4 * mm, buttonStyle="check")
    t(40 * mm, box_top - 25 * mm, "sofa", 8)
    form.checkbox(name="item_chair", x=55 * mm, y=box_top - 28 * mm, size=4 * mm, buttonStyle="check")
    t(60 * mm, box_top - 25 * mm, "chair", 8)
    form.checkbox(name="item_other", x=78 * mm, y=box_top - 28 * mm, size=4 * mm, buttonStyle="check")
    t(83 * mm, box_top - 25 * mm, "other", 8)

    t(18 * mm, box_top - 36 * mm, "Qty", 9, True)
    form.textfield(name="qty", x=18 * mm, y=box_top - 46 * mm, width=25 * mm, height=7 * mm, borderStyle="underlined")

    t(18 * mm, box_top - 52 * mm, "Filling", 9, True)
    fx = 18 * mm
    for i, opt in enumerate(FILLING_OPTIONS):
        col, row = i % 3, i // 3
        ox, oy = fx + col * 55 * mm, box_top - 58 * mm - row * 8 * mm
        form.checkbox(name=f"filling_{i}", x=ox, y=oy, size=3.5 * mm, buttonStyle="check")
        t(ox + 5 * mm, oy + 1 * mm, opt, 7)

    t(18 * mm, box_top - 78 * mm, "Cushion Border", 9, True)
    for i, opt in enumerate(BORDER_OPTIONS):
        ox = 18 * mm + (i % 3) * 55 * mm
        oy = box_top - 84 * mm - (i // 3) * 8 * mm
        form.checkbox(name=f"border_{i}", x=ox, y=oy, size=3.5 * mm, buttonStyle="check")
        t(ox + 5 * mm, oy + 1 * mm, opt, 7)

    t(18 * mm, box_top - 98 * mm, "Size Type", 9, True)
    for i, opt in enumerate(SIZE_OPTIONS[:3]):
        oy = box_top - 104 * mm - i * 7 * mm
        form.checkbox(name=f"size_{i}", x=18 * mm, y=oy, size=3.5 * mm, buttonStyle="check")
        t(24 * mm, oy + 1 * mm, textwrap.shorten(opt, 55), 6)

    t(18 * mm, box_top - 128 * mm, "Notes / fabric / special instructions", 9, True)
    form.textfield(
        name="notes", x=18 * mm, y=box_top - 148 * mm, width=w - 36 * mm, height=18 * mm,
        borderStyle="solid", fieldFlags="multiline",
    )

    draw_y = box_top - 155 * mm
    t(18 * mm, draw_y, "Cushion Drawing below", 10, True)
    c.rect(15 * mm, 15 * mm, w - 30 * mm, draw_y - 18 * mm)
    t(18 * mm, draw_y - 8 * mm, "Draw on this page in GoodNotes / Notability, or print and mark up by hand.", 7)

    c.showPage()
    c.save()


def write_readme():
    (ROOT / "README.md").write_text("""# Cushion Order Kit

Editable cushion order templates based on the **Dudgeon purchase order** layout.

## The problem with PNG layers in Procreate

PNG images bake lines and text into pixels — you **cannot** move individual dimension lines or edit labels after import. Procreate is a drawing app, not a form/vector editor.

This kit gives you **three workable approaches** depending on what you need.

---

## Option A — Best for editable forms + drawings (recommended)

**GoodNotes 6** or **Notability** on iPad:

1. Open `form/dudgeon-purchase-order.pdf` (fillable fields work in PDF Expert / Acrobat too).
2. Fill **To, From, Order No, Design, Qty** with the keyboard or Apple Pencil.
3. In the drawing box, **import** an SVG from `drawings/svg/` or draw freehand.
4. Dimension lines and notes are fully moveable if you draw them with the pen tool.

**Why this works:** PDF keeps the form structure; the drawing app lets you edit everything on top.

---

## Option B — Moveable lines + editable text (vector)

**Affinity Designer 2** (iPad) or **Concepts** (iPad):

1. Open `form/dudgeon-purchase-order.svg`.
2. Open a cushion drawing from `drawings/svg/chair/seat/t-cushion.svg` (etc.).
3. Each **dimension line is its own group** — tap to move, resize, or delete.
4. **Double-tap text** to edit inch measurements.
5. Export as PDF or PNG for sending to the workshop.

**Why this works:** SVG is true vector — lines and text stay editable.

---

## Option C — Still want Procreate?

Use Procreate for the **sketch only**, not the form fields:

1. New canvas → import `form/dudgeon-purchase-order-background.png` → **lock layer**.
2. Import cushion **outline** from `drawings/svg/` (Procreate flattens SVG but you can scale/move the whole import).
3. On **new layers above**, draw dimension lines with the **Monoline** brush (moveable by transforming that layer, or draw each dimension on its own layer).
4. Add all text (measurements, notes, To/From) with Procreate's **Add Text** tool — this stays editable.
5. Tick boxes: draw circles by hand or use PNG stamp.

**Honest limit:** Procreate will not give you moveable individual imported dimension lines from SVG. Draw them yourself on separate layers, or use Option A/B.

---

## Folder layout

```
cushion-order-kit/
├── form/
│   ├── dudgeon-purchase-order.pdf       ← fillable form fields
│   ├── dudgeon-purchase-order.svg       ← editable in Affinity/Concepts
│   └── dudgeon-purchase-order-background.png  ← Procreate locked background
└── drawings/svg/
    ├── chair/seat/t-cushion.svg         ← groups: outline, dimension-back-width, …
    └── …
```

## SVG group names (each moveable in vector apps)

| Group ID | Contents |
|----------|----------|
| `outline` | Cushion shape |
| `dimension-back-width` | One dimension line + label |
| `dimension-front-width` | … |
| `title` | Style name |

## Regenerate

```bash
python3 scripts/generate_cushion_order_kit.py
```
""")


def main():
    form_dir = ROOT / "form"
    form_dir.mkdir(parents=True, exist_ok=True)

    build_fillable_pdf(form_dir / "dudgeon-purchase-order.pdf")
    (form_dir / "dudgeon-purchase-order.svg").write_text(build_form_svg())
    draw_form_png().save(form_dir / "dudgeon-purchase-order-background.png", "PNG", dpi=(300, 300))

    manifest = {"styles": {}}
    for s in STYLES:
        out = ROOT / "drawings" / "svg" / s.category / s.part / f"{s.style_id}.svg"
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(build_cushion_svg(s))
        manifest["styles"][f"{s.category}/{s.part}/{s.style_id}"] = {
            "name": s.name,
            "file": str(out.relative_to(ROOT)),
            "groups": ["outline", "title"] + [f"dimension-{d.id}" for d in s.dims],
        }
        print(out.relative_to(ROOT))

    (ROOT / "manifest.json").write_text(json.dumps(manifest, indent=2))
    write_readme()
    print(f"\nKit written to {ROOT}")


if __name__ == "__main__":
    main()
