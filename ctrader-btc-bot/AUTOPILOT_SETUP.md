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
