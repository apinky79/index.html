# BrightFunded EMA Challenge cBot

Focused cBot for a BrightFunded-style challenge account:

1. **Double EMA cross** = trigger (optional ATR separation to skip chop)  
2. **Price must retest** the Retest EMA before entry  
3. Optional **Trend EMA** + **ADX** filters  
4. **ATR stop**, risk-multiple TP, break-even  
5. Fixed **USD risk** per trade  
6. **Daily loss halt** (default $1,500)

---

## Low-DD H1 preset (use this next)

Goal: fewer, cleaner trend trades so Monte Carlo drawdown can fall under ~8%.

| Parameter | Value | Optimize? |
|---|---|---|
| Timeframe | **H1** | Lock |
| Bot Trade ID | `BF_EMA_H1` | Lock |
| Retest EMA Length | **21** | Lock first test |
| Max Pending Bars | **6** | Lock |
| Fast EMA Length | **21** | Search `13–34` step `1` |
| Slow EMA Length | **55** | Search `55–89` step `1` (always > Fast) |
| Min EMA Separation (ATR) | **0.25** | Search `0.15–0.40` step `0.05` |
| Use Price EMA Filter | **Yes** | Lock ON |
| Trend EMA Length | **89** | Lock (or `89–144` step `55`) |
| Use ADX Trend Filter | **Yes** | Lock ON |
| ADX TimeFrame | **Hour** | Lock |
| ADX Period | **14** | Lock |
| ADX DI Offset | **12** | Lock (stricter than 8) |
| SL Mode | ATR | Lock |
| SL Value | **2.2** | Search `2.0–2.8` step `0.1` |
| TP Mode | RiskMultiplier | Lock |
| TP Value | **2.5** | Lock first test |
| Break Even | RiskMultiplier **1.0** | Lock |
| Trade Risk (USD) | **200** | Lock |
| Daily Loss Limit | **1500** | Lock |
| Window | **26 weeks** | — |
| Spread | **400** | — |
| Capital | **50000** | — |

### Selector gates (unchanged)
- Min trades **8**
- Expected PF **1.5**
- MC DD **8%**
- MC PF **1.20**
- Ignore passes with MC DD shown as **-**

### What success looks like
At least a few passes with **real** MC DD **&lt; 8%** (not `-`) and MC PF **≥ 1.20**.

If this preset still has all MC DD ≥ 10%, the EMA family needs a different entry model entirely.

---

## Install on your PC (cTrader)

1. Download `BrightFundedEmaChallenge.cs` from this folder.
2. On your computer, open:

   `Documents\cAlgo\Sources\Robots\`

3. Create a folder `BrightFundedEmaChallenge` (optional) and copy the `.cs` file into `Robots` (or that subfolder).
4. Open **cTrader → Automate**.
5. Find **BrightFundedEmaChallenge**, click **Build**.
6. Attach to a chart: **BTCUSD**, timeframe **m15**.

---

## Recommended live / challenge defaults

| Parameter | Value |
|---|---|
| Bot Trade ID | `BF_EMA` |
| Order Direction | LongAndShort |
| Retest EMA Length | **12** |
| Max Pending Bars | **12** |
| Fast EMA Length | **8** |
| Slow EMA Length | **21** |
| Use Price EMA Filter | **Yes** |
| Trend EMA Length | **55** |
| Use ADX Trend Filter | **Yes** |
| ADX TimeFrame | **Hour** |
| ADX Period | **14** |
| ADX DI Offset | **8** |
| SL Mode | **ATR** |
| SL Value | **1.5** |
| ATR Period | **14** |
| Break Even Mode | **RiskMultiplier** |
| Break Even Value | **1.0** |
| TP Mode | **RiskMultiplier** |
| TP Value | **2.0** |
| Trade Risk (USD) | **175** |
| Daily Loss Limit (USD) | **1500** |
| Halt On Daily Limit | **Yes** |

For BrightFunded $50k Classic: do **not** push Trade Risk above **$200**.

---

## What to optimize (and step sizes)

Keep the search small. Optimize only these:

### Pass A — EMA structure (main search)

| Parameter | Min | Max | Step | Notes |
|---|---:|---:|---:|---|
| Fast EMA Length | 5 | 21 | **1** or **2** | Must stay **&lt;** Slow |
| Slow EMA Length | 21 | 55 | **2** or **5** | Must stay **&gt;** Fast |
| Trend EMA Length | 34 | 89 | **5** or **8** | Trend filter only |
| Retest EMA Length | 8 | 21 | **1** | Pullback entry level |
| Max Pending Bars | 6 | 20 | **2** | Cancel stale setups |

Suggested tight grid if you want fewer passes:

| Parameter | Min | Max | Step |
|---|---:|---:|---:|
| Fast EMA Length | 8 | 13 | 1 |
| Slow EMA Length | 21 | 34 | 1 |
| Trend EMA Length | 55 | 55 | 0 (lock) |
| Retest EMA Length | 12 | 12 | 0 (lock) |

### Pass B — risk / exits (after Pass A winner)

Lock the EMA winners from Pass A, then search:

| Parameter | Min | Max | Step | Notes |
|---|---:|---:|---:|---|
| SL Value (ATR mult) | 1.2 | 2.2 | **0.1** | ATR stop distance |
| TP Value (R multiple) | 1.5 | 3.0 | **0.5** | Reward:risk |
| Break Even Value | 0.8 | 1.5 | **0.1** | Move SL to BE at XR |
| ADX DI Offset | 4 | 12 | **2** | Ignore weak DI spread |
| ADX Period | 10 | 20 | **2** | Optional |

### Do **not** optimize

- Trade Risk (USD) — set manually for the challenge (`150–200`)
- Daily Loss Limit — keep `1500` for $50k Classic
- Fitness filter thresholds during the search (leave defaults on)
- ADX TimeFrame (keep Hour or Daily fixed)

---

## Fitness filters (leave on while optimizing)

| Filter | Value |
|---|---|
| Use Min Trades | On → **50** |
| Use Min Profit Factor | On → **1.20** |
| Use Max Consecutive Losers | On → **6** |
| Use Max Equity DD % | On → **5.0** |
| Use Max Balance DD % | On → **5.0** |

Optimizer fitness returns **0** if filters fail, otherwise **net profit**.

---

## Backtest / walk-forward process

1. **Optimize** on window A (example: 6–9 months).  
2. Take top 3 passes that pass fitness.  
3. **Forward test** on window B (different dates, no re-optimize).  
4. Only use params that still show PF ≥ 1.2 and DD ≤ 5% on window B.  
5. Demo live for at least 2 weeks at **$175** risk before a challenge attempt.

---

## BrightFunded $50k Classic reminder

| Rule | Number |
|---|---|
| Phase 1 target | $5,000 |
| Phase 2 target | $2,500 |
| Daily loss | $2,500 |
| Max DD (static) | $5,000 (floor $45,000) |

This bot’s daily halt at **$1,500** is an early brake before the firm’s hard $2,500 daily breach.
