#!/usr/bin/env python3
"""Weekly advisory from research rules — shared by CLI and Telegram."""

from __future__ import annotations

import json
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path

import yfinance as yf

from full_matrix_research import STRATEGIES, adx_pack, apply_htf_filter
from weekly_prop_sim import run_sim_max_hold

STATE_PATH = Path(__file__).resolve().parent.parent / "telegram_advisor" / "state.json"

PICKS = [
    ("Ema50200", lambda d, h4: STRATEGIES["ema_50_200"](d)),
    ("Ema2155", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["ema_21_55"](d))),
    ("KeltnerHtf", lambda d, h4: apply_htf_filter(d, h4, STRATEGIES["keltner_break"](d))),
]


@dataclass
class WeeklyAdvisory:
    generated_at: str
    h4_adx: float
    week_action: str  # TRADE | SKIP
    autopilot_strategy: str
    max_hold_bars: int
    change_strategy_recommended: bool
    review_recommended: bool
    review_reason: str
    best_alternate: str | None
    summary: str


def _load_state() -> dict:
    if STATE_PATH.exists():
        return json.loads(STATE_PATH.read_text())
    return {}


def _save_state(adx: float, action: str) -> None:
    STATE_PATH.parent.mkdir(parents=True, exist_ok=True)
    STATE_PATH.write_text(
        json.dumps(
            {
                "last_h4_adx": adx,
                "last_action": action,
                "updated": datetime.now(timezone.utc).isoformat(),
            },
            indent=2,
        )
    )


def build_advisory() -> WeeklyAdvisory:
    t = yf.Ticker("BTC-USD")
    m15 = t.history(period="60d", interval="15m")
    h1 = t.history(period="60d", interval="1h")
    h4 = (
        h1.resample("4h")
        .agg({"Open": "first", "High": "max", "Low": "min", "Close": "last", "Volume": "sum"})
        .dropna()
    )
    adx_h4, _, _ = adx_pack(h4)
    h4_adx = float(adx_h4.iloc[-1])
    week_action = "TRADE" if h4_adx >= 20 else "SKIP"

    rows = []
    for mode, fn in PICKS:
        sig = fn(m15, h4)
        for hold in (48, 64):
            r = run_sim_max_hold(m15, sig, max_hold_bars=hold, atr_sl=2.5, rr=2.5, risk=0.008)
            score = r["green_week_pct"] * 2 + r["ret_pct"] * 0.2 - r["max_dd_pct"]
            rows.append((score, mode, hold, r))
    rows.sort(key=lambda x: -x[0])
    best = rows[0] if rows else None

    autopilot = "EMA 50/200 M15 + H4 trend filter (fixed)"
    hold = 48
    change_strategy = False
    review = False
    reason = ""
    alternate = None

    if best and best[1] != "Ema50200":
        alternate = f"{best[1]} hold={best[2]}"
        # Research: weekly hopping hurt — only flag review, not auto-change
        ema_row = next((x for x in rows if x[1] == "Ema50200"), None)
        if ema_row and best[0] > ema_row[0] * 1.25:
            review = True
            reason = "Recent sim ranks another trigger higher — optional manual review only (do not hop weekly)."

    prev = _load_state()
    prev_adx = prev.get("last_h4_adx")
    if prev_adx is not None:
        if prev_adx < 20 <= h4_adx:
            review = True
            reason = (reason + " " if reason else "") + "H4 ADX crossed above 20 — trend week starting."
        if prev_adx >= 20 > h4_adx:
            review = True
            reason = (reason + " " if reason else "") + "H4 ADX fell below 20 — expect SKIP weeks."

    _save_state(h4_adx, week_action)

    lines = [
        f"H4 ADX {h4_adx:.1f} → {week_action} week.",
        f"Autopilot: {autopilot}, max hold {hold} M15 bars (~12h).",
        "Strategy re-optimization: NOT needed (research: fixed stack beats weekly re-opt).",
    ]
    if week_action == "SKIP":
        lines.append("Bot should sit out new trades this week (Monday gate).")
    if review:
        lines.append(f"Review note: {reason}")
    if alternate:
        lines.append(f"Sim top alternate (info only): {alternate}")

    return WeeklyAdvisory(
        generated_at=datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC"),
        h4_adx=round(h4_adx, 2),
        week_action=week_action,
        autopilot_strategy=autopilot,
        max_hold_bars=hold,
        change_strategy_recommended=change_strategy,
        review_recommended=review,
        review_reason=reason or "None",
        best_alternate=alternate,
        summary="\n".join(lines),
    )


def format_telegram_message(ad: WeeklyAdvisory) -> str:
    icon = "🟢" if ad.week_action == "TRADE" else "🟡"
    opt = "❌ No strategy re-opt needed" if not ad.change_strategy_recommended else "⚠️ Review"
    rev = "📋 Review suggested" if ad.review_recommended else "✅ Routine week"
    return (
        f"{icon} *Learned BTC Autopilot*\n"
        f"_{ad.generated_at}_\n\n"
        f"*Week:* {ad.week_action}\n"
        f"*H4 ADX:* {ad.h4_adx}\n"
        f"*Bot:* {ad.autopilot_strategy}\n"
        f"*Max hold:* {ad.max_hold_bars} bars\n\n"
        f"{opt}\n{rev}\n"
        f"{ad.review_reason if ad.review_recommended else 'Keep cBot running on M15 demo/live.'}\n\n"
        f"_{ad.best_alternate or 'Fixed EMA 50/200 remains default.'}_"
    )


if __name__ == "__main__":
    ad = build_advisory()
    print(ad.summary)
