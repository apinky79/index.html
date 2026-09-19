#!/usr/bin/env python3
"""
Research script for XTXBtcTrendRegimeBot (not run inside cTrader).

Downloads BTC-USD from Yahoo Finance and approximates the same rules:
  - H4 ADX regime with hysteresis
  - EMA pullback entries on H1 (or H4-only mode)
  - ATR stop and fixed R:R

Usage:
  python3 backtest_btc_trend_regime.py
"""

from __future__ import annotations

import numpy as np
import pandas as pd
import yfinance as yf


def ema(s: pd.Series, n: int) -> pd.Series:
    return s.ewm(span=n, adjust=False).mean()


def atr(df: pd.DataFrame, n: int = 14) -> pd.Series:
    h, l, c = df["High"], df["Low"], df["Close"]
    tr = pd.concat([h - l, (h - c.shift()).abs(), (l - c.shift()).abs()], axis=1).max(axis=1)
    return tr.rolling(n).mean()


def adx_series(df: pd.DataFrame, n: int = 14):
    h, l, c = df["High"], df["Low"], df["Close"]
    up, down = h.diff(), -l.diff()
    plus_dm = np.where((up > down) & (up > 0), up, 0.0)
    minus_dm = np.where((down > up) & (down > 0), down, 0.0)
    tr = pd.concat([h - l, (h - c.shift()).abs(), (l - c.shift()).abs()], axis=1).max(axis=1)
    atr_v = tr.ewm(alpha=1 / n, adjust=False).mean()
    plus_di = 100 * pd.Series(plus_dm, index=df.index).ewm(alpha=1 / n, adjust=False).mean() / atr_v
    minus_di = 100 * pd.Series(minus_dm, index=df.index).ewm(alpha=1 / n, adjust=False).mean() / atr_v
    dx = 100 * (plus_di - minus_di).abs() / (plus_di + minus_di).replace(0, np.nan)
    return dx.ewm(alpha=1 / n, adjust=False).mean(), plus_di, minus_di


def resample_ohlc(df: pd.DataFrame, rule: str) -> pd.DataFrame:
    return (
        df.resample(rule)
        .agg({"Open": "first", "High": "max", "Low": "min", "Close": "last", "Volume": "sum"})
        .dropna()
    )


def backtest(
    df_h1: pd.DataFrame,
    *,
    fast: int = 13,
    slow: int = 34,
    adx_enter: float = 25,
    adx_exit: float = 20,
    adx_bars: int = 3,
    atr_sl: float = 2.5,
    rr: float = 2.5,
    risk: float = 0.008,
    start_equity: float = 50_000,
):
    df = df_h1.copy()
    h4 = resample_ohlc(df, "4h")
    adx_h4, pdi_h4, mdi_h4 = adx_series(h4)
    regime = pd.DataFrame({"adx": adx_h4, "pdi": pdi_h4, "mdi": mdi_h4})
    regime = regime.reindex(df.index, method="ffill")
    df["adx_h4"] = regime["adx"]
    df["trend_up"] = regime["pdi"] > regime["mdi"]
    df["trend_dn"] = regime["mdi"] > regime["pdi"]
    df["ema_f"] = ema(df["Close"], fast)
    df["ema_s"] = ema(df["Close"], slow)
    df["atr"] = atr(df)

    confirm = 0
    equity = start_equity
    peak = equity
    max_dd = 0.0
    pos = None
    trades: list[float] = []

    for i in range(max(slow, 200), len(df)):
        adx = df["adx_h4"].iloc[i]
        if adx >= adx_enter:
            confirm = min(confirm + 1, adx_bars)
        elif adx < adx_exit:
            confirm = 0
        can_trade = confirm >= adx_bars

        row = df.iloc[i]
        prev = df.iloc[i - 1]

        if pos:
            exit_p = None
            if pos["side"] == "buy":
                if row["Low"] <= pos["sl"]:
                    exit_p = pos["sl"]
                elif row["High"] >= pos["tp"]:
                    exit_p = pos["tp"]
            else:
                if row["High"] >= pos["sl"]:
                    exit_p = pos["sl"]
                elif row["Low"] <= pos["tp"]:
                    exit_p = pos["tp"]
            if exit_p is not None:
                sign = 1 if pos["side"] == "buy" else -1
                trades.append((exit_p - pos["entry"]) * pos["units"] * sign)
                equity += trades[-1]
                pos = None

        if pos is None and can_trade:
            atr_v = row["atr"]
            if atr_v <= 0 or np.isnan(atr_v):
                continue
            long_ok = row["trend_up"] and row["ema_f"] > row["ema_s"]
            short_ok = row["trend_dn"] and row["ema_f"] < row["ema_s"]
            cross_up = prev["Close"] < prev["ema_f"] and row["Close"] > row["ema_f"]
            cross_dn = prev["Close"] > prev["ema_f"] and row["Close"] < row["ema_f"]
            if long_ok and cross_up:
                entry = row["Close"]
                sl = entry - atr_sl * atr_v
                r_dist = entry - sl
                pos = {
                    "side": "buy",
                    "entry": entry,
                    "sl": sl,
                    "tp": entry + rr * r_dist,
                    "units": equity * risk / r_dist,
                }
            elif short_ok and cross_dn:
                entry = row["Close"]
                sl = entry + atr_sl * atr_v
                r_dist = sl - entry
                pos = {
                    "side": "sell",
                    "entry": entry,
                    "sl": sl,
                    "tp": entry - rr * r_dist,
                    "units": equity * risk / r_dist,
                }

        peak = max(peak, equity)
        max_dd = max(max_dd, (peak - equity) / peak)

    gp = sum(t for t in trades if t > 0)
    gl = -sum(t for t in trades if t < 0)
    pf = gp / max(1e-9, gl)
    return {
        "trades": len(trades),
        "win_rate": sum(1 for t in trades if t > 0) / max(1, len(trades)),
        "final_equity": equity,
        "return_pct": (equity / start_equity - 1) * 100,
        "max_drawdown_pct": max_dd * 100,
        "profit_factor": pf,
    }


def main() -> None:
    daily = yf.Ticker("BTC-USD").history(period="max", interval="1d")
    h1 = yf.Ticker("BTC-USD").history(period="730d", interval="1h")
    print("Daily history:", len(daily), daily.index[0].date(), "→", daily.index[-1].date())
    print("H1 window:", len(h1), h1.index[0], "→", h1.index[-1])
    stats = backtest(h1)
    print("\nDefault bot params on last ~2y H1:")
    for k, v in stats.items():
        print(f"  {k}: {v:.4f}" if isinstance(v, float) else f"  {k}: {v}")


if __name__ == "__main__":
    main()
