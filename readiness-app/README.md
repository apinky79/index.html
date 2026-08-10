# Readiness Check

Hume-style daily mind & body report for Apple Watch Series 6 + Bevel, HRV, Sleep Cycle, and OutPerform.

**Upload screenshots → get one clear health report.** No interpreting eight separate app scores yourself.

## Quick start (iPhone)

Open in Safari: **https://apinky79.github.io/index.html/readiness-app/**

Tap **Add to Home Screen** for app-like access.

## Daily workflow

1. Each morning, screenshot your Bevel, HRV, OutPerform, and Sleep Cycle dashboards
2. Open the app → **Scan** tab → add screenshots
3. Tap **Analyse screenshots** → review extracted numbers
4. Select how you feel → **Generate report**
5. **Report** tab shows your mind & body state, training guidance, and priorities

## Screenshot analysis

| Method | Accuracy | Privacy |
|---|---|---|
| **AI (optional)** | Best — reads app layouts reliably | Requires OpenAI API key in Settings; screenshots sent to OpenAI only when you tap Analyse |
| **OCR fallback** | Basic — may miss numbers on dark UIs | Stays entirely on your device |

Add an OpenAI API key in **Settings** for reliable screenshot reading (~$0.01/day).

## Apple Health

Web apps **cannot** link directly to Apple Health (HealthKit requires a native iOS app).

**Workaround:** iPhone **Settings → Health → Export All Health Data** → unzip in Files → upload `export.xml` in Settings. Parsed locally on your device — nothing uploaded.

Imports: HRV, resting HR, VO2 max, sleep duration.

## Secure sharing

Share reports with people you choose:

1. Set a passphrase in **Settings**
2. **Export encrypted** → send the file (email, AirDrop, etc.)
3. Share the passphrase separately (Signal, in person)
4. Recipient uses **Import encrypted** with the passphrase

Only someone with the passphrase can read the data. No cloud account, no public links.

## Mind & body report

Each report includes:

- **Body state** — HRV, sleep, resting HR, recovery, strain
- **Mind state** — stress, subjective feel, sleep impact on focus
- **Overall readiness** — single 1–10 score vs your personal baseline
- **Training** — Rest / Easy / Moderate / Hard
- **Priorities** — 1–2 practical actions for the day

## Privacy

- All data stored in your browser (localStorage)
- No account, no server, no tracking
- Apple Health export parsed locally
- Screenshot AI is opt-in (your API key, your choice to analyse)
- Encrypted export uses AES-GCM + PBKDF2 in the browser

## Manual entry

Still supported via **Log** tab or paste template if you prefer typing numbers.

## Future

- Native iOS app for direct HealthKit read access
- Automatic screenshot import via Shortcuts
- Shared family/coach dashboards with proper auth backend

## Important

Recovery scores are not medical diagnoses. Compare metrics to **your own trends**, not universal HRV norms.
