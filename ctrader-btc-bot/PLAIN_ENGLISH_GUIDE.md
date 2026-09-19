# Atlas BTC bots — plain English

You asked for **every timeframe** and **every indicator/trigger**. The latest run tests **11 timeframes** (M1 → Weekly) and **33 triggers** (~**1,900+** combinations). See `TIMEFRAME_MATRIX.md` and `scripts/full_matrix_results.csv`.

## Which file to use?

| Bot | When to use |
|-----|-------------|
| **`AtlasBtcOmniBot.cs`** | **Start here.** One bot, **33 triggers**, any chart **M1 → Weekly**, optional higher-timeframe filter. |
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
| **M1** | EMA 9/21 | Very fast trend cross |
| **M3** | Donchian 10 | Micro range break |
| **M5** | ADX + Donchian 20 | Breakout with trend strength |
| **M15** | EMA 50/200 cross | Only when slow trend agrees |
| **M30** | Volume spike + break | Big volume + new high/low |
| **H1** | CCI crosses ±100 | Strong momentum push |
| **H2** | MACD cross | Trend acceleration |
| **H3** | ADX + Donchian 55 | Slower breakout filter |
| **H4** | ADX + Donchian 20 | Breakout in a real trend |
| **Daily** | ADX rising + Donchian 20 | Best long-run combo on 2014+ data |
| **Weekly** | Donchian 55 | Slow swing breakouts |

You can override **Auto** and choose any trigger from the dropdown (Donchian, RSI, Bollinger, Ichimoku, Stochastic, etc.).

---

## All 33 triggers (what they mean)

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
| SuperTrend flip | Trend line (ATR channel) direction change |
| Williams %R | Bounce from oversold/overbought zones |
| Parabolic SAR flip | Dots flip from below to above price |
| Aroon cross | Which side made a new high/low recently |
| MFI reversal | Money-flow index leaves extreme zone |
| SMA 50/200 | Classic golden/death cross |
| EMA 9/21 | Fast intraday trend cross |
| Donchian 10 | Very short range break |

Research finding: **breakout + trend/momentum** triggers beat **mean reversion** on BTC most of the time.

---

## Timeframes tested

| TF | Data length | Notes |
|----|-------------|--------|
| M1 | 7 days | Demo-validate only |
| M3, M5, M15, M30 | 60 days (M3 from M1) | Short sample — demo on broker |
| H1 | ~2 years | Good intraday sample |
| H2, H3, H4 | Resampled from H1 | Same window as H1 |
| Daily | 2014 → today | Best for long-term stats |
| Weekly | From daily | Few trades; long hold |

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
