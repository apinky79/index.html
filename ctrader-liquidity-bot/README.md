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

A **gold badge** in the top-left shows `drawn: N / detected: M`. If you see the badge but no boxes, scroll/zoom the chart — zones are drawn on the visible bar range.

### Zones not showing?

1. **Re-copy** the latest `LiquiditySweepBot.SingleFile.cs` and rebuild.
2. Confirm **Draw Zones On Chart = true** and the bot is **running** (green play), not just built.
3. **Live chart or visual backtest** — silent backtest does not render drawings.
4. Enable **Debug Zone Drawing (log)** and check the cTrader log:
   - `detected 0` → lower **Min Zone Strength**, ensure zone TF has history (e.g. H4 loaded).
   - `detected N, drawn N` but nothing visible → zoom out on price; BTC zones may be off-screen.
5. Wait ~20 seconds after start — the bot retries drawing while H4 bars load.

## Algo trader preset (recommended)

**Default setup for systematic trading:** leave both timeframe boxes **unchecked** and keep **Apply Algo Preset = Yes**.

| Toggle | Default | Algo behaviour |
|--------|---------|----------------|
| Use Chart TF for Zones | **No** | Liquidity map on **H4** |
| Use Chart TF for Entry | **No** | Sweeps/entries on **M15** |
| Apply Algo Preset | **Yes** | Applies research-backed values below |

When the preset is active, the log prints `=== ALGO TRADER PRESET ACTIVE ===` with the full config.

**Preset values (HTF/LTF liquidity sweep model for BTCUSD):**

| Setting | Value | Rationale |
|---------|-------|-----------|
| Zone TF | H4 | Structural liquidity; filters M15 noise |
| Entry TF | M15 | Clean sweep confirmation without M1 overtrading |
| Pivot bars | 5 | Standard confirmed swing on H4 (~20h each side) |
| Min zone strength | 65 | Prefer EQH/EQL and session levels over weak swings |
| Confirmation delay | 1 bar | No lookahead; one closed M15 bar after sweep |
| MSS required | Yes | Cuts false sweeps in strong trends |
| Max sweep age | 10 M15 bars | ~2.5h; stale setups discarded |
| SL | Sweep wick + 0.05 ATR | Stop beyond liquidity grab |
| TP | 2R | Baseline systematic reward:risk |
| Risk | 0.5% equity | Conservative prop-style default (or set Trade Risk USD) |
| Max spread | 30 pips | Skip wide BTC CFD spreads |
| Session levels | On | PDH/L and PWH/L — validated on crypto |

**Attach the bot to a BTCUSD M15 chart** for best alignment with entry TF.

Check **Use Chart TF** boxes only for experimentation (zones/entries follow whatever chart you attach to).

## Recommended starting settings (BTCUSD)

| Parameter | Value | Notes |
|-----------|-------|-------|
| Apply Algo Preset | Yes | When TF boxes unchecked |
| Zone Timeframe | H4 | Auto-applied by preset |
| Entry Timeframe | M15 | Auto-applied by preset |
| SL Type | SweepWick | Stop beyond liquidity sweep wick |
| TP Type | RiskMultiplier | TP Value = 2.0 → 2R |
| Trade Risk (USD) | 400 | Optional; overrides % risk when > 0 |
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

## Optimisation (cTrader default ranges)

When you open **Optimisation → Parameters**, Min / Max / Step are pre-filled from the code. Use these as a pro algo starting point.

### Pin these (do NOT optimise)

Keeps structure and risk model fixed so you measure **edge**, not curve-fit:

| Parameter | Fixed value |
|-----------|-------------|
| Use Chart TF for Zones / Entry | **No** (H4 / M15 via preset) |
| Apply Algo Preset | **Yes** |
| Use Session Levels | **Yes** |
| SL Type | **SweepWick** |
| TP Type | **RiskMultiplier** |
| Trade Risk (USD) | **0** (use 0.5% equity) |
| Draw Zones On Chart | **No** (faster optimisation) |
| Min Equal Touches | **2** |

### Tick these to optimise (pass 1 — core edge)

| Parameter | Default | Range | Step |
|-----------|---------|-------|------|
| Pivot Bars | 5 | 3 – 8 | 1 |
| Min Zone Strength | 65 | 50 – 80 | 5 |
| Confirmation Bar Delay | 1 | 0 – 3 | 1 |
| TP Value (R-multiple) | 2.0 | 1.5 – 3.0 | 0.25 |
| Require MSS | Yes | True / False | — |

### Optional pass 2 (after pass 1 winner is fixed)

Max Sweep Age, Min Wick/Body Ratio, Stop Buffer ATR — see `LiquiditySweepBot.optimisation.json`.

### Optimisation settings

| Setting | Recommendation |
|---------|----------------|
| Chart | **BTCUSD M15** |
| Method | **Genetic Algorithm** |
| Criteria | **Custom** (uses built-in `GetFitness`) *or* Max **Profit Factor** + Min **Max Equity DD %** |
| Period | ≥ 6 months BTC data |
| Min trades | Ignore passes with &lt; 15 trades |

During optimisation the bot logs `Optimisation mode — sweeping parameter values` and **does not** override swept params with the live algo preset.

Full parameter list: [`LiquiditySweepBot.optimisation.json`](LiquiditySweepBot.optimisation.json)

## No trades in backtest?

1. **Timeframes** — Attach to **M15**. Set *Use Chart TF for Zones/Entry* = **No**, *Apply Algo Preset* = **Yes** (H4 zones → M15 sweeps).
2. **Spread** — If journal never shows orders, set **Max Spread (pips) = 0** to disable the filter (BTC spreads vary by broker pip definition).
3. **Filters** — Preset uses **MSS off** and **min wick/body 0.6** for more signals. Stricter manual settings (MSS on, min strength 65+, wick 0.8) can yield zero trades over short samples.
4. **Diagnostics** — Enable **Log Entry Diagnostics** and read the journal each entry bar: `ready: 0` with `setups pending > 0` means sweeps detected but not confirmed yet; `eligible zones: 0` means loosen *Min Zone Strength* (try 50).
5. **History** — Need enough **H4 + M15** history loaded (months, not days).

## Backtesting notes

- Use **M15** chart with bot attached; zone logic runs on H4 via `MarketData.GetBars`.
- Model **realistic spread** — BTC CFD spread varies widely by broker; set `Max Spread (pips)` to **0** first if unsure, then tighten for live.
- Session levels (PDH/L) work on **UTC** bar timestamps (`TimeZone = UTC`).
- Optimise **few** parameters at a time — see table above.

## Limitations

- **Price-only liquidity** — does not use L2 order book (would need Open API + exchange feed for that layer).
- **Symbol naming** — broker must offer the symbol as `BTCUSD` (or adjust `Extra Symbols`).
- **MSS optional** — disabling MSS increases trade count but also false sweeps in trending breakouts.

## References

- [FractalTrader liquidity module](https://github.com/r464r64r/FractalTrader/blob/main/core/liquidity.py) — equal levels & sweep detection
- [LuxAlgo — Liquidity Sweep concept](https://www.luxalgo.com/library/concept/liquidity-sweep/)
- [cTrader multi-timeframe guide](https://help.ctrader.com/ctrader-algo/how-tos/cbots/code-multitimeframe-strategies/)
- [cTrader bar events](https://help.ctrader.com/ctrader-algo/how-tos/cbots/handle-bar-events/)
