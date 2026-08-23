# UltimateTrader2026 — saved optsets

cTrader **Load settings** snapshots for BTCUSD m15 weekly hunt / forward.

## Files

| File | Saved | Notes |
|---|---|---|
| `UltimateTrader2026_with_news_d_BTCUSD_m15_2026-08-23.optset` | 23 Aug 2026 | Latest user snapshot — Vol Trend **OFF**, ADX Momentum H4 ON |

## How to restore

1. cTrader → UltimateTrader2026 → **Parameters** → **Load settings**
2. Pick the `.optset` file from this folder (or copy to your cTrader optsets path)

## Snapshot: 2026-08-23

**Stack**
- Entry: EMATest · EMA **12**
- Double EMA: **Trigger** · Fast **5** / Slow **21**
- VolumeTrend: **Disabled** (user change — was Condition in locked G)
- ADX Momentum: **Condition** · Trending **25** / Ranging **20** · Period **14** · TF **h4**
- ADX Trend: **Condition** · TF **m15** · offset **0**
- News: **90/45** · NFP / FOMC / CPI / CorePCE
- Week DD: **3.5%** · NoNewEntries
- SL: Percent · default **0.7** · opt **0.6→1.0** step **0.1**
- TP: RiskMultiplier · opt **1.8→3.0** step **0.2**
- Trade risk: **$400**
- Exits: all OFF · Fitness filters: all OFF

**Optimiser ticks:** Fast EMA, Slow EMA, SL Value, TP Value (315 combos if all ranges used)

**Diff vs locked G weekly hunt** (`TRADING_RULES.md`): Vol Trend OFF; Fast/Slow EMA still ticked; TP max **3.0** not **2.8**.
