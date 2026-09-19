#!/usr/bin/env python3
"""Record that you restarted Learned Autopilot on cTrader (for Telegram reminders)."""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

RESTART_PATH = Path(__file__).resolve().parent / "ctrader_restart.json"


def main() -> None:
    RESTART_PATH.write_text(
        json.dumps(
            {
                "last_restart_utc": datetime.now(timezone.utc).isoformat(),
                "note": "LearnedBtcAutopilotBot on BTCUSD M15",
            },
            indent=2,
        )
    )
    print(f"Recorded restart at {RESTART_PATH}")


if __name__ == "__main__":
    main()
