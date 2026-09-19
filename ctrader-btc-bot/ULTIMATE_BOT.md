# Ultimate BTC Bot — what “best” means here

No bot wins every week. **Ultimate** is the one file that combines everything that scored best in our research:

| Layer | Source | What it does |
|-------|--------|----------------|
| **Entry** | M15 weekly sim | **EMA 50 / 200 cross** (~3 trades/week, not multi-day holds) |
| **Big trend** | H4 matrix | Trade **with** H4 (price vs EMA 55) |
| **Chop filter** | Full history + your prop notes | **Monday:** skip week if **H4 ADX &lt; 20** |
| **Quality** | M15 matrix | No entry if **M15 ADX &lt; 20**; optional **+DI / −DI** must agree |
| **Exit** | Prop sim | **2.5× ATR** stop, **2.5R** target, **48 M15 bars (~12h)** max |
| **Account** | BrightFunded $50k Classic | **0.8%** risk ( **0.4%** funded ), **3.5%** week brake |
| **Extra** | Pro practice | **Breakeven at 1R**, **flat Friday 20:00 UTC** |

## Install

1. cTrader → Algo → new cBot → paste **`UltimateBtcBot.cs`** → Build.  
2. **BTCUSD**, timeframe **M15**, demo first.  
3. Leave defaults unless you are funded (turn on **Funded phase**).

## Sunday (optional)

```bash
python3 scripts/weekly_regime_pick.py
```

If another trigger beats EMA 50/200 for **green weeks**, you can use **`BrightFundedBtcM15Bot.cs`** with that trigger instead. Ultimate stays on the **single best default stack** so you are not flipping settings mid-week.

## Other bots in this folder

| Bot | Use if… |
|-----|---------|
| **UltimateBtcBot** | You want one “done” flagship |
| BrightFundedBtcM15Bot | You weekly-change trigger |
| AtlasBtcOmniBot | You want other timeframes / 33 triggers |

Disclaimer: past research ≠ live results. Demo forward on your broker.
