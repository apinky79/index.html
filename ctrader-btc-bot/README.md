# XTX BTC Trend Regime Bot (cTrader)

cAlgo cBot for **BTCUSD**: H4 **regime filter** + H1 **trend pullback** entries, ATR stops, percent-of-equity sizing.

| File | Purpose |
|------|---------|
| `XTXBtcTrendRegimeBot.cs` | cTrader cBot source |
| `PLAIN_ENGLISH_GUIDE.md` | Non-technical explanation |
| `scripts/backtest_btc_trend_regime.py` | Offline research / replay |

Aligned with workspace `TRADING_RULES.md` (ADX gate, 0.8% risk, 3.5% weekly DD brake, single position).

## Quick start

1. Copy `XTXBtcTrendRegimeBot.cs` into cTrader Algo (new cBot).
2. Build and attach to **BTCUSD** **H1** chart.
3. Demo trade until logs and fills match expectations.

## Recommended settings

| Setting | Value |
|---------|--------|
| Chart | BTCUSD **H1** |
| Fast / Slow EMA | 13 / 34 |
| Stop | 2.5 × ATR(14) |
| Take profit | 2.5 × stop distance |
| Risk | 0.8% equity (0.4% funded) |
| ADX gate | On (H4, enter 25 / exit 20, 3 bars) |
| Monday gate | Skip week if H4 ADX < 20 |

## Research

```bash
pip install yfinance pandas numpy
python3 scripts/backtest_btc_trend_regime.py
```

## Troubleshooting

- **`DirectionalMovementSystem` / `ADX`**: On some cTrader builds the property is `ADX` on `DirectionalMovementSystem`; if compile fails, check API docs for your version.
- **Volume too small**: Lower risk slightly or use a account/broker with smaller minimum volume on BTCUSD.
- **Different symbol name**: Some brokers use `BTCUSD`, `BTC/USD`, or `BITCOIN`; set the chart symbol before attaching.

## Disclaimer

Trading crypto is high risk. This is educational software; not financial advice. Test on demo first.
