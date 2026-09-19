# Atlas BTC bots — plain English

You asked for **every timeframe** and **every serious indicator/trigger**. We ran **792 backtests** (see `TIMEFRAME_MATRIX.md` and `scripts/full_matrix_results.csv`).

## Which file to use?

| Bot | When to use |
|-----|-------------|
| **`AtlasBtcOmniBot.cs`** | **Start here.** One bot, **24 triggers**, any chart from **M15 to Daily**, optional higher-timeframe filter. |
| `AtlasBtcBreakoutBot.cs` | Simple breakout-only version (H4). |

---

## Omni bot in 60 seconds

1. **Pick a chart speed** (M15 = fast, H4 = calm, Daily = slow).  
2. Set **Entry trigger = Auto** → the bot picks the trigger that won research **for that timeframe**.  
3. **Higher-TF filter ON** → ignore signals that fight the bigger trend (price vs 55-period average on a higher chart).  
4. Every trade risks about **1%** of equity with **ATR-based** stop and target.

### What “Auto” picks (from full history research)

| Your chart | Trigger | In simple terms |
|------------|---------|-----------------|
| **M15** | EMA 50/200 cross | Only when slow trend agrees |
| **M30** | Volume spike + break | Big volume + new high/low |
| **H1** | CCI crosses ±100 | Strong momentum push |
| **H2** | MACD cross | Trend acceleration |
| **H4** | ADX + Donchian 20 | Breakout in a real trend |
| **Daily** | ADX rising + Donchian 20 | Best long-run combo on 2014+ data |

You can override **Auto** and choose any trigger from the dropdown (Donchian, RSI, Bollinger, Ichimoku, Stochastic, etc.).

---

## All 24 triggers (what they mean)

| Trigger | Normal person description |
|---------|---------------------------|
| Donchian 20 / 55 | Price breaks its recent range (new high or low) |
| ADX + Donchian | Same, but only if trend strength is high enough |
| ADX rising + break | Breakout while trend is **getting** stronger |
| EMA crosses | Faster average crosses slower → trend change |
| Triple EMA pullback | Strong trend stack; buy the dip to middle average |
| MACD cross / histogram | Momentum indicator flips direction |
| RSI pullback | In a big trend, buy when RSI was oversold and turns up |
| RSI 50 cross | Momentum crosses the middle line |
| Bollinger breakout / fade | Break outer band OR bounce from outer band |
| Keltner breakout | Similar to Bollinger, ATR-based channel |
| Stochastic cross | Short-term oscillator cross in trend zone |
| CCI ±100 | Commodity Channel Index momentum burst |
| DI cross | Buyers vs sellers strength flip |
| ROC momentum | Rate-of-change turns positive/negative |
| Ichimoku TK cross | Tenkan/Kijun cross (Japanese trend system) |
| Volume spike break | Unusual volume + range break |

Research finding: **breakout + trend/momentum** triggers beat **mean reversion** on BTC most of the time.

---

## Timeframes tested

| TF | Data length | Notes |
|----|-------------|--------|
| M15, M30 | 60 days | Short sample (Yahoo limit) — use demo to confirm |
| H1 | ~2 years | Good intraday sample |
| H2, H4 | Resampled from H1 | Same window as H1 |
| Daily | 2014 → today | Best for long-term stats |

Re-run research: `python3 scripts/full_matrix_research.py`

---

## cTrader steps

1. Algo → New cBot → paste **`AtlasBtcOmniBot.cs`** → Build.  
2. Open **BTCUSD**, choose timeframe.  
3. **Entry trigger: Auto**, **HTF filter: true**, demo first.

---

## Honest expectations

- No trigger wins on **every** timeframe. That’s why **Auto** exists.  
- Past CSV results ≠ future live fills.  
- **M15/M30** numbers used only 60 days of data — treat as **hints**, not proof.
