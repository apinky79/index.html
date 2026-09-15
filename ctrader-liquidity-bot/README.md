# Liquidity Sweep Bot (cTrader)

A multi-timeframe cBot that identifies **liquidity pools**, waits for **ICT-style sweeps** confirmed by price action, and manages trades with **USD / percent risk** and **R-multiple profit** rules.

Designed for **BTCUSD** first, with optional multi-symbol support (ETHUSD, FX majors, etc.).

## Research summary — how liquidity is identified

After reviewing open-source SMC frameworks ([FractalTrader](https://github.com/r464r64r/FractalTrader)), ICT liquidity literature ([LuxAlgo](https://www.luxalgo.com/library/concept/liquidity-sweep/), [JOAT indicators](https://www.tradingview.com/script/hhAt4Q6Z-ICT-Liquidity-Sweep-Structure-JOAT/)), and crypto walk-forward systems using PDH/L sweeps ([CryptoGuardian](https://github.com/Jotanune/CryptoGuardian)), the bot uses a **layered, non-repainting** model:

| Layer | Method | Why |
|-------|--------|-----|
| **1. Swing pivots** | Confirmed local high/low (N bars each side) | Objective structure; stops rest beyond swings |
| **2. Equal highs/lows** | Cluster swings within **ATR × tolerance** | Strongest retail stop pools (EQH/EQL) |
| **3. Session levels** | Previous day/week high & low | Validated on crypto; institutions target obvious levels |
| **4. Zone strength** | Score by source + touch count | Prefer equal levels & session levels over single swings |

**Sweep confirmation** (ICT two-part test):

1. **Run** — wick trades through the pool (buy-side above highs, sell-side below lows).
2. **Failure** — candle **closes back inside** (not a breakout).
3. **Optional MSS** — close breaks prior swing in reversal direction (reduces false sweeps).
4. **Bar delay** — wait N closed bars while price holds the correct side of the level.
5. **Rejection filter** — minimum wick-to-body ratio filters weak closes.

This combination is the most **backtestable** and **broker-agnostic** approach (price-only; no order-book data required).

## Installation

1. Open **cTrader Automate** (requires cTrader **4.8+** for `BarClosed` events).
2. Create a new cBot and copy all `.cs` files from this folder into the project (same namespace structure).
3. Build the project.
4. Attach to a **BTCUSD** chart (recommended entry TF: **M15**).
5. Enable Algo trading.

### File layout

```
ctrader-liquidity-bot/
├── LiquiditySweepBot.cs          # Main robot + parameters
├── Models/LiquidityModels.cs
├── Engine/
│   ├── SwingPointDetector.cs
│   ├── LiquidityZoneEngine.cs
│   ├── SweepConfirmationEngine.cs
│   ├── RiskManager.cs
│   ├── ProfitManager.cs
│   └── SymbolTradingContext.cs
└── README.md
```

## Recommended starting settings (BTCUSD)

| Parameter | Value | Notes |
|-----------|-------|-------|
| Zone Timeframe | H4 | Liquidity map |
| Entry Timeframe | M15 | Execution (matches your existing rules doc) |
| Pivot Bars | 5 | Swing confirmation |
| Equal Level Tolerance | 0.15 × ATR | Scales with BTC volatility |
| Confirmation Bar Delay | 1 | One bar after sweep close |
| Require MSS | true | Structure shift filter |
| Risk Mode | Percent Equity | 0.8% challenge-style |
| Reward:Risk | 2.0 | Standard SMC target |
| Move to BE at 1R | true | |
| Partial close at 1R | 50% | |
| Trailing stop | 0.35% after 1.5R | |

## Multi-pair usage

- **Single pair:** `Trade Chart Symbol Only = true` (default). Attach one instance per symbol, or run on BTCUSD only.
- **Multiple pairs in one instance:** set `Trade Chart Symbol Only = false` and `Extra Symbols = ETHUSD,SOLUSD` (broker symbol names must match exactly).

Each symbol gets its own zone/entry bar subscriptions and position cap (`Max Open Positions Per Symbol`).

## Risk management

| Mode | Behavior |
|------|----------|
| **Fixed USD** | Risk a fixed dollar amount per trade |
| **Percent Equity** | Risk % of current equity (recommended for prop/challenge accounts) |
| **Percent Balance** | Risk % of balance |

Volume is calculated from **stop distance** (beyond sweep wick + ATR buffer), not a fixed lot size.

## Profit management

- Initial **take profit** at configurable **R:R** (default 2R).
- Optional **breakeven** at 1R.
- Optional **partial close** at 1R.
- Optional **trailing stop** (% of price) activated after a minimum R profit.

## Backtesting notes

- Use **M15** chart with bot attached; zone logic runs on H4 via `MarketData.GetBars`.
- Model **realistic spread** — BTC CFD spread varies widely by broker; set `Max Spread (pips)` if needed.
- Session levels (PDH/L) work on **UTC** bar timestamps (`TimeZone = UTC`).
- Optimize **few** parameters: pivot bars, confirmation delay, R:R, min zone strength — not every filter at once.

## Limitations

- **Price-only liquidity** — does not use L2 order book (would need Open API + exchange feed for that layer).
- **Symbol naming** — broker must offer the symbol as `BTCUSD` (or adjust `Extra Symbols`).
- **MSS optional** — disabling MSS increases trade count but also false sweeps in trending breakouts.

## References

- [FractalTrader liquidity module](https://github.com/r464r64r/FractalTrader/blob/main/core/liquidity.py) — equal levels & sweep detection
- [LuxAlgo — Liquidity Sweep concept](https://www.luxalgo.com/library/concept/liquidity-sweep/)
- [cTrader multi-timeframe guide](https://help.ctrader.com/ctrader-algo/how-tos/cbots/code-multitimeframe-strategies/)
- [cTrader BarClosed events](https://help.ctrader.com/ctrader-algo/how-tos/cbots/handle-bar-events/)
