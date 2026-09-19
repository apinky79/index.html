# Atlas BTC bots (cTrader)

## Learned BTC Autopilot (start here — self-running + optional Telegram)

**`LearnedBtcAutopilotBot.cs`** + **`AUTOPILOT_SETUP.md`** + **`telegram_advisor/`**

## Ultimate BTC Bot

**`UltimateBtcBot.cs`** — one flagship: M15 EMA 50/200 + H4 trend + ADX gates + prop risk + 12h max hold.  
Read **`ULTIMATE_BOT.md`**.

## BrightFunded M15 (prop / weekly regime)

**`BrightFundedBtcM15Bot.cs`** — $50k Classic defaults (0.8% risk, 3.5% week brake, Monday H4 ADX skip, **48-bar max hold ~12h**).  
Guide: **`BRIGHTFUNDED_M15.md`** · Sunday script: `python3 scripts/weekly_regime_pick.py`

## Primary: Omni bot (all timeframes & triggers)

**`AtlasBtcOmniBot.cs`** — 24 entry triggers, M15→Daily, **Auto** mode picks research winner per timeframe.

| Doc | Content |
|-----|---------|
| `TIMEFRAME_MATRIX.md` | Best trigger per TF + top 5 tables |
| `scripts/full_matrix_research.py` | Regenerate 792-test matrix |
| `scripts/full_matrix_results.csv` | Raw ranked results |
| `PLAIN_ENGLISH_GUIDE.md` | Non-technical guide |

## Simple: Breakout-only bot

**ADX-filtered Donchian breakout** on **H4**:

- Long when close breaks above the highest high of the last **55** bars (prior window) while **ADX ≥ 22** and **+DI > −DI**.
- Short is symmetric (optional).
- **Stop:** 2.5 × ATR(14). **Target:** 2.5 × stop distance. **Risk:** 1% of equity per trade.

See `RESEARCH.md` for backtest tables and `PLAIN_ENGLISH_GUIDE.md` for non-technical explanation.

## Install

1. cTrader → **Algo** → **New cBot** → paste `AtlasBtcBreakoutBot.cs`.
2. Build → attach to **BTCUSD** **H4** → **demo** first.

## Files

| File | Role |
|------|------|
| `AtlasBtcBreakoutBot.cs` | **Use this bot** |
| `XTXBtcTrendRegimeBot.cs` | Previous version (EMA pullback) — optional |
| `scripts/strategy_research.py` | History tournament |
| `scripts/backtest_btc_trend_regime.py` | Old EMA bot replay |

## Disclaimer

High risk. Educational only. Demo before live.
