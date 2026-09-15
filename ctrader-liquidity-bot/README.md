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

1. Open **cTrader Automate**.
2. Create a new cBot project.
3. Copy **`LiquiditySweepBot.SingleFile.cs`** into the project and **rename it to `LiquiditySweepBot.cs`**.
4. Delete any other `.cs` files from the project (the multi-file version is for development only — do **not** copy those into cTrader).
5. Build the project.
6. Attach to a **BTCUSD** chart (recommended entry TF: **M15**).
7. Enable Algo trading.

> **Important:** Use only the single-file version (`LiquiditySweepBot.SingleFile.cs`). The multi-file layout below is for repository development and is **not** compatible with older cTrader builds when copied as-is.

### Repository layout (development only)

```
ctrader-liquidity-bot/
├── LiquiditySweepBot.SingleFile.cs   # ← COPY THIS into cTrader (rename to LiquiditySweepBot.cs)
├── LiquiditySweepBot.cs              # Multi-file main robot (dev only)
├── Models/LiquidityModels.cs         # Dev only
├── Engine/                           # Dev only
│   ├── SwingPointDetector.cs
│   ├── LiquidityZoneEngine.cs
│   ├── SweepConfirmationEngine.cs
│   ├── ChartVisualizer.cs
│   ├── RiskManager.cs
│   ├── ProfitManager.cs
│   └── SymbolTradingContext.cs
└── README.md
```

## Timeframe settings (set any TF you want)

| Parameter | What it does |
|-----------|--------------|
| **Use Chart Timeframe for Zones** | When `true`, liquidity zones use whatever chart you attach the bot to (e.g. M15, H1, H4, D1) |
| **Liquidity Zone Timeframe** | Used when the above is `false` — pick any cTrader TF (M1, M15, H1, H4, D1, W1, etc.) |
| **Use Chart Timeframe for Entry** | When `true` (default), entries/sweeps run on the chart TF |
| **Entry Timeframe** | Used when the above is `false` — e.g. zones on H4, entries on M15 |

**Examples:**
- Zones + entries both on **M15** → attach to M15, enable both “Use Chart Timeframe” toggles
- Zones on **H4**, entries on **M15** → attach to M15, zone toggle off + set H4, entry toggle off + set M15
- Zones on **D1**, entries on chart → attach to M15, zone TF = Daily, entry uses chart

A badge in the top-left of the chart shows the active liquidity timeframe.

## Chart visuals

When **Draw Zones On Chart** is enabled (default):

| Element | Meaning |
|---------|---------|
| Red zone + line | **BSL** (buy-side liquidity) — equal highs, swing high, PDH/PWH |
| Blue zone + line | **SSL** (sell-side liquidity) — equal lows, swing low, PDL/PWL |
| Label | Side, source (EQH/EQL/PDH…), price, strength score |
| ▲ / ▼ triangle | Sweep detected (`SWEEP` → `SWEEP OK` when confirmed) |
| ↑ / ↓ arrow | Trade entry with dotted SL/TP lines |

Toggle individually: **Show Zone Labels**, **Show Sweep Markers**, **Show Entry Markers**.

## Recommended starting settings (BTCUSD)

| Parameter | Value | Notes |
|-----------|-------|-------|
| Zone Timeframe | H4 | Liquidity map (or enable Use Chart TF) |
| Entry Timeframe | M15 | Execution (or attach bot to M15 chart) |
| SL Type | SweepWick | Stop beyond liquidity sweep wick |
| TP Type | RiskMultiplier | TP Value = 2.0 → 2R |
| Trade Risk (USD) | 400 | Same sizing style as UltimateTrader2026 |
| SL to BE / Trailing SL | Optional | Same types as UltimateTrader (Pips, %, ATR, R-multiple) |
| Draw Zones On Chart | true | Red BSL / blue SSL boxes + level lines |

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
- [cTrader bar events](https://help.ctrader.com/ctrader-algo/how-tos/cbots/handle-bar-events/)
