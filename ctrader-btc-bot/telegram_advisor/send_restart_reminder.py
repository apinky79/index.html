#!/usr/bin/env python3
"""Telegram reminder when cTrader cBot may be near the ~7-day stop limit."""

from __future__ import annotations

import json
import os
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

RESTART_PATH = Path(__file__).resolve().parent / "ctrader_restart.json"
WARN_DAYS = 6.0


def send_telegram(text: str, *, token: str, chat_id: str) -> None:
    url = f"https://api.telegram.org/bot{token}/sendMessage"
    body = urllib.parse.urlencode(
        {
            "chat_id": chat_id,
            "text": text,
            "parse_mode": "Markdown",
            "disable_web_page_preview": "true",
        }
    ).encode()
    req = urllib.request.Request(url, data=body, method="POST")
    with urllib.request.urlopen(req, timeout=30) as resp:
        if resp.status != 200:
            raise RuntimeError(f"Telegram HTTP {resp.status}")


def main() -> None:
    token = os.environ.get("TELEGRAM_BOT_TOKEN", "").strip()
    chat_id = os.environ.get("TELEGRAM_CHAT_ID", "").strip()

    if not RESTART_PATH.exists():
        msg = (
            "⏱ *cTrader restart*\n\n"
            "No restart recorded yet. After you restart *Learned Autopilot* on BTCUSD M15, run:\n"
            "`record_ctrader_restart.py`\n\n"
            "cTrader stops cBots after ~7 days."
        )
        print(msg)
        if token and chat_id:
            send_telegram(msg, token=token, chat_id=chat_id)
            print("Sent to Telegram.")
        return

    data = json.loads(RESTART_PATH.read_text())
    last = datetime.fromisoformat(data["last_restart_utc"])
    if last.tzinfo is None:
        last = last.replace(tzinfo=timezone.utc)
    days = (datetime.now(timezone.utc) - last).total_days()

    if days < WARN_DAYS:
        print(f"OK: {days:.1f} days since restart (warn at {WARN_DAYS}).")
        return

    msg = (
        f"⚠️ *cTrader ~7-day limit*\n\n"
        f"Last recorded restart: *{days:.1f} days* ago.\n"
        f"Stop → start *LearnedBtcAutopilotBot* on *BTCUSD M15* (AutoTrading on).\n"
        f"Open trades keep running; then run `record_ctrader_restart.py`."
    )
    print(msg)
    if not token or not chat_id:
        print("Telegram skipped: set TELEGRAM_BOT_TOKEN and TELEGRAM_CHAT_ID.")
        return
    send_telegram(msg, token=token, chat_id=chat_id)
    print("Sent to Telegram.")


if __name__ == "__main__":
    main()
