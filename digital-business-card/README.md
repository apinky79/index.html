# Digital business card (Apple Wallet + vCard)

A small toolkit to build a **digital business card** you can keep in **Apple Wallet**, plus a one-tap **vCard** for Contacts.

## Quick start (web)

Open `index.html` in a browser (or run a local server):

```bash
npm run serve
```

Then visit `http://127.0.0.1:8787`.

- Edit your details — preview updates live.
- **Download vCard** — add to Contacts on iPhone immediately.
- **Export Wallet config** — saves `card.config.json` for the pass builder.

## Apple Wallet pass

Wallet passes must be **cryptographically signed** by Apple. See **[WALLET-SIGNING.md](./WALLET-SIGNING.md)** for certificate setup, then:

```bash
npm run build
```

Your signed pass appears in `output/*.pkpass`.

## Project layout

| Path | Purpose |
|------|---------|
| `index.html` | Card editor and Wallet preview |
| `card.config.json` | Your data (gitignored; copy from example) |
| `BusinessCard.pass/` | Pass template + generated icons |
| `scripts/generate-pass.mjs` | Signs and writes `.pkpass` |
| `certs/` | WWDR + pass cert + key (gitignored) |

## Customization

- Colors and text: web form or `card.config.json`
- QR code on the pass: uses `website`, else `mailto:email`, else a truncated vCard
- Back of pass: website, LinkedIn, location, tagline
