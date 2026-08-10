# Readiness Check

A Hume-style daily recovery assessment for Apple Watch Series 6 + Bevel, HRV, Sleep Cycle, and OutPerform.

**One clear morning check** — not eight separate app scores to interpret yourself.

## What it does

- Accepts manual morning metric entry (or paste from a text template)
- Compares HRV and resting HR to **your personal rolling baseline** (7/14/30 days)
- Weights subjective "how I feel" alongside sensor data
- Produces a single readiness score, training recommendation, and 1–2 daily priorities
- Tracks trends over time in your browser (no account, no server)
- Supports a **calibration period** after travel or routine changes

## What it does NOT do

- Pretend Apple Watch data equals Hume Band hardware
- Treat Bevel Recovery and OutPerform Readiness as equivalent scores
- Label a single HRV number as universally "good" or "bad"
- Replace medical advice

## Quick start

Open `index.html` in a browser (or serve the folder with any static server):

```bash
cd readiness-app
python3 -m http.server 8080
# visit http://localhost:8080
```

## Daily input template

Paste this into the app or type it in chat:

```
Sleep:
HRV:
7-day HRV:
HRV CV:
Recovery:
Resting HR:
Stress:
Yesterday's strain:
How I feel:
```

## Output format

```
HUME-STYLE DAILY CHECK

Overall readiness: 7/10 — GOOD
Training: Moderate
Recovery: …
HRV: … vs your baseline
Sleep: …
Resting HR: …
Stress: …
Yesterday's strain: …
How you feel: …
Today's priority: …
```

## Data storage

All entries live in browser `localStorage`. Use **Settings → Export JSON** for backup.

## Future direction

- HealthKit / Apple Health ingestion (when technically appropriate)
- VO2 max as a long-term fitness trend (not daily readiness)
- Optional sync across devices

## Hardware setup (reference)

| App | Role |
|---|---|
| Bevel | Main recovery / HRV / stress / strain dashboard |
| HRV | Detailed HRV trends and stability (CV) |
| OutPerform | Readiness / performance guidance |
| Sleep Cycle | Sleep quality and duration detail |

Apple Watch = sensor platform. This app = interpretation layer.
