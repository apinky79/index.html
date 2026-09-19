# Your BTC bot — explained without jargon

This bot is built the way a professional quant desk thinks about **Bitcoin**: you cannot reliably guess tomorrow’s price, but you *can* tell when the market is **trending** vs **going nowhere**. It only trades in the first case.

It runs on **cTrader** as a cBot called **XTXBtcTrendRegimeBot**.

---

## The story in one minute

1. **Wait for a real trend**  
   On the **4-hour** chart we measure **ADX** (Average Directional Index). Think of it as a “is Bitcoin actually moving in a direction?” score.  
   - Below **20** → mostly sideways → **the bot sits on its hands**.  
   - Above **25** for a few bars → trend is real → **trading is allowed**.

2. **Only trade in the direction of that trend**  
   We compare two moving averages (13 and 34 periods) and whether buyers or sellers are stronger on H4.

3. **Enter on a pullback**  
   When price dips to the fast average and bounces back through it (for an uptrend), we **buy**. In a downtrend, the mirror logic **sells**.

4. **Know the loss before you click**  
   Stop distance = **2.5 × ATR** (Average True Range — “how wild is Bitcoin lately?”).  
   Take profit = **2.5× that distance** in your favour (reward:risk = 2.5:1).

5. **Size the trade from your account**  
   Default **0.8% of equity** risk per trade (matches your prop-firm notes). If the stop is hit, you lose about that fraction, not a random lot size.

6. **Safety rails**  
   - **One position at a time** (default).  
   - **Weekly drawdown brake** at **3.5%** from Monday’s equity — no new trades, existing trades stay open.  
   - **Monday morning check**: if H4 ADX is still below **20**, skip the whole week (optional but on by default).

---

## What we learned from history (honest version)

We pulled **all available BTC/USD daily data since 2014** and **~2 years of hourly data** and replayed this ruleset in Python (`scripts/backtest_btc_trend_regime.py`).

| Finding | Meaning for you |
|--------|------------------|
| ADX gate cuts **bad chop weeks** | Fewer trades, smoother equity — aligns with your `TRADING_RULES.md` “Test F” |
| Trend + pullback is **not magic** | Win rate is often **35–45%**; edge comes from **winners bigger than losers** |
| No bot is “best ever” on all of BTC history | Buy-and-hold wins many bull years; this bot aims for **controlled risk** on a prop account |
| Defaults (**13/34 EMA, 2.5 ATR stop, 2.5 R:R**) scored well on recent H1 data | Good starting point; **forward-test on demo** before live |

Past performance in a script **does not guarantee** cTrader live fills, spread, or slippage on your broker’s BTCUSD.

---

## How to install on cTrader

1. Open **cTrader** → **Algo** → **New cBot** → name it `XTXBtcTrendRegimeBot`.
2. Replace the template code with `XTXBtcTrendRegimeBot.cs` from this folder.
3. **Build** (hammer icon). Fix any API renames your cTrader version uses (see README troubleshooting).
4. Open **BTCUSD**, set timeframe to **H1** (recommended).
5. Attach the bot, leave defaults, run on **demo** first.
6. In the log you’ll see plain messages like `Market mood: trending` or `Monday gate: SKIP`.

---

## Parameters you might change (and when)

| Parameter | Leave default unless… |
|-----------|------------------------|
| Risk % | You’re **funded** (try **0.4%**) per your rules |
| Use ADX regime gate | You’re experimenting — turning **off** increases trades and drawdown |
| Allow short | Your broker/firm disallows crypto shorts |
| Max hold bars | You want shorter swings (96 H1 bars ≈ 4 days) |

---

## What this bot will **not** do

- Predict news (CPI, FOMC, etc.) — add a manual **news pause** if you need it.  
- Guarantee weekly profit.  
- Replace a **risk manager** — the 3.5% week brake is a helper, not a prop-firm pass by itself.

---

## Mental model (XTX-style)

Professional firms earn money from **small, repeatable edges** plus **strict risk**, not from “calling the top.” This bot is the retail-sized version:

> **Filter chop → trade with trend → risk little → let winners pay for many small losses.**

That’s the whole game in plain English.
