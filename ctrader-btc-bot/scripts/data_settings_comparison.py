#!/usr/bin/env python3
"""
Compare Learned Autopilot-style sim under cTrader-like data assumptions.

We cannot run cTrader backtests here; this proxies:
  - M1 bars from server  → M15 OHLC with High/Low for SL/TP (baseline for M15 TF)
  - Tick / sub-bar       → walk 5m (or 1m) bars inside each M15 bar for fills
  - H1 bars from server  → close-only exits (optimistic / wrong for intrabar stops)
  - Spread fixed/random  → added to entries/exits on baseline

Usage: python3 data_settings_comparison.py
"""

from __future__ import annotations

import copy
from dataclasses import dataclass

import numpy as np
import pandas as pd
import yfinance as yf

from full_matrix_research import STRATEGIES, apply_htf_filter, atr
from weekly_prop_sim import run_sim_max_hold


@dataclass
class ModeResult:
    mode: str
    ret_pct: float
    max_dd_pct: float
    trades: int
    green_week_pct: float
    profit_factor: float
    notes: str


def resample_ohlc(df: pd.DataFrame, rule: str) -> pd.DataFrame:
    return (
        df.resample(rule)
        .agg({"Open": "first", "High": "max", "Low": "min", "Close": "last", "Volume": "sum"})
        .dropna()
    )


def autopilot_signals(m15: pd.DataFrame, h4: pd.DataFrame) -> pd.DataFrame:
    base = STRATEGIES["ema_50_200"](m15)
    return apply_htf_filter(m15, h4, base)


def run_sim_close_only(
    df: pd.DataFrame,
    signals: pd.DataFrame,
    *,
    max_hold_bars: int,
    atr_sl: float = 2.5,
    rr: float = 2.5,
    risk: float = 0.008,
) -> dict:
    """Simulates coarse bar data (e.g. H1) — SL/TP only checked on Close."""
    equity = 50_000.0
    peak = equity
    max_dd = 0.0
    pos = None
    week_start_eq = equity
    week_idx = None
    weekly_pnls: list[float] = []
    trades = 0
    pnls: list[float] = []
    atr_v = atr(df)
    week_dd_limit = 0.035

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
            c = row["Close"]
            if pos["side"] == "long":
                if c <= pos["sl"]:
                    exit_p = pos["sl"]
                elif c >= pos["tp"]:
                    exit_p = pos["tp"]
                elif pos["hold"] >= max_hold_bars:
                    exit_p = c
            else:
                if c >= pos["sl"]:
                    exit_p = pos["sl"]
                elif c <= pos["tp"]:
                    exit_p = pos["tp"]
                elif pos["hold"] >= max_hold_bars:
                    exit_p = c
            if exit_p is not None:
                sign = 1 if pos["side"] == "long" else -1
                pnl = (exit_p - pos["entry"]) * pos["units"] * sign
                equity += pnl
                pnls.append(pnl)
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
                pos = {"side": "long", "entry": entry, "sl": sl, "tp": entry + rr * r, "units": equity * risk / r, "hold": 0}
            elif bool(signals["short"].iloc[i]):
                entry = row["Close"]
                sl = entry + atr_sl * av
                r = sl - entry
                pos = {"side": "short", "entry": entry, "sl": sl, "tp": entry - rr * r, "units": equity * risk / r, "hold": 0}

        peak = max(peak, equity)
        max_dd = max(max_dd, (peak - equity) / peak)

    if week_idx is not None:
        weekly_pnls.append(equity - week_start_eq)
    w = np.array(weekly_pnls)
    gp = sum(p for p in pnls if p > 0)
    gl = -sum(p for p in pnls if p < 0)
    return {
        "ret_pct": (equity / 50_000.0 - 1) * 100,
        "max_dd_pct": max_dd * 100,
        "trades": trades,
        "green_week_pct": (w > 0).sum() / max(1, len(w)) * 100,
        "profit_factor": gp / max(1e-9, gl),
    }


def run_sim_with_spread(
    df: pd.DataFrame,
    signals: pd.DataFrame,
    *,
    spread_usd: float | None = None,
    spread_pct_range: tuple[float, float] | None = None,
    max_hold_bars: int,
    atr_sl: float = 2.5,
    rr: float = 2.5,
    risk: float = 0.008,
) -> dict:
    """Baseline OHLC sim with half-spread paid on entry and exit (USD per side)."""
    rng = np.random.default_rng(42)
    equity = 50_000.0
    peak = equity
    max_dd = 0.0
    pos = None
    week_start_eq = equity
    week_idx = None
    weekly_pnls: list[float] = []
    trades = 0
    pnls: list[float] = []
    atr_v = atr(df)
    week_dd_limit = 0.035

    def spread_for_trade() -> float:
        if spread_pct_range:
            lo, hi = spread_pct_range
            return float(rng.uniform(lo, hi))
        return spread_usd or 0.0

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
                spread = spread_for_trade()
                units = pos["units"]
                pnl = (exit_p - pos["entry"]) * units * sign - spread * units * 0.5
                pnl -= pos.get("entry_spread", 0)  # entry half already in pos
                equity += pnl
                pnls.append(pnl)
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
                es = spread_for_trade() * (equity * risk / r) * 0.5
                pos = {
                    "side": "long",
                    "entry": entry,
                    "sl": sl,
                    "tp": entry + rr * r,
                    "units": equity * risk / r,
                    "hold": 0,
                    "entry_spread": es,
                }
                equity -= es
            elif bool(signals["short"].iloc[i]):
                entry = row["Close"]
                sl = entry + atr_sl * av
                r = sl - entry
                es = spread_for_trade() * (equity * risk / r) * 0.5
                pos = {
                    "side": "short",
                    "entry": entry,
                    "sl": sl,
                    "tp": entry - rr * r,
                    "units": equity * risk / r,
                    "hold": 0,
                    "entry_spread": es,
                }
                equity -= es

        peak = max(peak, equity)
        max_dd = max(max_dd, (peak - equity) / peak)

    if week_idx is not None:
        weekly_pnls.append(equity - week_start_eq)
    w = np.array(weekly_pnls)
    gp = sum(p for p in pnls if p > 0)
    gl = -sum(p for p in pnls if p < 0)
    return {
        "ret_pct": (equity / 50_000.0 - 1) * 100,
        "max_dd_pct": max_dd * 100,
        "trades": trades,
        "green_week_pct": (w > 0).sum() / max(1, len(w)) * 100,
        "profit_factor": gp / max(1e-9, gl),
    }


def run_sim_subbars(
    m15: pd.DataFrame,
    sub: pd.DataFrame,
    signals: pd.DataFrame,
    *,
    max_hold_bars: int,
    atr_sl: float = 2.5,
    rr: float = 2.5,
    risk: float = 0.008,
) -> dict:
    """Walk sub-timeframe bars (5m/1m) for fills — closer to tick replay on M15 logic."""
    equity = 50_000.0
    peak = equity
    max_dd = 0.0
    pos = None
    week_start_eq = equity
    week_idx = None
    weekly_pnls: list[float] = []
    trades = 0
    pnls: list[float] = []
    atr_v = atr(m15)
    week_dd_limit = 0.035
    m15_index = m15.index

    for i in range(250, len(m15)):
        ts = m15_index[i]
        iso = (ts.isocalendar().year, ts.isocalendar().week)
        if week_idx != iso:
            if week_idx is not None:
                weekly_pnls.append(equity - week_start_eq)
            week_idx = iso
            week_start_eq = equity

        week_dd = (week_start_eq - equity) / week_start_eq if week_start_eq > 0 else 0
        allow_entry = week_dd < week_dd_limit

        bar_start = ts
        bar_end = ts + pd.Timedelta(minutes=15)
        sub_slice = sub[(sub.index >= bar_start) & (sub.index < bar_end)]

        if pos:
            pos["hold"] += 1
            exit_p = None
            for _, srow in sub_slice.iterrows():
                if pos["side"] == "long":
                    if srow["Low"] <= pos["sl"]:
                        exit_p = pos["sl"]
                        break
                    if srow["High"] >= pos["tp"]:
                        exit_p = pos["tp"]
                        break
                else:
                    if srow["High"] >= pos["sl"]:
                        exit_p = pos["sl"]
                        break
                    if srow["Low"] <= pos["tp"]:
                        exit_p = pos["tp"]
                        break
            if exit_p is None and pos["hold"] >= max_hold_bars:
                exit_p = m15.iloc[i]["Close"]
            if exit_p is not None:
                sign = 1 if pos["side"] == "long" else -1
                pnl = (exit_p - pos["entry"]) * pos["units"] * sign
                equity += pnl
                pnls.append(pnl)
                trades += 1
                pos = None

        if pos is None and allow_entry and i > 0:
            av = atr_v.iloc[i]
            if av <= 0 or np.isnan(av):
                continue
            sig_i = i
            if bool(signals["long"].iloc[sig_i]):
                entry = m15.iloc[i]["Close"]
                sl = entry - atr_sl * av
                r = entry - sl
                pos = {"side": "long", "entry": entry, "sl": sl, "tp": entry + rr * r, "units": equity * risk / r, "hold": 0}
            elif bool(signals["short"].iloc[sig_i]):
                entry = m15.iloc[i]["Close"]
                sl = entry + atr_sl * av
                r = sl - entry
                pos = {"side": "short", "entry": entry, "sl": sl, "tp": entry - rr * r, "units": equity * risk / r, "hold": 0}

        peak = max(peak, equity)
        max_dd = max(max_dd, (peak - equity) / peak)

    if week_idx is not None:
        weekly_pnls.append(equity - week_start_eq)
    w = np.array(weekly_pnls)
    gp = sum(p for p in pnls if p > 0)
    gl = -sum(p for p in pnls if p < 0)
    return {
        "ret_pct": (equity / 50_000.0 - 1) * 100,
        "max_dd_pct": max_dd * 100,
        "trades": trades,
        "green_week_pct": (w > 0).sum() / max(1, len(w)) * 100,
        "profit_factor": gp / max(1e-9, gl),
    }


def main() -> None:
    t = yf.Ticker("BTC-USD")
    m15_native = t.history(period="60d", interval="15m")
    m5 = t.history(period="60d", interval="5m")
    m1 = t.history(period="7d", interval="1m")
    h1 = t.history(period="60d", interval="1h")
    h4 = resample_ohlc(h1, "4h")
    m15_from_5m = resample_ohlc(m5, "15min")

    sig_native = autopilot_signals(m15_native, h4)
    sig_from_5m = autopilot_signals(m15_from_5m, h4)

    hold = 48
    params = dict(max_hold_bars=hold, atr_sl=2.5, rr=2.5, risk=0.008)

    results: list[ModeResult] = []

    r = run_sim_max_hold(m15_native, sig_native, **params)
    results.append(
        ModeResult(
            "M15_OHLC (cTrader M1 bars — recommended for M15 TF)",
            r["ret_pct"],
            r["max_dd_pct"],
            r["trades"],
            r["green_week_pct"],
            r["profit_factor"],
            f"{len(m15_native)} bars, ~60d Yahoo",
        )
    )

    r = run_sim_subbars(m15_native, m5, sig_native, **params)
    results.append(
        ModeResult(
            "M15 + 5m sub-bars (tick-ish fill order)",
            r["ret_pct"],
            r["max_dd_pct"],
            r["trades"],
            r["green_week_pct"],
            r["profit_factor"],
            "Same 60d window",
        )
    )

    # 7d overlap only for 1m
    overlap_start = max(m15_native.index[0], m1.index[0])
    m15_7d = m15_native[m15_native.index >= overlap_start]
    sig_7d = autopilot_signals(m15_7d, h4)
    r_base_7d = run_sim_max_hold(m15_7d, sig_7d, **params)
    r_sub_1m = run_sim_subbars(m15_7d, m1, sig_7d, **params)
    results.append(
        ModeResult(
            "7d M15 OHLC baseline",
            r_base_7d["ret_pct"],
            r_base_7d["max_dd_pct"],
            r_base_7d["trades"],
            r_base_7d["green_week_pct"],
            r_base_7d["profit_factor"],
            "Overlap window for 1m test",
        )
    )
    results.append(
        ModeResult(
            "7d M15 + 1m sub-bars (closest to tick)",
            r_sub_1m["ret_pct"],
            r_sub_1m["max_dd_pct"],
            r_sub_1m["trades"],
            r_sub_1m["green_week_pct"],
            r_sub_1m["profit_factor"],
            "7d only (Yahoo 1m limit)",
        )
    )

    r = run_sim_close_only(m15_native, sig_native, **params)
    results.append(
        ModeResult(
            "M15 close-only exits (bad: like H1 data on M15 bot)",
            r["ret_pct"],
            r["max_dd_pct"],
            r["trades"],
            r["green_week_pct"],
            r["profit_factor"],
            "Do not use H1 bars for this bot",
        )
    )

    r = run_sim_with_spread(m15_native, sig_native, spread_usd=25.0, **params)
    results.append(
        ModeResult(
            "M15 OHLC + fixed $25 spread/side",
            r["ret_pct"],
            r["max_dd_pct"],
            r["trades"],
            r["green_week_pct"],
            r["profit_factor"],
            "M1 bars + fixed spread (not tick historical spread)",
        )
    )

    r = run_sim_with_spread(m15_native, sig_native, spread_pct_range=(15.0, 45.0), **params)
    results.append(
        ModeResult(
            "M15 OHLC + random $15–45 spread/side",
            r["ret_pct"],
            r["max_dd_pct"],
            r["trades"],
            r["green_week_pct"],
            r["profit_factor"],
            "Random spread setting in cTrader",
        )
    )

    r = run_sim_max_hold(m15_from_5m, sig_from_5m, **params)
    results.append(
        ModeResult(
            "M15 built from 5m resample (data source check)",
            r["ret_pct"],
            r["max_dd_pct"],
            r["trades"],
            r["green_week_pct"],
            r["profit_factor"],
            "Should track native M15 closely",
        )
    )

    print("=== Learned Autopilot data-settings proxy test ===")
    print("Strategy: EMA 50/200 + H4 EMA55 filter | hold=48 | 2.5 ATR SL | 2.5R\n")
    print(f"{'Mode':<52} {'Ret%':>7} {'MaxDD%':>7} {'Trades':>6} {'GreenWk%':>8} {'PF':>5}")
    print("-" * 90)
    for m in results:
        print(
            f"{m.mode:<52} {m.ret_pct:7.2f} {m.max_dd_pct:7.2f} {m.trades:6d} "
            f"{m.green_week_pct:8.1f} {m.profit_factor:5.2f}  | {m.notes}"
        )

    baseline = results[0].ret_pct
    tickish = results[1].ret_pct
    delta = tickish - baseline
    print("\n--- Verdict (proxy) ---")
    print(f"Tick/sub-bar vs M15 OHLC (60d): {delta:+.2f}% return, {results[1].trades - results[0].trades:+d} trades")
    print(
        "cTrader: use **M1 bars from server** minimum (not H1). "
        "**Tick data** mainly refines stops/fills; for this M15 bot expect modest drift, not a new strategy."
    )
    print(
        "Longer **history** needs broker/server M15 or CSV — Yahoo caps ~60d; "
        "better data helps *confidence*, not weekly re-opt (still loses vs fixed stack)."
    )

    out = pd.DataFrame([m.__dict__ for m in results])
    out.to_csv("data_settings_comparison_results.csv", index=False)
    print("\nWrote data_settings_comparison_results.csv")


if __name__ == "__main__":
    main()
