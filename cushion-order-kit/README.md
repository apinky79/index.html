# Cushion Order Kit

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
