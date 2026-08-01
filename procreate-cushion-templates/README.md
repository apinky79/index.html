# Procreate Cushion Templates

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
