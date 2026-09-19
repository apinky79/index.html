# Atlas BTC Breakout Bot — explained simply

This is a **brand-new** cTrader bot built after testing many ideas on **all BTC/USD history we could download** (daily data back to **2014**, plus **two years of hourly** data resampled to 4-hour bars).

It does **not** use your older prop-test notes. It uses one clear idea that kept winning in research.

---

## The idea in one sentence

**Buy when Bitcoin breaks above its recent “ceiling” during a strong trend; sell when it breaks below its recent “floor” — otherwise do nothing.**

That is how many professional trend desks trade crypto: **breakout + trend filter**, not guessing tomorrow’s headline.

---

## The three rules

### 1. The “ceiling” and “floor” (Donchian channel)

Look at the last **55 four-hour candles** (about **9 days**).  
- **Ceiling** = highest price in that window (excluding the current candle).  
- **Floor** = lowest price in that window.

If price **closes above the ceiling**, something new is happening → **long**.  
If it **closes below the floor** → **short** (if your broker allows).

### 2. Only when the trend is real (ADX filter)

**ADX** measures *strength*, not direction.  
If ADX is below **22**, the market is usually drifting → **no trade**.

If **+DI > −DI**, bulls are in control (prefer long breakouts).  
If **−DI > +DI**, bears are in control (prefer short breakouts).

### 3. Fixed risk on every trade

- **Stop loss** = **2.5 × ATR** (how much Bitcoin typically moves in a day-ish window).  
- **Take profit** = **2.5 times** that distance (reward:risk **2.5 : 1**).  
- **Position size** = lose about **1% of account equity** if the stop hits.

---

## What the research showed (honest numbers)

We ran a **strategy tournament** (`scripts/strategy_research.py`): Donchian, moving-average crosses, Keltner, RSI pullbacks, Bollinger squeeze, and ADX hybrids.

| Test | Best approach | Rough result |
|------|----------------|--------------|
| **Daily, 2014 → today** | ADX + 55-day breakout | ~**+98%** on $10k sim, **~5%** max drawdown, profit factor **~2.2**, **116** trades |
| **Last ~2 years, H4 bars** | ADX + 55-bar breakout, 2.5 ATR stop | ~**+22%**, **~5%** drawdown, PF **~1.8** |
| **Out-of-sample** (last 35% of days) | Same family of rules | **Positive but modest** — no holy grail |

**Takeaway:** This style fits Bitcoin’s long-run behaviour (long trends + violent breakouts). It **will** have losing streaks. It **will not** win every month. Past simulation ≠ your broker’s spread tomorrow.

---

## cTrader setup

1. **Algo → New cBot** → paste `AtlasBtcBreakoutBot.cs` → **Build**.  
2. Chart: **BTCUSD**, timeframe **H4**.  
3. Start on **demo**.  
4. Defaults match the research winner: Donchian **55**, ADX **22**, stop **2.5×ATR**, target **2.5R**, risk **1%**.

### When you might change settings

| You want… | Try… |
|-----------|------|
| More trades, faster | Donchian **20** (more whipsaw) |
| Less size | Risk **0.5%** |
| Long-only (some firms) | Turn off **Allow short** |

---

## What this bot is NOT

- Not a “predict the news” AI.  
- Not guaranteed profit.  
- Not HFT — it trades **a few times per month** on H4.

---

## Files

| File | Purpose |
|------|---------|
| `AtlasBtcBreakoutBot.cs` | The cBot |
| `scripts/strategy_research.py` | Full history tournament |
| `RESEARCH.md` | Detailed results |

**Old file `XTXBtcTrendRegimeBot.cs`** is kept for reference only — use **Atlas** for the new design.
