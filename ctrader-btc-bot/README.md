# Atlas BTC Breakout Bot (cTrader)

**New build** from full-history BTC/USD research — not tied to prior prop tests.

## Strategy

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
