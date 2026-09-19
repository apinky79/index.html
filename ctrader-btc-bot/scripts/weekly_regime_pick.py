#!/usr/bin/env python3
"""Sunday helper: rank M15 triggers for next week (prop-style)."""

from __future__ import annotations

import yfinance as yf

from full_matrix_research import STRATEGIES, adx_pack, apply_htf_filter
from weekly_prop_sim import run_sim_max_hold

PICKS = [
    ("Ema50200", "ema_50_200", lambda d, h4: STRATEGIES["ema_50_200"](d)),
    ("Ema2155", "ema_21_55", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["ema_21_55"](d))),
    ("KeltnerHtf", "keltner", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["keltner_break"](d))),
    ("AroonHtf", "aroon", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["aroon_cross_25"](d))),
    ("DiCrossHtf", "di", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["di_cross"](d))),
]


def main() -> None:
    t = yf.Ticker("BTC-USD")
    m15 = t.history(period="60d", interval="15m")
    h1 = t.history(period="60d", interval="1h")
    h4 = h1.resample("4h").agg({"Open": "first", "High": "max", "Low": "min", "Close": "last", "Volume": "sum"}).dropna()
    adx_h4, _, _ = adx_pack(h4)
    last_adx = float(adx_h4.iloc[-1])
    monday_hint = "TRADE" if last_adx >= 20 else "SKIP (ADX chop)"

    print("=== BrightFunded M15 — weekly pick ===")
    print(f"H4 ADX (last closed): {last_adx:.1f}  →  {monday_hint}\n")

    rows = []
    for mode, _, fn in PICKS:
        sig = fn(m15, h4)
        for hold in (48, 64):
            r = run_sim_max_hold(m15, sig, max_hold_bars=hold, atr_sl=2.5, rr=2.5, risk=0.008)
            score = r["green_week_pct"] * 2 + r["ret_pct"] * 0.2 - r["max_dd_pct"]
            rows.append((score, mode, hold, r))

    rows.sort(key=lambda x: -x[0])
    print("Ranked triggers (0.8% risk, 3.5% week brake, 2.5 ATR / 2.5R):\n")
    for score, mode, hold, r in rows:
        print(
            f"  {mode}  max_hold={hold} ({hold // 4}h)  |  green {r['green_weeks']}/{r['weeks']}  "
            f"ret={r['ret_pct']:.1f}%  dd={r['max_dd_pct']:.1f}%  trades={r['trades']}  "
            f"worst_wk=${r['worst_week']:.0f}"
        )

    best = rows[0]
    print(f"\n→ Suggested cBot: Trigger={best[1]}, MaxHoldBars={best[2]}")


if __name__ == "__main__":
    main()
