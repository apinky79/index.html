#!/usr/bin/env python3
"""
Generate Dudgeon-style cushion order drawings for Procreate.

Plan-view technical sketches with dimension leader lines and inch callouts,
matching workshop purchase-order layout. Each style exports separate layers.
"""

from __future__ import annotations

import json
import math
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent / "procreate-cushion-templates"
W, H = 2000, 1600
MARGIN = 120
INK = (20, 20, 20, 255)
DIM = (20, 20, 20, 255)
TITLE = (20, 20, 20, 255)
WHITE = (255, 255, 255, 255)

OUTLINE_W = 5
DIM_W = 2


def load_font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    names = (
        ["DejaVuSans-Bold.ttf", "LiberationSans-Bold.ttf"]
        if bold
        else ["DejaVuSans.ttf", "LiberationSans.ttf"]
    )
    for name in names:
        path = Path("/usr/share/fonts/truetype/dejavu") / name
        if not path.exists():
            path = Path("/usr/share/fonts/truetype/liberation") / name
        if path.exists():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


FONT_DIM = load_font(36)
FONT_TITLE = load_font(44, bold=True)
FONT_SUB = load_font(28)


@dataclass
class DimSpec:
    """Dimension annotation: line from (x1,y1)-(x2,y2) with label at offset."""
    x1: float
    y1: float
    x2: float
    y2: float
    label: str
    ox: float = 0  # label offset
    oy: float = 0
    ext1: tuple[float, float] | None = None  # extension line from shape
    ext2: tuple[float, float] | None = None


@dataclass
class TemplateSpec:
    category: str
    part: str
    style_id: str
    title: str
    subtitle: str
    draw_shape: Callable[[ImageDraw.ImageDraw, float, float, float], list[tuple[float, float]]]
    dims: list[DimSpec] = field(default_factory=list)


class Ctx:
    """Maps inch coordinates to canvas pixels, centred in drawing area."""

    def __init__(self, draw: ImageDraw.ImageDraw, cx: float, cy: float, scale: float):
        self.draw = draw
        self.cx = cx
        self.cy = cy
        self.scale = scale

    def pt(self, x: float, y: float) -> tuple[float, float]:
        return (self.cx + x * self.scale, self.cy + y * self.scale)

    def poly(self, points: list[tuple[float, float]], close: bool = True):
        px = [self.pt(x, y) for x, y in points]
        if close:
            self.draw.line(px + [px[0]], fill=INK, width=OUTLINE_W, joint="curve")
        else:
            self.draw.line(px, fill=INK, width=OUTLINE_W, joint="curve")


def arrow_head(draw, tip, angle, size=12):
    x, y = tip
    a1 = angle + math.pi * 0.82
    a2 = angle - math.pi * 0.82
    p1 = (x + size * math.cos(a1), y + size * math.sin(a1))
    p2 = (x + size * math.cos(a2), y + size * math.sin(a2))
    draw.polygon([tip, p1, p2], fill=DIM)


def draw_dimension(draw: ImageDraw.ImageDraw, d: DimSpec, scale: float, cx: float, cy: float):
    def p(x, y):
        return (cx + x * scale, cy + y * scale)

    x1, y1 = p(d.x1, d.y1)
    x2, y2 = p(d.x2, d.y2)
    horizontal = abs(y2 - y1) < abs(x2 - x1) * 0.15

    if d.ext1:
        draw.line([p(*d.ext1), (x1, y1)], fill=DIM, width=DIM_W)
    if d.ext2:
        draw.line([p(*d.ext2), (x2, y2)], fill=DIM, width=DIM_W)

    draw.line([(x1, y1), (x2, y2)], fill=DIM, width=DIM_W)

    if horizontal:
        arrow_head(draw, (x1, y1), 0)
        arrow_head(draw, (x2, y2), math.pi)
    else:
        arrow_head(draw, (x1, y1), math.pi / 2)
        arrow_head(draw, (x2, y2), -math.pi / 2)

    mx = (x1 + x2) / 2 + d.ox
    my = (y1 + y2) / 2 + d.oy
    tw = draw.textlength(d.label, font=FONT_DIM)
    draw.text((mx - tw / 2, my - 18), d.label, fill=DIM, font=FONT_DIM)


# --- Shape builders (coordinates in inches, origin at shape centre) ---

def t_cushion_seat_shape(_draw, _cx, _cy, _s) -> list[tuple[float, float]]:
    # Matches Dudgeon order form: back 20, front 32, depth 28, front wrap 6.5
    hw_back, hw_front, depth, wrap = 10, 16, 28, 6.5
    y0 = -depth / 2
    y_wrap = y0 + wrap
    y1 = depth / 2
    return [
        (-hw_back, y0),
        (hw_back, y0),
        (hw_back, y_wrap),
        (hw_front, y_wrap),
        (hw_front, y1),
        (-hw_front, y1),
        (-hw_front, y_wrap),
        (-hw_back, y_wrap),
    ]


T_SEAT_DIMS = [
    DimSpec(-10, -18, 10, -18, '20"', oy=-28, ext1=(-10, -14), ext2=(10, -14)),
    DimSpec(-16, 16, 16, 16, '32"', oy=28, ext1=(-16, 14), ext2=(16, 14)),
    DimSpec(20, -14, 20, 14, '28"', ox=36, ext1=(16, -14), ext2=(16, 14)),
    DimSpec(-22, -14, -22, -1.5, '6.5"', ox=-42, ext1=(-16, -14), ext2=(-16, -1.5)),
]


def t_back_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    # Dudgeon back: top 28, bottom 20, wing 13, total 23
    hw_top, hw_bot, total_h, wing_h = 14, 10, 23, 13
    y0 = -total_h / 2
    y_wing = y0 + wing_h
    y1 = total_h / 2
    return [
        (-hw_top, y0),
        (hw_top, y0),
        (hw_top, y_wing),
        (hw_bot, y_wing),
        (hw_bot, y1),
        (-hw_bot, y1),
        (-hw_bot, y_wing),
        (-hw_top, y_wing),
    ]


T_BACK_DIMS = [
    DimSpec(-14, -14, 14, -14, '28"', oy=-28, ext1=(-14, -11.5), ext2=(14, -11.5)),
    DimSpec(-10, 13, 10, 13, '20"', oy=28, ext1=(-10, 11.5), ext2=(10, 11.5)),
    DimSpec(16, -11.5, 16, 0, '13"', ox=38, ext1=(14, -11.5), ext2=(10, 0)),
    DimSpec(-20, -11.5, -20, 11.5, '23"', ox=-44, ext1=(-14, -11.5), ext2=(-10, 11.5)),
]


def box_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    w, h = 24, 22
    return [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]


BOX_DIMS = [
    DimSpec(-12, -14, 12, -14, '24"', oy=-28, ext1=(-12, -11), ext2=(12, -11)),
    DimSpec(16, -11, 16, 11, '22"', ox=38, ext1=(12, -11), ext2=(12, 11)),
]


def knife_edge_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    w, h, taper = 24, 22, 2
    return [
        (-w / 2 + taper, -h / 2),
        (w / 2 - taper, -h / 2),
        (w / 2, h / 2),
        (-w / 2, h / 2),
    ]


KNIFE_DIMS = BOX_DIMS  # same bounding dims


def l_cushion_seat_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    # Corner sectional: main 22 deep x 18 wide + leg 14 x 14
    return [
        (-9, -11),
        (9, -11),
        (9, 0),
        (16, 0),
        (16, 11),
        (-9, 11),
    ]


L_SEAT_DIMS = [
    DimSpec(-9, -15, 9, -15, '18"', oy=-28, ext1=(-9, -11), ext2=(9, -11)),
    DimSpec(-13, -11, -13, 11, '22"', ox=-44, ext1=(-9, -11), ext2=(-9, 11)),
    DimSpec(9, 15, 16, 15, '14"', oy=28, ext1=(9, 11), ext2=(16, 11)),
    DimSpec(20, 0, 20, 11, '14"', ox=36, ext1=(16, 0), ext2=(16, 11)),
]


def bullnose_seat_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    w, h = 24, 20
    pts: list[tuple[float, float]] = [
        (-w / 2, -h / 2),
        (w / 2, -h / 2),
        (w / 2, h / 2 - 3),
    ]
    steps = 12
    for i in range(steps + 1):
        t = i / steps
        ang = t * math.pi
        pts.append((w / 2 * math.cos(ang), h / 2 - 3 + 3 * math.sin(ang)))
    pts.append((-w / 2, h / 2 - 3))
    return pts


BULLNOSE_DIMS = [
    DimSpec(-12, -14, 12, -14, '24"', oy=-28, ext1=(-12, -10), ext2=(12, -10)),
    DimSpec(17, -10, 17, 10, '20"', ox=40, ext1=(12, -10), ext2=(12, 10)),
    DimSpec(-18, 8, 18, 8, 'roll', oy=30),
]


def waterfall_seat_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    return [(-12, -9), (12, -11), (12, 11), (-12, 11)]


WATERFALL_DIMS = [
    DimSpec(-12, -15, 12, -15, '24"', oy=-28, ext1=(-12, -11), ext2=(12, -11)),
    DimSpec(16, -11, 16, 11, '22"', ox=38, ext1=(12, -11), ext2=(12, 11)),
]


def bench_seat_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    return [(-28, -8), (28, -8), (28, 8), (-28, 8)]


BENCH_DIMS = [
    DimSpec(-28, -12, 28, -12, '56"', oy=-28, ext1=(-28, -8), ext2=(28, -8)),
    DimSpec(32, -8, 32, 8, '16"', ox=44, ext1=(28, -8), ext2=(28, 8)),
]


def chaise_seat_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    return [(-10, -8), (10, -8), (10, 8), (22, 8), (22, 18), (-10, 18)]


CHAISE_DIMS = [
    DimSpec(-10, -12, 10, -12, '20"', oy=-28, ext1=(-10, -8), ext2=(10, -8)),
    DimSpec(-14, -8, -14, 18, '26"', ox=-44, ext1=(-10, -8), ext2=(-10, 18)),
    DimSpec(10, 22, 22, 22, '12"', oy=28, ext1=(10, 18), ext2=(22, 18)),
]


def round_seat_shape(_d, _cx, _cy, s) -> list[tuple[float, float]]:
    r = 11
    return [(r * math.cos(2 * math.pi * i / 48), r * math.sin(2 * math.pi * i / 48)) for i in range(48)]


ROUND_DIMS = [
    DimSpec(-11, 15, 11, 15, 'Ø 22"', oy=28),
    DimSpec(0, -11, 0, 11, '22"', ox=36),
]


def slip_seat_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    return [(-9, -8), (9, -8), (11, 8), (-11, 8)]


SLIP_DIMS = [
    DimSpec(-9, -12, 9, -12, '18"', oy=-28, ext1=(-9, -8), ext2=(9, -8)),
    DimSpec(-12, 12, 12, 12, '22"', oy=28, ext1=(-11, 8), ext2=(11, 8)),
]


def scatter_back_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    return [(-10, -8), (10, -8), (10, 8), (-10, 8)]


SCATTER_DIMS = [
    DimSpec(-10, -12, 10, -12, '20"', oy=-28, ext1=(-10, -8), ext2=(10, -8)),
    DimSpec(14, -8, 14, 8, '16"', ox=36, ext1=(10, -8), ext2=(10, 8)),
]


def wing_back_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    return [
        (-6, -12),
        (6, -12),
        (6, 4),
        (14, -4),
        (14, 8),
        (-14, 8),
        (-14, -4),
        (-6, 4),
    ]


WING_DIMS = [
    DimSpec(-6, -16, 6, -16, '12"', oy=-28, ext1=(-6, -12), ext2=(6, -12)),
    DimSpec(-16, 12, 16, 12, '28"', oy=28, ext1=(-14, 8), ext2=(14, 8)),
    DimSpec(18, -4, 18, 8, '12"', ox=40, ext1=(14, -4), ext2=(14, 8)),
]


def channel_back_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    return [(-12, -11), (12, -11), (12, 11), (-12, 11)]


def channel_details(draw: ImageDraw.ImageDraw, cx: float, cy: float, scale: float):
    for i in range(-4, 5):
        x = cx + i * (24 / 8) * scale
        draw.line([(x, cy - 11 * scale), (x, cy + 11 * scale)], fill=DIM, width=2)


CHANNEL_DIMS = BOX_DIMS


def fixed_back_shape(_d, _cx, _cy, _s) -> list[tuple[float, float]]:
    return [(-14, -12), (14, -12), (14, 12), (-14, 12)]


FIXED_DIMS = [
    DimSpec(-14, -16, 14, -16, '28"', oy=-28, ext1=(-14, -12), ext2=(14, -12)),
    DimSpec(18, -12, 18, 12, '24"', ox=40, ext1=(14, -12), ext2=(14, 12)),
]


TEMPLATES: list[TemplateSpec] = [
    # SOFA SEATS
    TemplateSpec("sofa", "seat", "t-cushion", "T-Cushion Seat", "Sofa · Seat", t_cushion_seat_shape, T_SEAT_DIMS),
    TemplateSpec("sofa", "seat", "box", "Box Cushion Seat", "Sofa · Seat", box_shape, BOX_DIMS),
    TemplateSpec("sofa", "seat", "knife-edge", "Knife Edge Seat", "Sofa · Seat", knife_edge_shape, KNIFE_DIMS),
    TemplateSpec("sofa", "seat", "l-cushion", "L-Cushion Seat", "Sofa · Seat · Corner", l_cushion_seat_shape, L_SEAT_DIMS),
    TemplateSpec("sofa", "seat", "bullnose", "Bullnose Seat", "Sofa · Seat", bullnose_seat_shape, BULLNOSE_DIMS),
    TemplateSpec("sofa", "seat", "waterfall", "Waterfall Seat", "Sofa · Seat", waterfall_seat_shape, WATERFALL_DIMS),
    TemplateSpec("sofa", "seat", "bench", "Bench Seat", "Sofa · Seat", bench_seat_shape, BENCH_DIMS),
    TemplateSpec("sofa", "seat", "chaise", "Chaise Seat", "Sofa · Seat", chaise_seat_shape, CHAISE_DIMS),
    # SOFA BACKS
    TemplateSpec("sofa", "back", "t-back", "T-Back Cushion", "Sofa · Back", t_back_shape, T_BACK_DIMS),
    TemplateSpec("sofa", "back", "box", "Box Cushion Back", "Sofa · Back", box_shape, BOX_DIMS),
    TemplateSpec("sofa", "back", "knife-edge", "Knife Edge Back", "Sofa · Back", knife_edge_shape, KNIFE_DIMS),
    TemplateSpec("sofa", "back", "l-back", "L-Back Cushion", "Sofa · Back · Corner", l_cushion_seat_shape, L_SEAT_DIMS),
    TemplateSpec("sofa", "back", "scatter", "Loose Scatter Back", "Sofa · Back", scatter_back_shape, SCATTER_DIMS),
    TemplateSpec("sofa", "back", "channel", "Channel Back", "Sofa · Back", channel_back_shape, CHANNEL_DIMS),
    TemplateSpec("sofa", "back", "fixed", "Fixed Back Panel", "Sofa · Back", fixed_back_shape, FIXED_DIMS),
    # CHAIR SEATS — t-cushion matches Dudgeon reference
    TemplateSpec("chair", "seat", "t-cushion", "T-Cushion Seat", "Chair · Seat", t_cushion_seat_shape, T_SEAT_DIMS),
    TemplateSpec("chair", "seat", "box", "Box Cushion Seat", "Chair · Seat", box_shape, BOX_DIMS),
    TemplateSpec("chair", "seat", "knife-edge", "Knife Edge Seat", "Chair · Seat", knife_edge_shape, KNIFE_DIMS),
    TemplateSpec("chair", "seat", "round", "Round Seat", "Chair · Seat", round_seat_shape, ROUND_DIMS),
    TemplateSpec("chair", "seat", "slip", "Slip Seat", "Chair · Seat · Drop-in", slip_seat_shape, SLIP_DIMS),
    # CHAIR BACKS — t-back matches Dudgeon reference
    TemplateSpec("chair", "back", "t-back", "T-Back Cushion", "Chair · Back", t_back_shape, T_BACK_DIMS),
    TemplateSpec("chair", "back", "box", "Box Cushion Back", "Chair · Back", box_shape, BOX_DIMS),
    TemplateSpec("chair", "back", "knife-edge", "Knife Edge Back", "Chair · Back", knife_edge_shape, KNIFE_DIMS),
    TemplateSpec("chair", "back", "scatter", "Loose Back Cushion", "Chair · Back", scatter_back_shape, SCATTER_DIMS),
    TemplateSpec("chair", "back", "wing", "Wing Back", "Chair · Back", wing_back_shape, WING_DIMS),
    TemplateSpec("chair", "back", "fixed", "Fixed Back Panel", "Chair · Back", fixed_back_shape, FIXED_DIMS),
]


def auto_scale(spec: TemplateSpec) -> float:
    pts = spec.draw_shape(None, 0, 0, 1)  # type: ignore
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    bw = max(xs) - min(xs)
    bh = max(ys) - min(ys)
    avail_w = W - 2 * MARGIN - 160
    avail_h = H - 2 * MARGIN - 200
    return min(avail_w / bw, avail_h / bh)


def render_outline(spec: TemplateSpec) -> Image.Image:
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    scale = auto_scale(spec)
    cx, cy = W / 2, H / 2 + 40
    ctx = Ctx(draw, cx, cy, scale)
    pts = spec.draw_shape(draw, cx, cy, scale)
    ctx.poly(pts)
    return img


def render_dimensions(spec: TemplateSpec) -> Image.Image:
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    scale = auto_scale(spec)
    cx, cy = W / 2, H / 2 + 40
    if spec.style_id == "channel" and spec.part == "back":
        channel_details(draw, cx, cy, scale)
    for d in spec.dims:
        draw_dimension(draw, d, scale, cx, cy)
    return img


def render_title(spec: TemplateSpec) -> Image.Image:
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.text((MARGIN, 36), spec.title, fill=TITLE, font=FONT_TITLE)
    draw.text((MARGIN, 88), spec.subtitle, fill=TITLE, font=FONT_SUB)
    note = "Replace dimensions with your measurements"
    tw = draw.textlength(note, font=FONT_SUB)
    draw.text((W - MARGIN - tw, 36), note, fill=(100, 100, 100, 255), font=FONT_SUB)
    draw.text((MARGIN, H - 56), "Cushion drawing — plan view", fill=(100, 100, 100, 255), font=FONT_SUB)
    return img


def composite(*layers: Image.Image, white_bg: bool = True) -> Image.Image:
    base = Image.new("RGBA", (W, H), WHITE if white_bg else (0, 0, 0, 0))
    for layer in layers:
        base = Image.alpha_composite(base, layer)
    return base


def save_template(spec: TemplateSpec) -> dict:
    out = ROOT / spec.category / spec.part / spec.style_id
    layers_dir = out / "layers"
    layers_dir.mkdir(parents=True, exist_ok=True)

    outline = render_outline(spec)
    dimensions = render_dimensions(spec)
    title = render_title(spec)
    comp = composite(title, outline, dimensions)

    files = {}
    for name, img in [("01-outline", outline), ("02-dimensions", dimensions), ("03-title", title)]:
        p = layers_dir / f"{name}.png"
        img.save(p, "PNG")
        files[name] = str(p.relative_to(ROOT))

    cp = out / f"{spec.style_id}.png"
    comp.save(cp, "PNG")
    files["drawing"] = str(cp.relative_to(ROOT))
    return files


def write_readme():
    (ROOT / "README.md").write_text("""# Cushion Order Drawings (Procreate)

Dudgeon-style **plan-view cushion drawings** with dimension lines and inch callouts — for filling out workshop purchase orders in Procreate.

## What's included

Each style is a technical sketch like a cushion order form:

- **Black outline** — cushion shape (plan view, looking down)
- **Dimension lines** — arrows and inch measurements (edit these for each order)
- **Title** — style name and category

### Sofa
- **Seats:** t-cushion, box, knife-edge, l-cushion, bullnose, waterfall, bench, chaise
- **Backs:** t-back, box, knife-edge, l-back, scatter, channel, fixed

### Chair
- **Seats:** t-cushion, box, knife-edge, round, slip
- **Backs:** t-back, box, knife-edge, scatter, wing, fixed

The **chair t-cushion seat** and **t-back** match the Dudgeon order-form layout (20"/32"/28"/6.5" seat and 28"/20"/13"/23" back).

## Folder layout

```
chair/seat/t-cushion/
├── layers/
│   ├── 01-outline.png
│   ├── 02-dimensions.png
│   └── 03-title.png
└── t-cushion.png          ← full drawing on white (like order form)
```

## Procreate workflow

1. Save this folder to **iCloud Drive** (see repo instructions).
2. New canvas (A4 or 2000×1600).
3. Import layers: **01-outline → 02-dimensions → 03-title** (or import the single `.png` drawing).
4. Use the **Transform** tool to scale the whole group to fit your page.
5. **Erase and rewrite** the dimension numbers, or add text layers with your measurements.
6. Draw any extra notes (filling, border, fabric) on new layers above.

## Regenerate

```bash
python3 scripts/generate_cushion_templates.py
```
""")


def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    manifest = {"templates": {}, "format": "Dudgeon-style plan view", "size": [W, H]}
    for spec in TEMPLATES:
        key = f"{spec.category}/{spec.part}/{spec.style_id}"
        manifest["templates"][key] = {"title": spec.title, "files": save_template(spec)}
        print(key)
    (ROOT / "manifest.json").write_text(json.dumps(manifest, indent=2))
    write_readme()
    print(f"\n{len(TEMPLATES)} drawings → {ROOT}")


if __name__ == "__main__":
    main()
