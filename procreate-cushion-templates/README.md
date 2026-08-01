# Cushion Order Drawings (Procreate)

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
