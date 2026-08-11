# BrightFunded Challenge Pace cBot

Faster strategy for Phase 1 / Phase 2 speed — **not** the slow EMA grinder.

## Design

| Piece | Choice | Why |
|---|---|---|
| Style | **N-bar breakout** | More signals than slow EMA retests |
| Timeframe | **m15** (start here) | Throughput without pure noise of m5 |
| Trend filter | EMA50 optional | Cuts some counter-trend breaks |
| SL / TP | ATR SL, **1.5R** TP default | Higher hit rate → faster equity |
| Daily loss lock | **$1500** | Protect BrightFunded daily limit |
| Daily profit lock | **$800** | Bank green days; reduce giveback |
| Max trades/day | **4** | Cap overtrading |
| Fitness | Min net profit **$2500** | Optimizer must find challenge-speed results |

### Challenge maths ($50k Classic)
- Phase 1: **$5,000**
- Phase 2: **$2,500**
- Daily hard: **$2,500** → bot locks earlier at **$1,500**
- Max DD: **$5,000** → keep MC DD ≤ **8%** in Strategy Selector

**Target for a usable pass:** cTrader net profit roughly **$4,000+ in ~12–16 weeks** (or **$5,000+ in ~24 weeks**) *and* Selector MC gates clear.

---

## Install

1. Copy `BrightFundedChallengePace.cs` to `Documents\cAlgo\Sources\Robots\`
2. Automate → Build
3. Attach to **BTCUSD m15** (or try a major FX pair if allowed — often cleaner DD)

---

## Defaults (live / first backtest)

| Parameter | Value |
|---|---|
| Bot Trade ID | `BF_PACE` |
| Breakout Lookback | **20** |
| Require Close Beyond | **Yes** |
| Use EMA Trend Filter | **Yes** |
| Trend EMA Length | **50** |
| Use Min ATR Filter | No |
| SL ATR Multiple | **1.5** |
| TP Risk Multiple | **1.5** |
| Break Even at R | **1.0** |
| Trail ATR | **0** (off first) |
| Trade Risk (USD) | **300** |
| Daily Loss Limit | **1500** |
| Daily Profit Lock | **800** |
| Max Trades Per Day | **4** |

Spread for backtest/opt: **400** (or your BrightFunded-conservative value)  
Capital: **50000**

---

## What to optimize

### Pass A — structure
| Parameter | Min | Max | Step |
|---|---:|---:|---:|
| Breakout Lookback | 12 | 40 | 2 |
| Trend EMA Length | 34 | 89 | 5 |
| SL ATR Multiple | 1.2 | 2.2 | 0.1 |
| TP Risk Multiple | 1.2 | 2.5 | 0.1 |

Keep risk / daily locks fixed while optimizing.

### Pass B — challenge wrapper (optional)
| Parameter | Min | Max | Step |
|---|---:|---:|---:|
| Daily Profit Lock | 500 | 1200 | 100 |
| Max Trades Per Day | 2 | 6 | 1 |

### Fitness (cBot)
Leave ON:
- Min Trades **40**
- Min PF **1.15**
- Min Net Profit **2500** (raise to **4000** if you want only fast passes)
- Max Balance DD % **8**
- Max Consecutive Losers **7**

### Strategy Selector
Same safety gates as before:
- Min trades **8–15**
- Expected PF **1.5**
- MC DD **8%**
- MC PF **1.20**

Then check **cTrader Net profit** on the Top Pick — reject if still ~$2k/24 weeks.

---

## Process

1. Optimize **16–26 weeks** on m15  
2. Upload `.optres` + zip to Strategy Selector  
3. Require **strict-gate** if possible (not only fallback)  
4. Confirm cTrader **Net profit** is challenge-pace  
5. Forward-test on unseen dates  
6. Demo live with daily locks on before challenge  

If MC DD kills everything again, lower Trade Risk to **$200** and re-opt once — don’t loosen MC DD above 8–10%.

---

## vs EMA Challenge bot

| | EMA Challenge | **Challenge Pace** |
|---|---|---|
| Goal | Low DD grind | Phase speed |
| Signals | Few | Many |
| Typical $ / 24w | ~$1–2.5k | Aim **$4k+** |
| Risk wrapper | Daily loss | Daily loss **+ profit lock** + max trades/day |
