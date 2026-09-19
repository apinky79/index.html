# Learned BTC Autopilot — setup (cTrader + optional Telegram)

One **self-running** cBot built from all research. Optional **Telegram** nudges you on **regime**, not “change strategy every week.”

---

## Part 1: cTrader (required)

1. Open **cTrader → Algo → New cBot**.
2. Paste **`LearnedBtcAutopilotBot.cs`** → **Build**.
3. Chart: **BTCUSD**, timeframe **M15**.
4. Attach bot, enable **AutoTrading** on **demo** first.
5. Defaults match research:
   - Fixed **EMA 50/200** entries  
   - **H4** trend filter + **ADX** gates  
   - **0.8%** risk (toggle **Funded** → **0.4%**)  
   - **48-bar** max hold (~12h), Friday flat, 3.5% week brake  

The bot runs **by itself** — no manual entries, no weekly parameter changes in cTrader.

*(Same logic as `UltimateBtcBot.cs`; Autopilot adds clearer logging + H4 intraweek ADX pause.)*

### cTrader’s ~7-day restart (important)

cTrader **stops algorithm instances after about 7 days**. You must **stop and start** (or remove and re-attach) the cBot on the chart — parameters stay saved; the instance does not run forever.

**Routine (every 5–6 days, or when the log says so):**

1. Leave **open trades** alone — they stay on the account; the bot uses label `Learned-Autopilot` and picks them up again after restart.
2. **Stop** the cBot on the BTCUSD **M15** chart → **Start** it again (AutoTrading still on).
3. Optional: run `python3 record_ctrader_restart.py` in `telegram_advisor/` so Telegram restart reminders stay accurate.

**What survives a restart**

| Item | After restart |
|------|----------------|
| Open positions | Yes — same label, max-hold counted from entry time |
| Week-start equity & 3.5% week brake | Yes — if **Persist week state** is on (default) |
| Monday TRADE/SKIP gate this week | Yes — restored from local state file |
| 7-day cTrader timer | Reset — you get another ~7 days |

State file (Windows example):  
`Documents\cAlgo\Data\cBots\LearnedBtcAutopilotBot\autopilot_state.txt`

The bot logs a **REMINDER** once it has been running **6+ days** in the same instance.

---

## Part 2: Telegram advisor (optional)

Tells you each **Sunday** (or on demand):

- **TRADE** vs **SKIP** week (H4 ADX)  
- **Do NOT re-optimize strategy** (research result)  
- **Review** only if ADX **crossed** the 20 line or sim strongly diverges  

### Create a Telegram bot

1. In Telegram, message **@BotFather** → `/newbot` → copy **token**.  
2. Message your bot, then open:  
   `https://api.telegram.org/bot<TOKEN>/getUpdates`  
   and note your **chat_id**.

### Send advisory

```bash
cd ctrader-btc-bot/telegram_advisor
pip install -r requirements.txt
export TELEGRAM_BOT_TOKEN="your_token"
export TELEGRAM_CHAT_ID="your_chat_id"
python3 send_advisory.py
```

### Automate (Sunday 18:00 UTC)

Cron example:

```cron
0 18 * * 0 cd /path/to/ctrader-btc-bot/telegram_advisor && TELEGRAM_BOT_TOKEN=... TELEGRAM_CHAT_ID=... python3 send_advisory.py
```

Daily **cTrader restart** nudge (optional, if you use Telegram on a VPS):

```cron
0 9 * * * cd /path/to/ctrader-btc-bot/telegram_advisor && TELEGRAM_BOT_TOKEN=... TELEGRAM_CHAT_ID=... python3 send_restart_reminder.py
```

After each manual cBot restart on your PC:

```bash
python3 record_ctrader_restart.py
```

Or run on a Raspberry Pi / VPS — **Telegram does not run inside cTrader**; it’s a small sidecar.

---

## What Telegram will *not* do

- It will **not** change cTrader settings remotely.  
- It will **not** tell you to swap indicators every week (that **lost** vs fixed stack in tests).  
- It **will** remind you when **regime** shifts (ADX chop ↔ trend).

---

## Files

| File | Role |
|------|------|
| `LearnedBtcAutopilotBot.cs` | **Main autopilot** |
| `scripts/advisory_core.py` | Weekly regime logic |
| `telegram_advisor/send_advisory.py` | Telegram sender |
| `ULTIMATE_BOT.md` / `BRIGHTFUNDED_M15.md` | Background research |

---

## Disclaimer

Not financial advice. Demo forward before BrightFunded live. Telegram/Yahoo data may differ from your broker.
