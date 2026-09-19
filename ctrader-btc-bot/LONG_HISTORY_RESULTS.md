# BTC tests from 2000 → now (what we can actually run)

## Data limits (important)

| Period | Data? | Notes |
|--------|-------|--------|
| **2000–2008** | **No** | Bitcoin did not have a public USD market yet. |
| **2009-01-03 → today** | **Yes** | Daily USD from [blockchain.info](https://blockchain.info) → `scripts/btc_usd_daily_full.csv` |
| **M15 from 2000** | **No** | Free feeds only give ~**60 days** of 15m bars. Your **BrightFunded M15 bot** cannot be back-tested to 2000. |

Download / refresh daily data:

```bash
python3 scripts/load_btc_history.py
```

Full long-history test:

```bash
python3 scripts/long_history_walk_forward.py
```

---

## Full sample (2009 → 2026, **daily** chart, $50k start, 0.8% risk)

| Strategy | End equity | Return | Max DD | Profit factor | Trades |
|----------|------------|--------|--------|---------------|--------|
| Donchian 20 | $339,288 | 579% | 6.5% | 1.83 | 599 |
| ADX rising + Donchian 20 | $226,623 | 353% | 5.8% | 2.06 | 409 |
| ADX + Donchian 55 | $223,742 | 347% | 4.6% | 2.25 | 352 |
| EMA 50/200 | $50,579 | 1% | 2.3% | 1.19 | 23 |
| Buy & hold (from 2014) | $5.5M+ | — | — | — | 1 |

*Simulated compounding with 0.8% risk per trade — not the same as buy & hold $50k in 2009.*

---

## Weekly re-optimization (2009 → now, **daily**)

Every **4 weeks**, train on the last **400 days**, pick **#1 of top 10** configs, trade the next week. Compared to **fixed** ADX rising + Donchian 20.

| Approach | Total profit on $50k | End equity |
|----------|----------------------|------------|
| **Weekly re-opt** | **+$1,651 (+3.3%)** | $51,651 |
| **Fixed strategy (no re-opt)** | **+$19,496 (+39%)** | $69,496 |

Same lesson as the M15 test: **weekly hopping underperformed** a single robust rule on long history.

Forward steps: **205** (2011–2026). Logs: `scripts/long_history_walk_forward_log.csv`

---

## M15 tests (still short window only)

```bash
python3 scripts/weekly_walk_forward_opt.py   # ~Jul–Sep 2026, 5 forward weeks
python3 scripts/full_matrix_research.py      # 60d M15 + 2y H1, etc.
```

---

## Takeaway for your BrightFunded M15 plan

- **Long-history edge** lives on **daily trend/breakout** systems, not EMA 50/200 on daily (too few trades).  
- Your **Ultimate / BrightFunded M15** stack is tuned for **recent intraday** sims, not 2009 daily.  
- **Weekly re-opt** did **not** beat fixed rules on either **60d M15** or **2009+ daily**.
