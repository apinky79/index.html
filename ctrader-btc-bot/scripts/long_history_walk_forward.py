#!/usr/bin/env python3
"""
Long-history BTC/USD tests: 2009 → today (daily).
Bitcoin had no USD market in 2000–2008; data starts ~2009-01-03.
"""

from __future__ import annotations

from dataclasses import dataclass
from itertools import product
from pathlib import Path

import pandas as pd

from full_matrix_research import STRATEGIES, apply_htf_filter
from load_btc_history import load_or_fetch
from weekly_prop_sim import run_sim_max_hold

START = 50_000.0
RISK = 0.008
MIN_TRADES = 5
MIN_PF = 1.15
MAX_DD = 20.0
TRAIN_BARS = 400


def build_configs():
    return [
        ("ema_50_200", lambda d, w: STRATEGIES["ema_50_200"](d)),
        ("donchian_55", lambda d, w: STRATEGIES["donchian_55"](d)),
        ("adx_dc_55", lambda d, w: STRATEGIES["adx_donchian_55"](d)),
        ("adx_rising_20", lambda d, w: STRATEGIES["adx_rising_break_20"](d)),
        ("donchian_20", lambda d, w: STRATEGIES["donchian_20"](d)),
        ("keltner", lambda d, w: STRATEGIES["keltner_break"](d)),
        ("macd_cross", lambda d, w: STRATEGIES["macd_cross"](d)),
        ("ema_21_55", lambda d, w: STRATEGIES["ema_21_55"](d)),
    ]


HOLDS = [3, 5, 10]
SL_RR = [(2.0, 2.0), (2.5, 2.5)]


@dataclass
class Cand:
    name: str
    hold: int
    atr_sl: float
    rr: float
    fn: object


def weekly_ohlc(daily: pd.DataFrame) -> pd.DataFrame:
    return daily.resample("W").agg({"Open": "first", "High": "max", "Low": "min", "Close": "last", "Volume": "sum"}).dropna()


def optimize(train: pd.DataFrame, weekly: pd.DataFrame, builders) -> list[Cand]:
    ranked: list[tuple[float, Cand]] = []
    for name, fn in builders:
        try:
            sig = fn(train, weekly).reindex(train.index).fillna(False)
        except Exception:
            continue
        for hold, (sl, rr) in product(HOLDS, SL_RR):
            r = run_sim_max_hold(train, sig, max_hold_bars=hold, atr_sl=sl, rr=rr, risk=RISK, start_equity=START)
            if r["trades"] < MIN_TRADES or r["profit_factor"] < MIN_PF or r["max_dd_pct"] > MAX_DD:
                continue
            sc = r["profit_factor"] * 2 + r["ret_pct"] * 0.05 - r["max_dd_pct"] * 0.2
            ranked.append((sc, Cand(name, hold, sl, rr, fn)))
    ranked.sort(key=lambda x: -x[0])
    return [c for _, c in ranked[:10]]


def main():
    daily = load_or_fetch()
    weekly = weekly_ohlc(daily)

    print("=" * 70, flush=True)
    print("LONG-HISTORY BTC/USD (daily)", flush=True)
    print(f"Range: {daily.index[0].date()} → {daily.index[-1].date()} ({len(daily)} days)", flush=True)
    print("2000–2008: no BTC market data (N/A).\n", flush=True)

    print("--- FULL SAMPLE: fixed strategies (0.8% risk, max hold 5 days) ---", flush=True)
    for label, fn in [
        ("adx_rising_break_20", lambda d: STRATEGIES["adx_rising_break_20"](d)),
        ("adx_donchian_55", lambda d: STRATEGIES["adx_donchian_55"](d)),
        ("donchian_20", lambda d: STRATEGIES["donchian_20"](d)),
        ("ema_50_200", lambda d: STRATEGIES["ema_50_200"](d)),
    ]:
        sig = fn(daily)
        r = run_sim_max_hold(daily, sig, max_hold_bars=5, atr_sl=2.0, rr=2.0, risk=RISK, start_equity=START)
        print(
            f"  {label:22} ${r['final']:,.0f}  ret {r['ret_pct']:.0f}%  DD {r['max_dd_pct']:.1f}%  "
            f"PF {r['profit_factor']:.2f}  trades {r['trades']}",
            flush=True,
        )

    bh0 = float(daily["Close"].loc["2014-01-01":].iloc[0])
    bh = START * float(daily["Close"].iloc[-1]) / bh0
    print(f"  {'buy_hold_from_2014':22} ${bh:,.0f}  ret {(bh/START-1)*100:.0f}%\n", flush=True)

    # Walk-forward: every 4th week from 2011 → now
    wk = daily.index.to_series().apply(lambda t: (t.isocalendar().year, t.isocalendar().week))
    tmp = daily.copy()
    tmp["_w"] = wk.values
    weeks = []
    for k, g in tmp.groupby("_w"):
        weeks.append((k, g.index.min(), g.index.max()))
    weeks.sort(key=lambda x: x[1])

    builders = build_configs()
    fixed = Cand("adx_rising_20", 5, 2.0, 2.0, lambda d, w: STRATEGIES["adx_rising_break_20"](d))
    eq_wf = eq_fix = START
    log = []

    for idx in range(60, len(weeks), 4):  # every 4 weeks after warmup
        fwd, wstart, wend = weeks[idx]
        if wstart.year < 2011:
            continue
        train = daily.loc[daily.index <= weeks[idx - 1][2]].iloc[-TRAIN_BARS:]
        top = optimize(train, weekly, builders)
        if not top:
            continue
        pick = top[0]
        e0 = eq_wf
        eq_wf, _, _ = forward_week_pnl_fixed(daily, weekly, pick, wstart, wend, eq_wf)
        pnl_wf = eq_wf - e0
        e0f = eq_fix
        eq_fix, _, _ = forward_week_pnl_fixed(daily, weekly, fixed, wstart, wend, eq_fix)
        pnl_fix = eq_fix - e0f
        log.append({"week": fwd, "pick": pick.name, "pnl_wf": pnl_wf, "pnl_fix": pnl_fix})

    print("--- WALK-FORWARD (daily, every 4 weeks, train last 400 days) ---", flush=True)
    print(f"  Forward steps: {len(log)}", flush=True)
    print(f"  Weekly re-opt total PnL: ${eq_wf - START:,.0f} ({(eq_wf/START-1)*100:.1f}%)  end ${eq_wf:,.0f}", flush=True)
    print(f"  Fixed adx_rising_20:     ${eq_fix - START:,.0f} ({(eq_fix/START-1)*100:.1f}%)  end ${eq_fix:,.0f}", flush=True)

    yr_wf, yr_fix = {}, {}
    for row in log:
        y = row["week"][0]
        yr_wf[y] = yr_wf.get(y, 0) + row["pnl_wf"]
        yr_fix[y] = yr_fix.get(y, 0) + row["pnl_fix"]
    print("\n--- Yearly PnL (walk-forward steps, daily) ---", flush=True)
    for y in sorted(yr_wf):
        print(f"  {y}: re-opt ${yr_wf[y]:,.0f}  |  fixed ${yr_fix[y]:,.0f}", flush=True)

    pd.DataFrame(log).to_csv(Path(__file__).parent / "long_history_walk_forward_log.csv", index=False)
    print("\nSaved long_history_walk_forward_log.csv", flush=True)


def forward_week_pnl_fixed(daily, weekly, c: Cand, wstart, wend, equity) -> tuple[float, float, int]:
    df = daily.loc[daily.index <= wend]
    sig = c.fn(daily, weekly).reindex(df.index).fillna(False)
    from full_matrix_research import atr

    atr_v = atr(df)
    e0 = equity
    pos = None
    trades = 0
    for i in range(250, len(df)):
        ts = df.index[i]
        row = df.iloc[i]
        if pos:
            pos["hold"] += 1
            exit_p = None
            if pos["side"] == "long":
                if row["Low"] <= pos["sl"]:
                    exit_p = pos["sl"]
                elif row["High"] >= pos["tp"]:
                    exit_p = pos["tp"]
                elif pos["hold"] >= c.hold:
                    exit_p = row["Close"]
            else:
                if row["High"] >= pos["sl"]:
                    exit_p = pos["sl"]
                elif row["Low"] <= pos["tp"]:
                    exit_p = pos["tp"]
                elif pos["hold"] >= c.hold:
                    exit_p = row["Close"]
            if exit_p is not None:
                sign = 1 if pos["side"] == "long" else -1
                equity += (exit_p - pos["entry"]) * pos["units"] * sign
                if wstart <= ts <= wend:
                    trades += 1
                pos = None
        if pos is None and wstart <= ts <= wend:
            av = atr_v.iloc[i]
            if av <= 0:
                continue
            if sig["long"].iloc[i]:
                entry = row["Close"]
                sl = entry - c.atr_sl * av
                r = entry - sl
                pos = {"side": "long", "entry": entry, "sl": sl, "tp": entry + c.rr * r, "units": equity * RISK / r, "hold": 0}
            elif sig["short"].iloc[i]:
                entry = row["Close"]
                sl = entry + c.atr_sl * av
                r = sl - entry
                pos = {"side": "short", "entry": entry, "sl": sl, "tp": entry - c.rr * r, "units": equity * RISK / r, "hold": 0}
    return equity, equity - e0, trades


if __name__ == "__main__":
    main()
