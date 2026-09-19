#!/usr/bin/env python3
"""
Exhaustive BTC-USD matrix: timeframes × indicators/triggers.
Outputs ranked results to full_matrix_results.csv
"""

from __future__ import annotations

import itertools
import warnings
from dataclasses import dataclass
from typing import Callable

import numpy as np
import pandas as pd
import yfinance as yf

warnings.filterwarnings("ignore")

# ---------------------------------------------------------------------------
# Indicators
# ---------------------------------------------------------------------------


def ema(s: pd.Series, n: int) -> pd.Series:
    return s.ewm(span=n, adjust=False).mean()


def sma(s: pd.Series, n: int) -> pd.Series:
    return s.rolling(n).mean()


def atr(df: pd.DataFrame, n: int = 14) -> pd.Series:
    h, l, c = df["High"], df["Low"], df["Close"]
    tr = pd.concat([h - l, (h - c.shift()).abs(), (l - c.shift()).abs()], axis=1).max(axis=1)
    return tr.rolling(n).mean()


def adx_pack(df: pd.DataFrame, n: int = 14):
    h, l, c = df["High"], df["Low"], df["Close"]
    up, down = h.diff(), -l.diff()
    plus_dm = np.where((up > down) & (up > 0), up, 0.0)
    minus_dm = np.where((down > up) & (down > 0), down, 0.0)
    tr = pd.concat([h - l, (h - c.shift()).abs(), (l - c.shift()).abs()], axis=1).max(axis=1)
    atr_v = tr.ewm(alpha=1 / n, adjust=False).mean()
    pdi = 100 * pd.Series(plus_dm, index=df.index).ewm(alpha=1 / n, adjust=False).mean() / atr_v
    mdi = 100 * pd.Series(minus_dm, index=df.index).ewm(alpha=1 / n, adjust=False).mean() / atr_v
    dx = 100 * (pdi - mdi).abs() / (pdi + mdi).replace(0, np.nan)
    adx_v = dx.ewm(alpha=1 / n, adjust=False).mean()
    return adx_v, pdi, mdi


def macd_line(s: pd.Series, fast=12, slow=26, signal=9):
    m = ema(s, fast) - ema(s, slow)
    sig = ema(m, signal)
    hist = m - sig
    return m, sig, hist


def rsi(s: pd.Series, n=14):
    d = s.diff()
    up = d.clip(lower=0).rolling(n).mean()
    down = (-d.clip(upper=0)).rolling(n).mean()
    return 100 - 100 / (1 + up / down.replace(0, np.nan))


def stoch(df: pd.DataFrame, k=14, d=3):
    ll = df["Low"].rolling(k).min()
    hh = df["High"].rolling(k).max()
    k_line = 100 * (df["Close"] - ll) / (hh - ll).replace(0, np.nan)
    d_line = k_line.rolling(d).mean()
    return k_line, d_line


def empty_sig(df: pd.DataFrame) -> pd.DataFrame:
    return pd.DataFrame({"long": False, "short": False}, index=df.index)


def sig_donchian(df: pd.DataFrame, n: int) -> pd.DataFrame:
    up = df["High"].rolling(n).max().shift(1)
    lo = df["Low"].rolling(n).min().shift(1)
    c = df["Close"]
    return pd.DataFrame({"long": c > up, "short": c < lo}, index=df.index).fillna(False)


def sig_adx_donchian(df: pd.DataFrame, n: int, adx_min=22) -> pd.DataFrame:
    adx_v, pdi, mdi = adx_pack(df)
    base = sig_donchian(df, n)
    trend = adx_v >= adx_min
    return pd.DataFrame(
        {
            "long": base["long"] & trend & (pdi > mdi),
            "short": base["short"] & trend & (mdi > pdi),
        },
        index=df.index,
    ).fillna(False)


def sig_ema_cross(df: pd.DataFrame, f: int, s: int) -> pd.DataFrame:
    ef, es = ema(df["Close"], f), ema(df["Close"], s)
    return pd.DataFrame(
        {
            "long": (ef.shift(1) <= es.shift(1)) & (ef > es),
            "short": (ef.shift(1) >= es.shift(1)) & (ef < es),
        },
        index=df.index,
    ).fillna(False)


def sig_macd_cross(df: pd.DataFrame) -> pd.DataFrame:
    m, sig, _ = macd_line(df["Close"])
    return pd.DataFrame(
        {
            "long": (m.shift(1) <= sig.shift(1)) & (m > sig),
            "short": (m.shift(1) >= sig.shift(1)) & (m < sig),
        },
        index=df.index,
    ).fillna(False)


def sig_macd_hist_turn(df: pd.DataFrame) -> pd.DataFrame:
    _, _, h = macd_line(df["Close"])
    return pd.DataFrame(
        {
            "long": (h.shift(1) <= 0) & (h > 0),
            "short": (h.shift(1) >= 0) & (h < 0),
        },
        index=df.index,
    ).fillna(False)


def sig_rsi_pullback(df: pd.DataFrame, trend_ema=200) -> pd.DataFrame:
    tr = ema(df["Close"], trend_ema)
    r = rsi(df["Close"])
    return pd.DataFrame(
        {
            "long": (df["Close"] > tr) & (r.shift(1) < 40) & (r >= 40),
            "short": (df["Close"] < tr) & (r.shift(1) > 60) & (r <= 60),
        },
        index=df.index,
    ).fillna(False)


def sig_rsi50_cross(df: pd.DataFrame) -> pd.DataFrame:
    r = rsi(df["Close"])
    return pd.DataFrame(
        {
            "long": (r.shift(1) <= 50) & (r > 50),
            "short": (r.shift(1) >= 50) & (r < 50),
        },
        index=df.index,
    ).fillna(False)


def sig_bb_break(df: pd.DataFrame, n=20) -> pd.DataFrame:
    mid = sma(df["Close"], n)
    std = df["Close"].rolling(n).std()
    up, lo = mid + 2 * std, mid - 2 * std
    c = df["Close"]
    return pd.DataFrame(
        {
            "long": (c.shift(1) <= up.shift(1)) & (c > up),
            "short": (c.shift(1) >= lo.shift(1)) & (c < lo),
        },
        index=df.index,
    ).fillna(False)


def sig_bb_revert(df: pd.DataFrame, n=20) -> pd.DataFrame:
    mid = sma(df["Close"], n)
    std = df["Close"].rolling(n).std()
    lo, up = mid - 2 * std, mid + 2 * std
    c, p = df["Close"], df["Close"].shift(1)
    return pd.DataFrame(
        {
            "long": (p >= lo.shift(1)) & (c < lo) & (c > p),
            "short": (p <= up.shift(1)) & (c > up) & (c < p),
        },
        index=df.index,
    ).fillna(False)


def sig_keltner(df: pd.DataFrame, n=20, mult=2.0) -> pd.DataFrame:
    mid = ema(df["Close"], n)
    band = mult * atr(df, n)
    up, lo = mid + band, mid - band
    c = df["Close"]
    return pd.DataFrame(
        {
            "long": (c.shift(1) <= up.shift(1)) & (c > up),
            "short": (c.shift(1) >= lo.shift(1)) & (c < lo),
        },
        index=df.index,
    ).fillna(False)


def sig_stoch_cross(df: pd.DataFrame) -> pd.DataFrame:
    k, d = stoch(df)
    return pd.DataFrame(
        {
            "long": (k.shift(1) <= d.shift(1)) & (k > d) & (k < 80),
            "short": (k.shift(1) >= d.shift(1)) & (k < d) & (k > 20),
        },
        index=df.index,
    ).fillna(False)


def sig_cci_cross(df: pd.DataFrame, n=20) -> pd.DataFrame:
    tp = (df["High"] + df["Low"] + df["Close"]) / 3
    cci = (tp - sma(tp, n)) / (0.015 * tp.rolling(n).std())
    return pd.DataFrame(
        {
            "long": (cci.shift(1) <= 100) & (cci > 100),
            "short": (cci.shift(1) >= -100) & (cci < -100),
        },
        index=df.index,
    ).fillna(False)


def sig_di_cross(df: pd.DataFrame) -> pd.DataFrame:
    _, pdi, mdi = adx_pack(df)
    return pd.DataFrame(
        {
            "long": (pdi.shift(1) <= mdi.shift(1)) & (pdi > mdi),
            "short": (pdi.shift(1) >= mdi.shift(1)) & (pdi < mdi),
        },
        index=df.index,
    ).fillna(False)


def sig_adx_rising_breakout(df: pd.DataFrame, n=20, adx_min=20) -> pd.DataFrame:
    adx_v, pdi, mdi = adx_pack(df)
    dc = sig_donchian(df, n)
    rising = adx_v > adx_v.shift(3)
    return pd.DataFrame(
        {
            "long": dc["long"] & rising & (adx_v >= adx_min) & (pdi > mdi),
            "short": dc["short"] & rising & (adx_v >= adx_min) & (mdi > pdi),
        },
        index=df.index,
    ).fillna(False)


def sig_roc_momentum(df: pd.DataFrame, n=12, thresh=0) -> pd.DataFrame:
    roc = df["Close"].pct_change(n) * 100
    return pd.DataFrame(
        {
            "long": (roc.shift(1) <= thresh) & (roc > thresh),
            "short": (roc.shift(1) >= -thresh) & (roc < -thresh),
        },
        index=df.index,
    ).fillna(False)


def sig_triple_ema(df: pd.DataFrame) -> pd.DataFrame:
    e8, e21, e55 = ema(df["Close"], 8), ema(df["Close"], 21), ema(df["Close"], 55)
    stack_up = (e8 > e21) & (e21 > e55)
    stack_dn = (e8 < e21) & (e21 < e55)
    touch = (df["Close"].shift(1) <= e21.shift(1)) & (df["Close"] > e21)
    touch_s = (df["Close"].shift(1) >= e21.shift(1)) & (df["Close"] < e21)
    return pd.DataFrame(
        {"long": stack_up & touch, "short": stack_dn & touch_s},
        index=df.index,
    ).fillna(False)


def sig_supertrend(df: pd.DataFrame, mult=3.0, n=10) -> pd.DataFrame:
    hl2 = (df["High"] + df["Low"]) / 2
    a = atr(df, n)
    upper = hl2 + mult * a
    lower = hl2 - mult * a
    st = pd.Series(index=df.index, dtype=float)
    direction = pd.Series(index=df.index, dtype=int)
    st.iloc[0] = upper.iloc[0]
    direction.iloc[0] = 1
    for i in range(1, len(df)):
        if df["Close"].iloc[i] > st.iloc[i - 1]:
            direction.iloc[i] = 1
            st.iloc[i] = max(lower.iloc[i], st.iloc[i - 1] if direction.iloc[i - 1] == 1 else lower.iloc[i])
        else:
            direction.iloc[i] = -1
            st.iloc[i] = min(upper.iloc[i], st.iloc[i - 1] if direction.iloc[i - 1] == -1 else upper.iloc[i])
    flip_up = (direction.shift(1) == -1) & (direction == 1)
    flip_dn = (direction.shift(1) == 1) & (direction == -1)
    return pd.DataFrame({"long": flip_up, "short": flip_dn}, index=df.index).fillna(False)


def sig_ichimoku_tk(df: pd.DataFrame) -> pd.DataFrame:
    high9 = df["High"].rolling(9).max()
    low9 = df["Low"].rolling(9).min()
    tenkan = (high9 + low9) / 2
    high26 = df["High"].rolling(26).max()
    low26 = df["Low"].rolling(26).min()
    kijun = (high26 + low26) / 2
    return pd.DataFrame(
        {
            "long": (tenkan.shift(1) <= kijun.shift(1)) & (tenkan > kijun),
            "short": (tenkan.shift(1) >= kijun.shift(1)) & (tenkan < kijun),
        },
        index=df.index,
    ).fillna(False)


def sig_williams_r(df: pd.DataFrame, n: int = 14) -> pd.DataFrame:
    hh = df["High"].rolling(n).max()
    ll = df["Low"].rolling(n).min()
    wr = -100 * (hh - df["Close"]) / (hh - ll).replace(0, np.nan)
    return pd.DataFrame(
        {
            "long": (wr.shift(1) <= -80) & (wr > -80),
            "short": (wr.shift(1) >= -20) & (wr < -20),
        },
        index=df.index,
    ).fillna(False)


def sig_aroon_cross(df: pd.DataFrame, n: int = 25) -> pd.DataFrame:
    a_up = df["High"].rolling(n).apply(lambda x: 100 * (n - 1 - np.argmax(x)) / n, raw=True)
    a_down = df["Low"].rolling(n).apply(lambda x: 100 * (n - 1 - np.argmin(x)) / n, raw=True)
    return pd.DataFrame(
        {
            "long": (a_up.shift(1) <= a_down.shift(1)) & (a_up > a_down) & (a_up > 50),
            "short": (a_down.shift(1) <= a_up.shift(1)) & (a_down > a_up) & (a_down > 50),
        },
        index=df.index,
    ).fillna(False)


def sig_psar_flip(df: pd.DataFrame, step=0.02, max_step=0.2) -> pd.DataFrame:
    # Simplified PSAR trend flip vs close
    high, low, close = df["High"].values, df["Low"].values, df["Close"].values
    n = len(df)
    psar = np.zeros(n)
    bull = True
    af, ep = step, low[0]
    psar[0] = high[0]
    for i in range(1, n):
        psar[i] = psar[i - 1] + af * (ep - psar[i - 1])
        if bull:
            psar[i] = min(psar[i], low[i - 1], low[i - 2] if i > 1 else low[i - 1])
            if low[i] < psar[i]:
                bull = False
                psar[i] = ep
                ep = low[i]
                af = step
            elif high[i] > ep:
                ep = high[i]
                af = min(af + step, max_step)
        else:
            psar[i] = max(psar[i], high[i - 1], high[i - 2] if i > 1 else high[i - 1])
            if high[i] > psar[i]:
                bull = True
                psar[i] = ep
                ep = high[i]
                af = step
            elif low[i] < ep:
                ep = low[i]
                af = min(af + step, max_step)
    bull_s = pd.Series(close > psar, index=df.index)
    return pd.DataFrame(
        {
            "long": (~bull_s.shift(1).fillna(False)) & bull_s,
            "short": bull_s.shift(1).fillna(False) & (~bull_s),
        },
        index=df.index,
    ).fillna(False)


def sig_mfi_reversal(df: pd.DataFrame, n: int = 14) -> pd.DataFrame:
    tp = (df["High"] + df["Low"] + df["Close"]) / 3
    rmf = tp * df["Volume"]
    pos = np.where(tp > tp.shift(1), rmf, 0.0)
    neg = np.where(tp < tp.shift(1), rmf, 0.0)
    mfr = pd.Series(pos, index=df.index).rolling(n).sum() / pd.Series(neg, index=df.index).rolling(n).sum().replace(0, np.nan)
    mfi = 100 - 100 / (1 + mfr)
    return pd.DataFrame(
        {
            "long": (mfi.shift(1) <= 20) & (mfi > 20),
            "short": (mfi.shift(1) >= 80) & (mfi < 80),
        },
        index=df.index,
    ).fillna(False)


def sig_sma_cross(df: pd.DataFrame, f: int, s: int) -> pd.DataFrame:
    fs, ss = sma(df["Close"], f), sma(df["Close"], s)
    return pd.DataFrame(
        {
            "long": (fs.shift(1) <= ss.shift(1)) & (fs > ss),
            "short": (fs.shift(1) >= ss.shift(1)) & (fs < ss),
        },
        index=df.index,
    ).fillna(False)


def sig_vol_breakout(df: pd.DataFrame, n=20) -> pd.DataFrame:
    vol_ma = df["Volume"].rolling(n).mean()
    hi = df["High"].rolling(n).max().shift(1)
    lo = df["Low"].rolling(n).min().shift(1)
    spike = df["Volume"] > 1.8 * vol_ma
    c = df["Close"]
    return pd.DataFrame(
        {"long": spike & (c > hi), "short": spike & (c < lo)},
        index=df.index,
    ).fillna(False)


# Multi-TF filter wrapper
def apply_htf_filter(df_ltf: pd.DataFrame, df_htf: pd.DataFrame, signals: pd.DataFrame) -> pd.DataFrame:
    """Only keep LTF long if HTF close > HTF EMA55; short if below."""
    htf_ema = ema(df_htf["Close"], 55)
    htf_up = df_htf["Close"] > htf_ema
    htf_dn = df_htf["Close"] < htf_ema
    up = htf_up.reindex(df_ltf.index, method="ffill").fillna(False)
    dn = htf_dn.reindex(df_ltf.index, method="ffill").fillna(False)
    return pd.DataFrame(
        {"long": signals["long"] & up, "short": signals["short"] & dn},
        index=df_ltf.index,
    )


# ---------------------------------------------------------------------------
# Simulator
# ---------------------------------------------------------------------------


@dataclass
class SimResult:
    trades: int
    ret_pct: float
    max_dd_pct: float
    pf: float
    win_rate: float


def run_sim(
    df: pd.DataFrame,
    signals: pd.DataFrame,
    *,
    atr_sl: float = 2.5,
    rr: float = 2.5,
    risk: float = 0.01,
    start_equity: float = 10_000.0,
) -> SimResult:
    equity = start_equity
    peak = equity
    max_dd = 0.0
    pos = None
    pnls: list[float] = []
    atr_v = atr(df)
    warmup = 250

    for i in range(warmup, len(df)):
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
                pnls.append((exit_p - pos["entry"]) * pos["units"] * sign)
                equity += pnls[-1]
                pos = None

        if pos is None:
            av = atr_v.iloc[i]
            if av <= 0 or np.isnan(av):
                continue
            if bool(signals["long"].iloc[i]):
                entry = row["Close"]
                sl = entry - atr_sl * av
                r = entry - sl
                pos = {"side": "long", "entry": entry, "sl": sl, "tp": entry + rr * r, "units": equity * risk / r}
            elif bool(signals["short"].iloc[i]):
                entry = row["Close"]
                sl = entry + atr_sl * av
                r = sl - entry
                pos = {"side": "short", "entry": entry, "sl": sl, "tp": entry - rr * r, "units": equity * risk / r}

        peak = max(peak, equity)
        max_dd = max(max_dd, (peak - equity) / peak)

    gp = sum(p for p in pnls if p > 0)
    gl = -sum(p for p in pnls if p < 0)
    n = len(pnls)
    return SimResult(
        trades=n,
        ret_pct=(equity / start_equity - 1) * 100,
        max_dd_pct=max_dd * 100,
        pf=gp / max(1e-9, gl),
        win_rate=sum(1 for p in pnls if p > 0) / max(1, n),
    )


def score(r: SimResult, min_trades: int) -> float:
    if r.trades < min_trades or r.max_dd_pct > 40:
        return -1e9
    years_factor = 1.0
    return (r.pf * 2.0) + (r.ret_pct / 50.0) - (r.max_dd_pct / 20.0) + (r.win_rate * 0.5)


# ---------------------------------------------------------------------------
# Strategy catalog
# ---------------------------------------------------------------------------

STRATEGIES: dict[str, Callable[[pd.DataFrame], pd.DataFrame]] = {
    "donchian_20": lambda d: sig_donchian(d, 20),
    "donchian_55": lambda d: sig_donchian(d, 55),
    "adx_donchian_20": lambda d: sig_adx_donchian(d, 20, 22),
    "adx_donchian_55": lambda d: sig_adx_donchian(d, 55, 22),
    "adx_donchian_20_strict": lambda d: sig_adx_donchian(d, 20, 25),
    "adx_rising_break_20": lambda d: sig_adx_rising_breakout(d, 20, 20),
    "ema_12_26": lambda d: sig_ema_cross(d, 12, 26),
    "ema_21_55": lambda d: sig_ema_cross(d, 21, 55),
    "ema_50_200": lambda d: sig_ema_cross(d, 50, 200),
    "triple_ema_pullback": lambda d: sig_triple_ema(d),
    "macd_cross": lambda d: sig_macd_cross(d),
    "macd_hist_turn": lambda d: sig_macd_hist_turn(d),
    "rsi_trend_pullback": lambda d: sig_rsi_pullback(d, 200),
    "rsi50_cross": lambda d: sig_rsi50_cross(d),
    "bb_breakout": lambda d: sig_bb_break(d),
    "bb_mean_revert": lambda d: sig_bb_revert(d),
    "keltner_break": lambda d: sig_keltner(d),
    "stoch_cross": lambda d: sig_stoch_cross(d),
    "cci_100_cross": lambda d: sig_cci_cross(d),
    "di_cross": lambda d: sig_di_cross(d),
    "roc_momentum_12": lambda d: sig_roc_momentum(d),
    "supertrend_flip": lambda d: sig_supertrend(d),
    "ichimoku_tk_cross": lambda d: sig_ichimoku_tk(d),
    "volume_spike_break": lambda d: sig_vol_breakout(d),
    "williams_r_reversal": lambda d: sig_williams_r(d),
    "aroon_cross_25": lambda d: sig_aroon_cross(d, 25),
    "psar_flip": lambda d: sig_psar_flip(d),
    "mfi_reversal": lambda d: sig_mfi_reversal(d),
    "sma_50_200": lambda d: sig_sma_cross(d, 50, 200),
    "ema_9_21": lambda d: sig_ema_cross(d, 9, 21),
    "donchian_10": lambda d: sig_donchian(d, 10),
}

PARAM_GRID = [(2.0, 2.0), (2.5, 2.5), (2.5, 3.0)]


def _ohlc_agg(df: pd.DataFrame, rule: str) -> pd.DataFrame:
    return (
        df.resample(rule)
        .agg({"Open": "first", "High": "max", "Low": "min", "Close": "last", "Volume": "sum"})
        .dropna()
    )


def load_timeframes() -> dict[str, pd.DataFrame]:
    t = yf.Ticker("BTC-USD")
    m1 = t.history(period="7d", interval="1m")
    m5 = t.history(period="60d", interval="5m")
    m15 = t.history(period="60d", interval="15m")
    m30 = t.history(period="60d", interval="30m")
    h1 = t.history(period="730d", interval="1h")
    d1 = t.history(period="max", interval="1d")
    m3 = _ohlc_agg(m1, "3min") if len(m1) > 500 else pd.DataFrame()
    h2 = _ohlc_agg(h1, "2h")
    h3 = _ohlc_agg(h1, "3h")
    h4 = _ohlc_agg(h1, "4h")
    w1 = _ohlc_agg(d1, "W")
    out = {
        "M1": m1,
        "M3": m3,
        "M5": m5,
        "M15": m15,
        "M30": m30,
        "H1": h1,
        "H2": h2,
        "H3": h3,
        "H4": h4,
        "D1": d1,
        "W1": w1,
    }
    return {k: v for k, v in out.items() if v is not None and len(v) >= 300}


def min_trades_for(tf: str) -> int:
    return {
        "M1": 40,
        "M3": 35,
        "M5": 30,
        "M15": 25,
        "M30": 20,
        "H1": 15,
        "H2": 12,
        "H3": 12,
        "H4": 10,
        "D1": 10,
        "W1": 8,
    }.get(tf, 15)


def higher_tf(tf: str) -> str | None:
    order = ["M1", "M3", "M5", "M15", "M30", "H1", "H2", "H3", "H4", "D1", "W1"]
    i = order.index(tf)
    return order[i + 1] if i + 1 < len(order) else None


def main() -> None:
    data = load_timeframes()
    rows = []

    for tf, df in data.items():
        if len(df) < 300:
            continue
        mt = min_trades_for(tf)
        htf_name = higher_tf(tf)
        htf_df = data.get(htf_name) if htf_name else None

        for strat_name, fn in STRATEGIES.items():
            try:
                sig = fn(df)
            except Exception:
                continue
            variants = [(strat_name, sig)]
            if htf_df is not None and len(htf_df) > 100:
                variants.append((strat_name + "+HTF_EMA55", apply_htf_filter(df, htf_df, sig)))

            for vname, vsig in variants:
                for atr_sl, rr in PARAM_GRID:
                    r = run_sim(df, vsig, atr_sl=atr_sl, rr=rr, risk=0.01)
                    sc = score(r, mt)
                    rows.append(
                        {
                            "timeframe": tf,
                            "strategy": vname,
                            "atr_sl": atr_sl,
                            "rr": rr,
                            "score": sc,
                            "trades": r.trades,
                            "return_pct": round(r.ret_pct, 2),
                            "max_dd_pct": round(r.max_dd_pct, 2),
                            "profit_factor": round(r.pf, 3),
                            "win_rate": round(r.win_rate, 3),
                        }
                    )

    out = pd.DataFrame(rows)
    out = out.sort_values("score", ascending=False)
    path = "/workspace/ctrader-btc-bot/scripts/full_matrix_results.csv"
    out.to_csv(path, index=False)

    print(f"Wrote {len(out)} rows to {path}\n")
    for tf in ["M1", "M3", "M5", "M15", "M30", "H1", "H2", "H3", "H4", "D1", "W1"]:
        sub = out[out["timeframe"] == tf].head(5)
        if sub.empty:
            continue
        print(f"=== TOP 5 on {tf} ===")
        for _, row in sub.iterrows():
            print(
                f"  {row['strategy']} sl={row['atr_sl']} rr={row['rr']} "
                f"ret={row['return_pct']}% dd={row['max_dd_pct']}% pf={row['profit_factor']} "
                f"n={row['trades']} score={row['score']:.2f}"
            )

    # Cross-TF robust: strategies that appear in top 10 per TF
    top_per = out.groupby("timeframe").head(10)
    counts = top_per.groupby("strategy").size().sort_values(ascending=False)
    print("\n=== Strategies most often in TOP 10 per timeframe ===")
    print(counts.head(15).to_string())

    print("\n=== GLOBAL TOP 15 ===")
    print(
        out.head(15)[
            ["timeframe", "strategy", "atr_sl", "rr", "return_pct", "max_dd_pct", "profit_factor", "trades"]
        ].to_string(index=False)
    )


if __name__ == "__main__":
    main()
