# Complete BTC/USD matrix — all timeframes × all triggers

**Updated run:** 11 timeframes × **33 triggers** × optional HTF filter × 3 stop/target pairs → **~1,900+** simulated combinations.

Regenerate:

```bash
python3 scripts/full_matrix_research.py
# → scripts/full_matrix_results.csv
```

## Timeframes included

| TF | History (Yahoo) | Role |
|----|-----------------|------|
| **M1** | 7 days | Scalping — short sample, demo-validate |
| **M3** | Resampled from M1 | Between M1 and M5 |
| **M5** | 60 days | Intraday |
| **M15** | 60 days | Intraday |
| **M30** | 60 days | Intraday |
| **H1** | ~730 days | Core intraday |
| **H2** | Resampled from H1 | |
| **H3** | Resampled from H1 | |
| **H4** | Resampled from H1 | |
| **D1** | 2014 → today | Long-term edge |
| **W1** | Weekly from daily | Slow swing |

## All 33 triggers tested

Donchian 10/20/55 · ADX+Donchian (20/55/strict) · ADX rising break · EMA 9/21, 12/26, 21/55, 50/200 · SMA 50/200 · Triple EMA pullback · MACD cross & histogram · RSI pullback & RSI 50 · Bollinger break & fade · Keltner · Stochastic · CCI ±100 · DI cross · ROC 12 · SuperTrend flip · Ichimoku TK · Volume spike · **Williams %R** · **Aroon 25** · **Parabolic SAR** · **MFI reversal**

## Auto mode mapping (`AtlasBtcOmniBot.cs`)

| Chart | Auto trigger |
|-------|----------------|
| M1 | EMA 9/21 |
| M3 | Donchian 10 |
| M5 | ADX Donchian 20 |
| M15 | EMA 50/200 |
| M30 | Volume spike break |
| H1 | CCI ±100 |
| H2 | MACD cross |
| H3 | ADX Donchian 55 |
| H4 | ADX Donchian 20 |
| Daily | ADX rising + Donchian 20 |
| Weekly | Donchian 55 |

You can override **Auto** and pick **any** trigger in the bot parameters.

## Best per timeframe (latest research top line)

| TF | Best combo (short label) | Return | Max DD | PF |
|----|--------------------------|--------|--------|-----|
| M1 | See CSV — fast TF noisy | varies | varies | varies |
| M3 | Williams %R + HTF | ~36% | ~3% | ~3.3 |
| M5 | See CSV top rows | | | |
| M15 | EMA 50/200 + HTF | ~18% | ~4% | ~2.25 |
| M30 | Volume spike + HTF | ~17% | ~7% | ~1.78 |
| H1 | CCI + HTF | ~152% | ~14% | ~1.43 |
| H2 | MACD cross | ~74% | ~8% | ~1.42 |
| H3 | SMA 50/200 | ~14% | ~7% | ~1.76 |
| H4 | ADX Donchian 20 | ~33% | ~5% | ~1.86 |
| D1 | Volume spike + HTF / ADX rising break | ~56–139% | ~4–5% | ~2.2–3.2 |
| W1 | Donchian 55 | ~10% | ~3% | ~4.1 |

Full ranked lists: open **`scripts/full_matrix_results.csv`** and filter column `timeframe`.

## cTrader

Use **`AtlasBtcOmniBot.cs`** on **whatever chart TF you trade** — not H4 only.

- **Entry trigger:** Auto (table above) or manual  
- **Higher-TF EMA55 filter:** ON for M1–H1 (research improved many combos)

## Limits

- M1/M3/M5/M15/M30: limited free history → **confirm on broker demo**.  
- Simulation ≠ live spread/slippage.  
- Some cTrader indicator API names may differ by version (Aroon, Williams, MFI) — adjust if build fails.
