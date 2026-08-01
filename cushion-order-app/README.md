# Cushion Order App

**Free, private, browser-based** cushion purchase orders — no app store, no subscription, no data sent to a server.

## Open the app

After GitHub Pages is enabled for this repo, open:

**https://apinky79.github.io/index.html/cushion-order-app/**

(On iPad: Safari → Share → **Add to Home Screen** for an app-like icon.)

## Features

- Full **Dudgeon-style purchase order** form
- Editable fields: To, From, order number, design, qty, filling, border, size type, notes
- **Seat + back drawing panels** with cushion templates
- **Drag** any outline or dimension line to move it
- **Double-tap** a measurement to edit the inches
- **Pinch to zoom** drawing panels
- **Save drafts** on your device (localStorage)
- **Export PDF** via Print → Save as PDF
- **Works offline** after first load

## Security

- All data stays in your browser unless you export a PDF yourself
- No accounts, analytics, or third-party scripts
- See [privacy.html](privacy.html) for details

## Local development

```bash
cd /path/to/repo
python3 -m http.server 8080
# Open http://localhost:8080/cushion-order-app/
```

Templates load from `../cushion-order-kit/drawings/svg/` — keep both folders in the repo.
