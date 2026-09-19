#!/usr/bin/env python3
"""Strategy tournament on BTC-USD — daily (full history) + H1 (recent)."""

from __future__ import annotations

import warnings
from dataclasses import dataclass
from typing import Callable

import numpy as np
import pandas as pd
import yfinance as yf

warnings.filterwarnings("ignore")


def ema(s: pd.Series, n: int) -> pd.Series:
    return s.ewm(span=n, adjust=False).mean()


def sma(s: pd.Series, n: int) -> pd.Series:
    return s.rolling(n).mean()


def atr(df: pd.DataFrame, n: int = 14) -> pd.Series:
    h, l, c = df["High"], df["Low"], df["Close"]
    tr = pd.concat([h - l, (h - c.shift()).abs(), (l - c.shift()).abs()], axis=1).max(axis=1)
    return tr.rolling(n).mean()


def adx(df: pd.DataFrame, n: int = 14):
    h, l, c = df["High"], df["Low"], df["Close"]
    up, down = h.diff(), -l.diff()
    plus_dm = np.where((up > down) & (up > 0), up, 0.0)
    minus_dm = np.where((down > up) & (down > 0), down, 0.0)
    tr = pd.concat([h - l, (h - c.shift()).abs(), (l - c.shift()).abs()], axis=1).max(axis=1)
    atr_v = tr.ewm(alpha=1 / n, adjust=False).mean()
    pdi = 100 * pd.Series(plus_dm, index=df.index).ewm(alpha=1 / n, adjust=False).mean() / atr_v
    mdi = 100 * pd.Series(minus_dm, index=df.index).ewm(alpha=1 / n, adjust=False).mean() / atr_v
    dx = 100 * (pdi - mdi).abs() / (pdi + mdi).replace(0, np.nan)
    return dx.ewm(alpha=1 / n, adjust=False).mean(), pdi, mdi


@dataclass
class SimResult:
    name: str
    trades: int
    ret_pct: float
    max_dd_pct: float
    pf: float
    calmar: float
    win_rate: float


def run_sim(
    df: pd.DataFrame,
    signals: pd.DataFrame,
    *,
    atr_sl: float = 2.0,
    rr: float = 2.0,
    risk: float = 0.01,
    start_equity: float = 10_000,
    allow_short: bool = True,
) -> SimResult:
    """signals: long_entry, short_entry bool columns aligned to df index."""
    equity = start_equity
    peak = equity
    max_dd = 0.0
    pos = None
    pnls: list[float] = []

    atr_v = atr(df)
    for i in range(200, len(df)):
        row = df.iloc[i]
        if pos:
            exit_p = None
            if pos["side"] == "long":
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
                sign = 1 if pos["side"] == "long" else -1
                pnl = (exit_p - pos["entry"]) * pos["units"] * sign
                equity += pnl
                pnls.append(pnl)
                pos = None

        if pos is None:
            av = atr_v.iloc[i]
            if av <= 0 or np.isnan(av):
                continue
            go_long = bool(signals["long"].iloc[i])
            go_short = allow_short and bool(signals["short"].iloc[i])
            if go_long:
                entry = row["Close"]
                sl = entry - atr_sl * av
                r = entry - sl
                pos = {
                    "side": "long",
                    "entry": entry,
                    "sl": sl,
                    "tp": entry + rr * r,
                    "units": equity * risk / r,
                }
            elif go_short:
                entry = row["Close"]
                sl = entry + atr_sl * av
                r = sl - entry
                pos = {
                    "side": "short",
                    "entry": entry,
                    "sl": sl,
                    "tp": entry - rr * r,
                    "units": equity * risk / r,
                }
        peak = max(peak, equity)
        max_dd = max(max_dd, (peak - equity) / peak)

    gp = sum(p for p in pnls if p > 0)
    gl = -sum(p for p in pnls if p < 0)
    pf = gp / max(1e-9, gl)
    years = max(0.5, (df.index[-1] - df.index[200]).days / 365.25)
    ret_pct = (equity / start_equity - 1) * 100
    cagr = ((equity / start_equity) ** (1 / years) - 1) * 100 if equity > 0 else -100
    calmar = cagr / max(0.01, max_dd * 100)
    wins = sum(1 for p in pnls if p > 0)
    return SimResult(
        name="",
        trades=len(pnls),
        ret_pct=ret_pct,
        max_dd_pct=max_dd * 100,
        pf=pf,
        calmar=calmar,
        win_rate=wins / max(1, len(pnls)),
    )


def donchian(df: pd.DataFrame, n: int = 20) -> pd.DataFrame:
    upper = df["High"].rolling(n).max().shift(1)
    lower = df["Low"].rolling(n).min().shift(1)
    mid = df["Close"]
    sig = pd.DataFrame(index=df.index)
    sig["long"] = mid > upper
    sig["short"] = mid < lower
    return sig.fillna(False)


def ema_cross(df: pd.DataFrame, fast: int, slow: int) -> pd.DataFrame:
    f, s = ema(df["Close"], fast), ema(df["Close"], slow)
    prev_f, prev_s = f.shift(1), s.shift(1)
    sig = pd.DataFrame(index=df.index)
    sig["long"] = (prev_f <= prev_s) & (f > s)
    sig["short"] = (prev_f >= prev_s) & (f < s)
    return sig.fillna(False)


def keltner_breakout(df: pd.DataFrame, ema_n: int = 20, atr_n: int = 14, mult: float = 2.0) -> pd.DataFrame:
    mid = ema(df["Close"], ema_n)
    band = mult * atr(df, atr_n)
    upper, lower = mid + band, mid - band
    c = df["Close"]
    sig = pd.DataFrame(index=df.index)
    sig["long"] = (c.shift(1) <= upper.shift(1)) & (c > upper)
    sig["short"] = (c.shift(1) >= lower.shift(1)) & (c < lower)
    return sig.fillna(False)


def trend_rsi_pullback(df: pd.DataFrame, ema_trend: int = 200, rsi_n: int = 14) -> pd.DataFrame:
    trend = ema(df["Close"], ema_trend)
    delta = df["Close"].diff()
    up = delta.clip(lower=0).rolling(rsi_n).mean()
    down = (-delta.clip(upper=0)).rolling(rsi_n).mean()
    rsi = 100 - 100 / (1 + up / down.replace(0, np.nan))
    c, prev = df["Close"], df["Close"].shift(1)
    sig = pd.DataFrame(index=df.index)
    sig["long"] = (c > trend) & (rsi.shift(1) < 40) & (rsi >= 40)
    sig["short"] = (c < trend) & (rsi.shift(1) > 60) & (rsi <= 60)
    return sig.fillna(False)


def squeeze_breakout(df: pd.DataFrame, bb_n: int = 20, kc_mult: float = 1.5) -> pd.DataFrame:
    c = df["Close"]
    bb_mid = sma(c, bb_n)
    std = c.rolling(bb_n).std()
    bb_u, bb_l = bb_mid + 2 * std, bb_mid - 2 * std
    kc_mid = ema(c, bb_n)
    kc_band = kc_mult * atr(df, bb_n)
    squeeze = (bb_u - bb_l) < (2 * kc_band)
    sig = pd.DataFrame(index=df.index)
    sig["long"] = squeeze.shift(1) & (c > bb_u.shift(1))
    sig["short"] = squeeze.shift(1) & (c < bb_l.shift(1))
    return sig.fillna(False)


def adx_donchian_hybrid(df: pd.DataFrame, dc: int = 55, adx_min: float = 22) -> pd.DataFrame:
    adx_v, pdi, mdi = adx(df)
    upper = df["High"].rolling(dc).max().shift(1)
    lower = df["Low"].rolling(dc).min().shift(1)
    c = df["Close"]
    trending = adx_v >= adx_min
    sig = pd.DataFrame(index=df.index)
    sig["long"] = trending & (c > upper) & (pdi > mdi)
    sig["short"] = trending & (c < lower) & (mdi > pdi)
    return sig.fillna(False)


def turtle_variant(df: pd.DataFrame, entry: int = 20, exit_n: int = 10) -> pd.DataFrame:
    """Breakout entry; not modeling exit channel in sim (ATR stop handles)."""
    return donchian(df, entry)


def score(r: SimResult) -> float:
    if r.trades < 15:
        return -999
    if r.max_dd_pct > 35:
        return -999
    return r.calmar * 0.5 + r.pf * 0.3 + (r.ret_pct / 100) * 0.2


def grid_daily(df: pd.DataFrame) -> list[tuple[float, SimResult, dict]]:
    results = []
    for atr_sl in [1.5, 2.0, 2.5, 3.0]:
        for rr in [2.0, 2.5, 3.0]:
            for name, fn in [
                ("donchian20", lambda d: donchian(d, 20)),
                ("donchian55", lambda d: donchian(d, 55)),
                ("ema12_48", lambda d: ema_cross(d, 12, 48)),
                ("ema21_89", lambda d: ema_cross(d, 21, 89)),
                ("keltner20", lambda d: keltner_breakout(d, 20, 14, 2.0)),
                ("rsi_pullback200", lambda d: trend_rsi_pullback(d, 200, 14)),
                ("squeeze", lambda d: squeeze_breakout(d)),
                ("adx_donchian55", lambda d: adx_donchian_hybrid(d, 55, 22)),
                ("adx_donchian20", lambda d: adx_donchian_hybrid(d, 20, 25)),
            ]:
                sig = fn(df)
                r = run_sim(df, sig, atr_sl=atr_sl, rr=rr, risk=0.01)
                r.name = name
                sc = score(r)
                results.append((sc, r, {"atr_sl": atr_sl, "rr": rr}))
    results.sort(key=lambda x: -x[0])
    return results


def walk_forward_daily(df: pd.DataFrame, sig_fn: Callable, params: dict) -> SimResult:
    split = int(len(df) * 0.65)
    test = df.iloc[split:].copy()
    sig = sig_fn(test)
    r = run_sim(test, sig, **params)
    r.name = "OOS"
    return r


if __name__ == "__main__":
    daily = yf.Ticker("BTC-USD").history(period="max", interval="1d")
    h1 = yf.Ticker("BTC-USD").history(period="730d", interval="1h")

    print("=== DAILY FULL HISTORY TOURNAMENT (2014+) ===")
    top = grid_daily(daily)[:12]
    for sc, r, p in top:
        print(
            f"score={sc:.2f} {r.name} atr={p['atr_sl']} rr={p['rr']} "
            f"ret={r.ret_pct:.0f}% dd={r.max_dd_pct:.1f}% pf={r.pf:.2f} "
            f"calmar={r.calmar:.2f} trades={r.trades} win={r.win_rate:.0%}"
        )

    best = top[0]
    _, br, bp = best
    print("\n=== WALK-FORWARD (last 35% of daily) for top daily name ===")
    fns = {
        "donchian20": lambda d: donchian(d, 20),
        "donchian55": lambda d: donchian(d, 55),
        "adx_donchian55": lambda d: adx_donchian_hybrid(d, 55, 22),
        "adx_donchian20": lambda d: adx_donchian_hybrid(d, 20, 25),
        "ema12_48": lambda d: ema_cross(d, 12, 48),
        "squeeze": lambda d: squeeze_breakout(d),
    }
    fn = fns.get(br.name, lambda d: donchian(d, 20))
    oos = walk_forward_daily(
        daily,
        fn,
        {"atr_sl": bp["atr_sl"], "rr": bp["rr"], "risk": 0.01},
    )
    print(f"OOS {br.name}: ret={oos.ret_pct:.0f}% dd={oos.max_dd_pct:.1f}% pf={oos.pf:.2f} trades={oos.trades}")

    print("\n=== H1 RECENT (730d) — top configs ===")
    hres = grid_daily(h1)[:8]
    for sc, r, p in hres:
        print(
            f"score={sc:.2f} {r.name} atr={p['atr_sl']} rr={p['rr']} "
            f"ret={r.ret_pct:.0f}% dd={r.max_dd_pct:.1f}% pf={r.pf:.2f} trades={r.trades}"
        )