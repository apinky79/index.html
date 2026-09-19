#!/usr/bin/env python3
"""
Walk-forward: each week optimize on prior 4 weeks, pick #1 of top-10 passing configs,
trade that for the forward week only (M15, BrightFunded-style).
"""

from __future__ import annotations

from dataclasses import dataclass
from itertools import product

import pandas as pd
import yfinance as yf

from full_matrix_research import STRATEGIES, apply_htf_filter
from weekly_prop_sim import run_sim_forward_week, run_sim_max_hold

RISK = 0.008
START = 50_000.0
MIN_TRADES_TRAIN = 5
MIN_PF = 1.2
MAX_DD_TRAIN = 8.0
HOLDS = [32, 48, 64]
SL_RR = [(2.0, 2.0), (2.5, 2.5), (2.5, 3.0)]


def build_configs():
    return [
        ("ema_50_200", lambda d, h4: STRATEGIES["ema_50_200"](d)),
        ("ema_21_55", lambda d, h4: STRATEGIES["ema_21_55"](d)),
        ("ema_21_55_htf", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["ema_21_55"](d))),
        ("donchian_20_htf", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["donchian_20"](d))),
        ("adx_dc_20_htf", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["adx_donchian_20"](d))),
        ("keltner_htf", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["keltner_break"](d))),
        ("aroon_htf", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["aroon_cross_25"](d))),
        ("di_cross_htf", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["di_cross"](d))),
        ("triple_ema_htf", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["triple_ema_pullback"](d))),
        ("macd_cross", lambda d, h4: STRATEGIES["macd_cross"](d)),
        ("volume_spike_htf", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["volume_spike_break"](d))),
        ("cci_htf", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["cci_100_cross"](d))),
        ("williams_htf", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["williams_r_reversal"](d))),
    ]


@dataclass
class Candidate:
    name: str
    hold: int
    atr_sl: float
    rr: float
    fn: object
    train_score: float
    train_pf: float
    train_dd: float
    train_trades: int


def iso_week_key(ts: pd.Timestamp) -> tuple[int, int]:
    ic = ts.isocalendar()
    return (ic.year, ic.week)


def week_slices(df: pd.DataFrame) -> list[tuple[tuple[int, int], pd.Timestamp, pd.Timestamp]]:
    wk = df.index.to_series().apply(iso_week_key)
    tmp = df.copy()
    tmp["_wk"] = wk.values
    out = []
    for key, grp in tmp.groupby("_wk"):
        out.append((key, grp.index.min(), grp.index.max()))
    out.sort(key=lambda x: x[1])
    return out


def optimize_top10(train_df: pd.DataFrame, h4: pd.DataFrame, builders) -> list[Candidate]:
    candidates: list[Candidate] = []
    for name, fn in builders:
        try:
            sig = fn(train_df, h4).reindex(train_df.index).fillna(False)
        except Exception:
            continue
        for hold, (atr_sl, rr) in product(HOLDS, SL_RR):
            r = run_sim_max_hold(
                train_df,
                sig,
                max_hold_bars=hold,
                atr_sl=atr_sl,
                rr=rr,
                risk=RISK,
                start_equity=START,
                week_dd_limit=0.035,
            )
            if r["trades"] < MIN_TRADES_TRAIN:
                continue
            if r["profit_factor"] < MIN_PF:
                continue
            if r["max_dd_pct"] > MAX_DD_TRAIN:
                continue
            score = r["profit_factor"] * 2 + r["ret_pct"] * 0.15 - r["max_dd_pct"] * 0.5
            candidates.append(
                Candidate(name, hold, atr_sl, rr, fn, score, r["profit_factor"], r["max_dd_pct"], r["trades"])
            )
    candidates.sort(key=lambda c: -c.train_score)
    return candidates[:10]


def trade_week(
    m15: pd.DataFrame,
    h4: pd.DataFrame,
    cand: Candidate,
    week_start: pd.Timestamp,
    week_end: pd.Timestamp,
    equity: float,
) -> dict:
    df = m15.loc[m15.index <= week_end]
    sig = cand.fn(m15, h4).reindex(df.index).fillna(False)
    return run_sim_forward_week(
        df,
        sig,
        week_start=week_start,
        week_end=week_end,
        max_hold_bars=cand.hold,
        atr_sl=cand.atr_sl,
        rr=cand.rr,
        risk=RISK,
        start_equity=equity,
    )


def walk_forward(m15: pd.DataFrame, h4: pd.DataFrame, *, min_train_weeks: int = 4, lookback_weeks: int = 4):
    builders = build_configs()
    weeks = week_slices(m15)
    equity_wf = START
    equity_fix = START
    log = []

    fixed_cand = Candidate(
        "ema_50_200", 48, 2.5, 2.5, lambda d, h4: STRATEGIES["ema_50_200"](d), 0, 0, 0, 0
    )

    for i in range(min_train_weeks, len(weeks)):
        fwd_key, fwd_start, fwd_end = weeks[i]
        train_start = weeks[i - lookback_weeks][1]
        train_end = weeks[i - 1][2]
        train_df = m15.loc[(m15.index >= train_start) & (m15.index <= train_end)]

        top10 = optimize_top10(train_df, h4, builders)
        if not top10:
            log.append({"week": fwd_key, "pick": None, "pnl_wf": 0, "pnl_fix": 0, "equity_wf": equity_wf})
            continue

        pick = top10[0]
        r_wf = trade_week(m15, h4, pick, fwd_start, fwd_end, equity_wf)
        equity_wf = r_wf["final"]

        r_fix = trade_week(m15, h4, fixed_cand, fwd_start, fwd_end, equity_fix)
        equity_fix = r_fix["final"]

        # Oracle: best forward PnL among top10 this week
        best_oracle = -1e18
        for c in top10:
            r_o = trade_week(m15, h4, c, fwd_start, fwd_end, START)
            best_oracle = max(best_oracle, r_o["week_pnl"])

        log.append(
            {
                "week": fwd_key,
                "pick": f"{pick.name} h={pick.hold} sl={pick.atr_sl} rr={pick.rr}",
                "train_pf": pick.train_pf,
                "train_dd": pick.train_dd,
                "top10_n": len(top10),
                "pnl_wf": r_wf["week_pnl"],
                "pnl_fix": r_fix["week_pnl"],
                "equity_wf": equity_wf,
                "equity_fix": equity_fix,
                "trades_wf": r_wf["trades"],
                "oracle_best_top10_pnl": best_oracle,
            }
        )

    return equity_wf, equity_fix, log


def main():
    t = yf.Ticker("BTC-USD")
    m15 = t.history(period="60d", interval="15m")
    h1 = t.history(period="60d", interval="1h")
    h4 = h1.resample("4h").agg({"Open": "first", "High": "max", "Low": "min", "Close": "last", "Volume": "sum"}).dropna()

    print("BTC-USD M15 walk-forward weekly re-optimization")
    print(f"Range: {m15.index[0].date()} → {m15.index[-1].date()}  ({len(m15)} bars)\n")
    print("Method: train on prior 4 weeks → rank configs (14 strategies × 3 holds × 3 SL/RR)")
    print(f"Gates: ≥{MIN_TRADES_TRAIN} trades, PF≥{MIN_PF}, train DD≤{MAX_DD_TRAIN}%")
    print("Pick #1 of top 10 → trade forward week only | 0.8% risk, 3.5% week brake\n")

    eq_wf, eq_fix, log = walk_forward(m15, h4)

    pnl_wf = eq_wf - START
    pnl_fix = eq_fix - START

    print("=" * 60)
    print("WEEKLY RE-OPT (pick #1 of top 10 each week)")
    print(f"  Start:  ${START:,.0f}")
    print(f"  End:    ${eq_wf:,.0f}")
    print(f"  Profit: ${pnl_wf:,.0f}  ({pnl_wf/START*100:.2f}%)")
    print()
    print("FIXED Ultimate stack (EMA50/200, h=48, 2.5/2.5 — no weekly re-opt)")
    print(f"  End:    ${eq_fix:,.0f}")
    print(f"  Profit: ${pnl_fix:,.0f}  ({pnl_fix/START*100:.2f}%)")
    print()

    if log:
        oracle_sum = sum(r["oracle_best_top10_pnl"] for r in log if r.get("pick"))
        print(f"ORACLE (perfect pick from top-10 each week, non-compounded): ${oracle_sum:,.0f} sum of weeks")
        print(f"  (Upper bound — not achievable in live trading)\n")

    print("WEEK DETAIL:")
    for r in log:
        if not r.get("pick"):
            print(f"  {r['week']}: no config passed gates — flat")
            continue
        print(
            f"  {r['week']}: {r['pick']} | WF ${r['pnl_wf']:,.0f} | "
            f"fixed ${r['pnl_fix']:,.0f} | trades={r['trades_wf']}"
        )

    pd.DataFrame(log).to_csv("/workspace/ctrader-btc-bot/scripts/weekly_walk_forward_log.csv", index=False)
    print("\nLog: scripts/weekly_walk_forward_log.csv")
    print("\nNote: Only ~6 forward weeks in free 60-day M15 data — treat as illustration, not proof.")


if __name__ == "__main__":
    main()
