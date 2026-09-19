#!/usr/bin/env python3
"""Send weekly Learned BTC Autopilot advisory via Telegram."""

from __future__ import annotations

import os
import sys
import urllib.parse
import urllib.request

# scripts/ on path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scripts"))

from advisory_core import build_advisory, format_telegram_message


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

    ad = build_advisory()
    msg = format_telegram_message(ad)

    print(ad.summary)
    print("---")

    if not token or not chat_id:
        print("Telegram skipped: set TELEGRAM_BOT_TOKEN and TELEGRAM_CHAT_ID to send.")
        return

    # Telegram Markdown: escape problematic chars in free text
    send_telegram(msg, token=token, chat_id=chat_id)
    print("Sent to Telegram.")


if __name__ == "__main__":
    main()
