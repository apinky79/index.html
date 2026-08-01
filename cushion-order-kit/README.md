# Cushion Order Kit

Editable cushion order templates based on the **Dudgeon purchase order** layout.

---

## Free apps only (iPad) — start here

You don't need Procreate, GoodNotes, or Affinity. These are **100% free**:

### Best free setup: Adobe Acrobat Reader + Apple Notes

Both are free. Apple Notes is already on your iPad.

**Step 1 — Fill the form (Acrobat Reader)**
1. Install **Adobe Acrobat Reader** from the App Store (free).
2. Copy `form/dudgeon-purchase-order.pdf` to iCloud / Files.
3. Open it in Acrobat Reader.
4. Tap form fields to type **To, From, Order No, Design, Qty**, tick filling/border boxes, add notes.
5. Share → **Save to Files** or **Copy to Notes**.

**Step 2 — Draw the cushion (Apple Notes)**
1. Open the PDF in **Notes** (share sheet → Notes), or attach it to a new note.
2. Tap the **Markup** pen icon.
3. In the big “Cushion Drawing below” box, draw your cushion shape and dimension lines.
4. Use the **text tool** in Markup (Aa) for inch measurements.
5. Everything you draw or type can be edited later.

**Step 3 — Duplicate for each order**
- In Notes: select the note → **Duplicate** for the next job.

---

### Best free for moveable lines + editable measurements

**Linearity Curve** (free on iPad — search “Linearity Curve” in App Store):

1. Open `form/dudgeon-purchase-order.svg`.
2. Open a cushion file, e.g. `drawings/svg/chair/seat/t-cushion.svg`.
3. Tap a dimension group → **move, resize, or delete** it.
4. Double-tap inch text → edit the number.
5. Export as PDF to email to the workshop.

The free tier is enough for opening SVGs and editing layers.

---

### Also free: Apple Freeform

Good if you want to **drag things around** on a board:

1. New Freeform board.
2. Insert `dudgeon-purchase-order-background.png` as the base.
3. Insert cushion SVGs (or draw with Apple Pencil).
4. Add text boxes for To / From / measurements — move them anywhere.
5. Share as PDF when done.

---

## Files in this kit

```
cushion-order-kit/
├── form/
│   ├── dudgeon-purchase-order.pdf       ← fillable in Acrobat Reader (free)
│   ├── dudgeon-purchase-order.svg       ← editable in Linearity Curve (free)
│   └── dudgeon-purchase-order-background.png
└── drawings/svg/
    └── chair/seat/t-cushion.svg         ← moveable dimension groups
```

---

## The problem with PNG layers in Procreate

PNG images bake lines and text into pixels — you **cannot** move individual dimension lines or edit labels after import. Procreate also costs money. Use the free workflow above instead.

---

## Paid alternatives (if you ever want them)

<details>
<summary>GoodNotes, Affinity, Procreate</summary>

**GoodNotes / Notability** — PDF + pen drawing (paid/subscription).

**Affinity Designer** — full vector editing (paid once).

**Procreate** — draw on form background; lines must be drawn by hand on separate layers.

</details>

---

## SVG group names (Linearity Curve / vector apps)

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
