# cTrader backtest data settings — does it help?

Tested in-repo (proxy sim, not cTrader itself):  
`python3 scripts/data_settings_comparison.py` → `scripts/data_settings_comparison_results.csv`

**Strategy tested:** Learned Autopilot core (M15 EMA 50/200 + H4 EMA55, 48-bar hold, 2.5 ATR / 2.5R, 0.8% risk).

## Short answer

| Setting | Helps this M15 bot? |
|--------|---------------------|
| **Tick data from server** vs **M1 bars from server** | **Little change** — same trades/return in our 60d proxy (wide ATR stops, bar-close entries). Use tick once to *sanity-check*, not to hunt a new edge. |
| **M1 bars from server** (default) | **Yes — use this** for M15 backtests. |
| **H1 bars from server** | **No — misleading** (simulated ~4.6% lower return vs proper OHLC; intrabar SL/TP wrong). |
| **Fixed / random spread** on M1 bars | **Matters modestly** (~1–2% return drag vs zero spread in sim). **Tick data** embeds historical spread — closer to live than a wrong fixed spread. |
| **More / newer history (broker M15 or CSV)** | **Helps confidence**, not a substitute for weekly re-opt (re-opt still lost vs fixed stack in prior tests). Yahoo free M15 ≈ **60 days** only. |

## What to pick in cTrader (gear icon on Backtesting)

1. **Data:** `M1 bars from server` for day-to-day tests on **M15** charts.  
2. **Optional:** `Tick data from server` on a **short** range to confirm M1 results before live.  
3. **Avoid:** `H1 bars from server` for this bot (too coarse for ATR stops).  
4. **Spread:** If using M1 bars, set spread near your broker’s typical BTCUSD value, or prefer tick mode for spread realism.  
5. **Long history:** Export **M1 CSV** from cTrader/broker and use `M1 bars in CSV file` if you need more than the server’s online window.

## Live trading vs backtest

The **7-day cBot restart** limit is unrelated to backtest data — see `AUTOPILOT_SETUP.md`.

## Disclaimer

Proxy uses Yahoo BTC-USD; your cTrader broker symbol, spread, and server history will differ slightly.
