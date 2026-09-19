# BTC/USD strategy research (Atlas bot)

Data sources: Yahoo Finance `BTC-USD`, daily `period=max` (4386 bars, Sep 2014 – Sep 2026), hourly 730d resampled to H4.

Method: bar-by-bar simulation, 1% (daily) or 0.8% (H4) equity risk per trade, ATR stop + fixed R:R, no compounding abuse, entries on signal close.

## Tournament winners (full daily history)

Top risk-adjusted systems on **daily** bars:

| Rank | Strategy | ATR SL | R:R | Return | Max DD | PF | Trades |
|------|----------|--------|-----|--------|--------|-----|--------|
| 1 | Donchian 20 breakout | 2.5 | 3.0 | 88% | 3.9% | 2.36 | 79 |
| 2 | Donchian 20 | 2.0 | 2.0 | 179% | 7.1% | 1.88 | 198 |
| 3 | **ADX + Donchian 55** | **2.0** | **2.0** | **98%** | **4.9%** | **2.18** | **116** |
| 4 | Donchian 55 | 2.0 | 2.0 | 117% | 5.9% | 2.19 | 128 |

**Chosen for Atlas:** **ADX + Donchian 55** — nearly the same edge as raw Donchian 55 with **smoother drawdown** and fewer weak breakouts in chop.

## Out-of-sample (last 35% of daily sample)

| Strategy | Return | Max DD | PF | Trades |
|----------|--------|--------|-----|--------|
| Donchian 55 | 11.2% | 6.8% | 1.57 | 34 |
| ADX + Donchian 55 | 9.1% | 5.9% | 1.59 | 27 |
| Donchian 20 | 6.8% | 3.9% | 1.38 | 25 |

OOS returns are **modest** (as expected). The edge is survival + compounding over **many years**, not doubling every year.

## H4 (730d hourly → 4h bars)

Best stable config:

- Donchian **55**, ADX ≥ **22**, DI filter **on**
- Stop **2.5 × ATR(14)**, target **2.5R**
- ~**+22%** sim, **~4.8%** DD, PF **~1.78**, **55** trades

Aggressive alternative (more trades, similar return on 2y): Donchian **20**, same ADX — **~+26%**, **~3.9%** DD, **61** trades.

Atlas defaults: **55 / 22 / 2.5 / 2.5** (slightly conservative vs 20-bar).

## Reproduce

```bash
pip install yfinance pandas numpy
python3 scripts/strategy_research.py
```

## Limitations

- Yahoo data ≠ every cTrader broker’s BTCUSD feed.
- Simulation assumes stop/target fills at exact prices (real slippage/gaps differ).
- Short side depends on broker/firm rules.
- No transaction costs in base sim — add ~0.05% per side mentally for crypto CFDs.
