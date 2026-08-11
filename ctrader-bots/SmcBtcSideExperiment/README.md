# SmcBtcSideExperiment (cTrader)

**Side experiment only.** Does **not** replace UltimateTrader2026 / Test G (Double EMA + ADX H4).

Lean **Smart Money Concepts** cBot using OHLC only — **no order book / DOM required**.

## What it does

| Step | Timeframe | Logic |
|---|---|---|
| Bias | **H4** (param) | Last BOS direction |
| Sweep | Chart (**m15**) | Wick beyond swing high/low, close back inside |
| CHoCH | Chart | Close breaks opposite swing after sweep |
| Entry | Chart | Retest of order block (last opposing candle) |
| Risk | — | Fixed USD risk, SL beyond OB, TP = RR multiple |
| Brake | — | Optional week DD % (default 3.5%, no new entries) |

## Install in cTrader

**Use `SMC_Testing.cs` if your cBot project is named “SMC Testing”.**

1. Open **cTrader Automate** → your **SMC Testing** cBot  
2. **Select all** in the editor → **Delete** (wipe the default template completely)  
3. Paste the **entire** `SMC_Testing.cs` file  
4. **Build**

If you paste *into* the empty `OnStart` / `OnTick` template you get errors like `CS1022` and `CS0106` (“private is not valid”). That means the class closed too early — replace the whole file instead.

5. Attach to **BTCUSD m15**  
6. **Bias Time Frame = Hour4**  
7. `Trade Risk (USD)` ≈ 400 on a 50k account

## Suggested first A/B (vs G)

Same weeks as Test G kill-zone diggers + one green:

| # | Forward week | Log |
|---|---|---|
| S1 | 24–30 Nov | $ / DD / trades |
| S2 | 8–14 Dec | |
| S3 | 22–28 Dec | |
| S4 | 19–25 Jan | (G green) |
| S5 | 16–22 Feb | |
| S6 | 8–14 Jun | |
| S7 | 20–26 Jul | |

**Pass SMC side experiment if:** sum PnL ≥ G H4 on those weeks **and** it doesn’t wreck G4/G7-type greens.

Until then: **live Challenge stays on G (UltimateTrader + ADX H4).**

## Parameters to leave alone at first

- Require HTF Bias Align = **true**
- Require Liquidity Sweep = **true**
- TP Risk Multiplier = **2.0**
- Week DD Brake = **ON · 3.5%**

## Notes

- This is a **v1 research bot** — expect tuning (pivot strength, sweep ratio, OB rules).
- cTrader backtests are run **by you**; paste stats here to score.
- SMC here ≠ institutional order flow; it’s chart-structure approximations.
