# SmcBtcSideExperiment / SMC_Testing (cTrader)

**Side experiment only.** Does **not** replace UltimateTrader2026 / Test G.

## Install

1. Open **SMC Testing** in Automate  
2. **Ctrl+A → Delete** (wipe template)  
3. Paste entire `SMC_Testing.cs`  
4. **Build**  
5. Attach **BTCUSD m15** · Bias TF **Hour4**

## What’s included now

### Core SMC
H4 BOS bias → m15 liquidity sweep → CHoCH → order-block retest.

### News pause
- ON by default · PauseBefore **90** / ResumeAfter **45**
- Toggles: NFP / FOMC / CPI / Core PCE  
- Built-in approximate calendar + **Extra Events UTC**  
  Format: `yyyy-MM-dd HH:mm|Title;yyyy-MM-dd HH:mm|Title`  
  Example: `2026-07-29 18:00|FOMC;2026-08-01 12:30|CPI`  
- Blocks **new entries** only (open trades left alone)

### Max hold (required for weekly hunt)
| Param | Value |
|---|---|
| Enable Max Hold | **ON** |
| Max Hold Hours | **24** (match G “max hold 1”) |

Without this, trades often sit open past the opt/forward window → **0 closed trades** + one open PnL, so min-trades hard gate never passes.

### Optimisable SL / TP (match G ranges)
| Param | Default | Optimise in UI |
|---|---|---|
| SL Mode | **Percent** | or OrderBlock |
| SL Percent | 0.7 | **0.6 → 1.0** (set step **0.1** in Optimizer) |
| TP Risk Multiplier | 2.0 | **1.8 → 2.8** (step **0.2**) |

In Optimizer: tick **SL Percent** and **TP Risk Multiplier**, set min/max/step as above (Grid).

### Level-2 order book (DOM)
| Setting | Meaning |
|---|---|
| Use Level-2 Imbalance Filter | OFF by default |
| DOM Levels to Sum | Top N bids/asks |
| Min Bid/Ask Imbalance Ratio | e.g. 1.2 |

**How it works:** before entry, sums top N bid vs ask volume. Long needs bids ≥ asks × ratio; short needs asks ≥ bids × ratio.

**Important limits**
- cTrader can read DOM via `MarketData.GetMarketDepth` (**AccessRights.None** is enough).
- Many **BTC / prop** feeds publish **empty** depth → filter auto-**skips** (won’t block).
- Backtests usually have **no historical DOM** → L2 does nothing in history; only useful live if your broker shows Depth of Market on BTCUSD.
- Check the log on start: `BidLevels=0 AskLevels=0` means no L2 available.

## Shareable guide
- **HTML (open in browser / Print → Save as PDF):** `SMC_Testing_Share_Guide.html`
- **PDF:** `SMC_Testing_Share_Guide.pdf`

Covers news pause, SL/TP opt, Level-2, and what happens when DOM is missing.

## Live Challenge
Stay on **UltimateTrader + ADX H4 (G)** until this beats G on the digger/green panel.
