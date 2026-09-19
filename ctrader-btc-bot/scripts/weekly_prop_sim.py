#!/usr/bin/env python3
"""M15 (and alt TF) sim with max-hold + weekly PnL — BrightFunded-style."""

from __future__ import annotations

import numpy as np
import pandas as pd
import yfinance as yf

from full_matrix_research import (
    STRATEGIES,
    apply_htf_filter,
    atr,
    run_sim,
)


def run_sim_max_hold(
    df: pd.DataFrame,
    signals: pd.DataFrame,
    *,
    max_hold_bars: int,
    atr_sl: float = 2.0,
    rr: float = 2.0,
    risk: float = 0.008,
    start_equity: float = 50_000.0,
    week_dd_limit: float = 0.035,
) -> dict:
    equity = start_equity
    peak = equity
    max_dd = 0.0
    pos = None
    week_start_eq = equity
    week_idx = None
    weekly_pnls: list[float] = []
    trades = 0
    atr_v = atr(df)

    for i in range(250, len(df)):
        ts = df.index[i]
        iso = (ts.isocalendar().year, ts.isocalendar().week)
        if week_idx != iso:
            if week_idx is not None:
                weekly_pnls.append(equity - week_start_eq)
            week_idx = iso
            week_start_eq = equity

        week_dd = (week_start_eq - equity) / week_start_eq if week_start_eq > 0 else 0
        allow_entry = week_dd < week_dd_limit

        row = df.iloc[i]
        if pos:
            pos["hold"] += 1
            exit_p = None
            if pos["side"] == "long":
                if row["Low"] <= pos["sl"]:
                    exit_p = pos["sl"]
                elif row["High"] >= pos["tp"]:
                    exit_p = pos["tp"]
                elif pos["hold"] >= max_hold_bars:
                    exit_p = row["Close"]
            else:
                if row["High"] >= pos["sl"]:
                    exit_p = pos["sl"]
                elif row["Low"] <= pos["tp"]:
                    exit_p = pos["tp"]
                elif pos["hold"] >= max_hold_bars:
                    exit_p = row["Close"]
            if exit_p is not None:
                sign = 1 if pos["side"] == "long" else -1
                equity += (exit_p - pos["entry"]) * pos["units"] * sign
                trades += 1
                pos = None

        if pos is None and allow_entry:
            av = atr_v.iloc[i]
            if av <= 0 or np.isnan(av):
                continue
            if bool(signals["long"].iloc[i]):
                entry = row["Close"]
                sl = entry - atr_sl * av
                r = entry - sl
                pos = {
                    "side": "long",
                    "entry": entry,
                    "sl": sl,
                    "tp": entry + rr * r,
                    "units": equity * risk / r,
                    "hold": 0,
                }
            elif bool(signals["short"].iloc[i]):
                entry = row["Close"]
                sl = entry + atr_sl * av
                r = sl - entry
                pos = {
                    "side": "short",
                    "entry": entry,
                    "sl": sl,
                    "tp": entry - rr * r,
                    "units": equity * risk / r,
                    "hold": 0,
                }

        peak = max(peak, equity)
        max_dd = max(max_dd, (peak - equity) / peak)

    if week_idx is not None:
        weekly_pnls.append(equity - week_start_eq)

    w = np.array(weekly_pnls)
    green = (w > 0).sum()
    return {
        "final": equity,
        "ret_pct": (equity / start_equity - 1) * 100,
        "max_dd_pct": max_dd * 100,
        "trades": trades,
        "weeks": len(w),
        "green_weeks": int(green),
        "green_week_pct": green / max(1, len(w)) * 100,
        "avg_week_pnl": w.mean() if len(w) else 0,
        "worst_week": w.min() if len(w) else 0,
        "best_week": w.max() if len(w) else 0,
    }


def main():
    t = yf.Ticker("BTC-USD")
    m15 = t.history(period="60d", interval="15m")
    m30 = t.history(period="60d", interval="30m")
    h1 = t.history(period="60d", interval="1h")
    h4 = h1.resample("4h").agg({"Open": "first", "High": "max", "Low": "min", "Close": "last", "Volume": "sum"}).dropna()

    # Top M15 candidates from matrix
    picks = [
        ("ema_50_200", STRATEGIES["ema_50_200"], False),
        ("ema_21_55+HTF", lambda d: apply_htf_filter(d, h4, STRATEGIES["ema_21_55"](d)), False),
        ("keltner+HTF", lambda d: apply_htf_filter(d, h4, STRATEGIES["keltner_break"](d)), False),
        ("aroon+HTF", lambda d: apply_htf_filter(d, h4, STRATEGIES["aroon_cross_25"](d)), False),
        ("di_cross+HTF", lambda d: apply_htf_filter(d, h4, STRATEGIES["di_cross"](d)), False),
        ("triple_ema+HTF", lambda d: apply_htf_filter(d, h4, STRATEGIES["triple_ema_pullback"](d)), False),
    ]

    print("=== M15 max-hold scan (0.8% risk, 3.5% week brake) ===\n")
    rows = []
    for name, fn, _ in picks:
        sig = fn(m15)
        for hold in [16, 32, 48, 64]:
            for atr_sl, rr in [(2.0, 2.0), (2.5, 2.5)]:
                r = run_sim_max_hold(m15, sig, max_hold_bars=hold, atr_sl=atr_sl, rr=rr, risk=0.008)
                score = r["green_week_pct"] * 0.4 + r["ret_pct"] * 0.3 - r["max_dd_pct"] * 0.3
                rows.append((score, name, hold, atr_sl, rr, r))

    rows.sort(key=lambda x: -x[0])
    for score, name, hold, atr_sl, rr, r in rows[:15]:
        print(
            f"{name} hold={hold}bars ({hold//4}h) sl={atr_sl} rr={rr} | "
            f"ret={r['ret_pct']:.1f}% dd={r['max_dd_pct']:.1f}% trades={r['trades']} | "
            f"green weeks {r['green_weeks']}/{r['weeks']} ({r['green_week_pct']:.0f}%) "
            f"avg/wk ${r['avg_week_pnl']:.0f} worst ${r['worst_week']:.0f}"
        )

    print("\n=== Same best configs on M30 vs M15 (hold=32, 2.0/2.0) ===")
    best_name, best_fn = "ema_50_200", STRATEGIES["ema_50_200"]
    for label, df, fn in [
        ("M15", m15, best_fn),
        ("M30", m30, best_fn),
        ("M15_keltner_htf", m15, lambda d: apply_htf_filter(d, h4, STRATEGIES["keltner_break"](d))),
    ]:
        sig = fn(df)
        r = run_sim_max_hold(df, sig, max_hold_bars=32, atr_sl=2.0, rr=2.0, risk=0.008)
        print(label, r)


if __name__ == "__main__":
    main()
