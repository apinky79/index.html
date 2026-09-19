# BTC/USD — every timeframe × trigger (research summary)

Source: `scripts/full_matrix_research.py` on Yahoo BTC-USD  
(**M15/M30:** 60 days · **H1:** 730 days · **H4/H2:** resampled from H1 · **D1:** 2014 → today)

792 combinations tested (24 triggers × optional **higher-TF EMA55 filter** × 3 stop/target pairs).

Re-run anytime:

```bash
python3 scripts/full_matrix_research.py
# → scripts/full_matrix_results.csv
```

---

## Best trigger per timeframe (pick this in the Omni bot → **Auto** mode)

| Chart TF | Best trigger (research) | Stop / Target | Why (plain English) |
|----------|-------------------------|---------------|------------------------|
| **M15** | EMA 50/200 cross (+ HTF filter) | 2.0 ATR / 2R | Slow trend alignment; filters noise on fast chart |
| **M30** | Volume spike + range break | 2.5 ATR / 3R | Big volume often starts BTC moves |
| **H1** | CCI crosses ±100 (+ HTF filter) | 2.5 ATR / 2.5R | Strong intraday momentum bursts |
| **H2** | MACD line cross (or histogram flip) | 2.0 ATR / 2R | Classic trend acceleration |
| **H4** | ADX + Donchian 20 breakout | 2.5 ATR / 2.5R | Breakout only when trend is strong |
| **D1** | ADX rising + Donchian 20 | 2.0 ATR / 2R | Best long-history balance (~139% sim, ~5% DD) |

---

## Top 5 per timeframe (full stats)

### M15 (60d sample)
| Trigger | Return | Max DD | PF | Trades |
|---------|--------|--------|-----|--------|
| EMA 50/200 (+HTF) | 18.1% | 3.9% | 2.25 | 28 |
| EMA 21/55 (+HTF) | 23.8% | 6.9% | 1.93 | 41 |
| Donchian 20 (+HTF) | 45.6% | 7.9% | 1.65 | 94 |

### M30
| Trigger | Return | Max DD | PF | Trades |
|---------|--------|--------|-----|--------|
| Volume spike (+HTF) | 16.6% | 6.9% | 1.78 | 32 |
| Donchian 20 (+HTF) | 20.0% | 5.9% | 1.69 | 41 |

### H1
| Trigger | Return | Max DD | PF | Trades |
|---------|--------|--------|-----|--------|
| CCI ±100 (+HTF) | 152% | 13.7% | 1.43 | 347 |
| ADX Donchian 20 | 119% | 11.4% | 1.36 | 337 |

### H2
| Trigger | Return | Max DD | PF | Trades |
|---------|--------|--------|-----|--------|
| MACD cross / hist turn | 74% | 8.0% | 1.42 | 245 |
| ADX Donchian 20 strict (+HTF) | 54% | 5.9% | 1.52 | 130 |

### H4
| Trigger | Return | Max DD | PF | Trades |
|---------|--------|--------|-----|--------|
| ADX Donchian 20 | 33% | 4.9% | 1.86 | 58 |
| EMA 21/55 (+HTF) | 18% | 4.9% | 1.92 | 34 |

### D1 (2014+)
| Trigger | Return | Max DD | PF | Trades |
|---------|--------|--------|-----|--------|
| ADX rising + Donchian 20 | 139% | 4.9% | 2.23 | 139 |
| Donchian 20 | 179% | 7.1% | 1.88 | 198 |
| ADX Donchian 55 | 100% | 4.9% | 2.21 | 115 |

---

## Triggers that show up most in “top 10” lists

1. CCI ±100 (+ HTF filter)  
2. MACD cross / histogram turn  
3. Volume spike breakout  
4. Donchian 20 / 55  
5. ADX + Donchian variants  
6. EMA stacks with HTF filter  

**Mean reversion** (Bollinger fade, RSI50 chop) ranked lower on BTC across most TFs — Bitcoin trends more than it mean-reverts.

---

## How to use in cTrader

Use **`AtlasBtcOmniBot.cs`**:

1. Attach to **any** timeframe (M15 → D1).  
2. Set **Entry trigger** = **Auto** (uses table above) **or** pick any trigger manually.  
3. Turn **Higher-TF EMA55 filter** **ON** for M15–H1 (research improved results).  
4. Demo first — Yahoo data ≠ your broker feed.
