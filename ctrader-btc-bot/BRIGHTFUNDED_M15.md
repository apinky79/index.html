# BrightFunded $50k Classic — BTC M15 weekly playbook

Built for your goals: **M15 entries**, **several trades per week**, **not** sitting in one position for days, **weekly regime** updates, **prop-safe** risk.

---

## Why M15 (and not H4 only)

Full-matrix research on 60 days of M15 data + **prop-style sim** (`scripts/weekly_prop_sim.py`):

| Setup | Trades (~9 weeks) | Green weeks | Max DD (sim) | Hold cap |
|--------|-------------------|-------------|--------------|----------|
| **EMA 50/200 + H4 filter** | ~28 (~3/week) | **9/9 (100%)** | ~2.4% | **48–64 M15 bars (12–16h)** |
| EMA 50/200, 8h cap | ~28 | 7/9 | ~1.6% | 32 bars |
| Keltner + H4 (more trades) | ~92 | 5/9 | ~7.6% | — |

**Recommendation:** Stay on **M15**. Use **EMA 50/200** with **max hold 48 bars (12 hours)** or **64 bars (16 hours)**.  
M30 was **less consistent** green weeks in the same test — only switch if your forward demo shows otherwise.

---

## Bot to use

**`BrightFundedBtcM15Bot.cs`**

| Setting | Challenge ($50k Classic) | Funded |
|---------|--------------------------|--------|
| Risk | **0.8%** Monday equity | Turn **Funded phase** ON → **0.4%** |
| Week DD brake | **3.5%** (no new entries) | Same |
| Monday gate | H4 **ADX &lt; 20** → **SKIP week** | Same |
| Trigger (default) | **Ema50200** | Re-pick weekly if needed |
| H4 EMA55 filter | **ON** | ON |
| Max hold | **48** M15 bars (~12h) | Same |
| Stop / target | **2.5 × ATR**, **2.5R** | Same |
| Friday 20:00 UTC | **Flatten** (optional, default ON) | Avoid weekend gap risk |

Chart: **BTCUSD M15 only.**

---

## Weekly optimization ritual (Sunday UTC)

You said you want to **optimise weekly** for BTC regime. Do this **once per week**, not mid-week (matches your “no param hopping” rule).

### Step 1 — Regime read (Monday preview)

On **Sunday**, note **H4 ADX(14)** on the last closed H4 candle:

- **ADX ≥ 25** → trend week → trade  
- **ADX 20–25** → cautious → trade with default trigger or half size if funded  
- **ADX &lt; 20** → bot **SKIPs** the week automatically if Monday gate is ON  

### Step 2 — Re-run M15 prop sim (optional, 2 minutes)

```bash
cd ctrader-btc-bot/scripts
python3 weekly_prop_sim.py
```

Pick the trigger with:

- Highest **green week %**  
- **Worst week** not beyond what you tolerate on $50k (~**-$1,750** ≈ 3.5% week)  
- Enough **trades** (≥ ~2–3 per week)

Map to bot parameter **Entry trigger**:

| Sim name | Bot `WeeklyTriggerMode` |
|----------|-------------------------|
| ema_50_200 | **Ema50200** (default) |
| ema_21_55+HTF | **Ema2155** |
| keltner+HTF | **KeltnerBreakHtf** |
| aroon+HTF | **Aroon25Htf** |
| di_cross+HTF | **DiCrossHtf** |

### Step 3 — Log (copy each Sunday)

```
Week of: ___________
H4 ADX: _____  → TRADE / SKIP
Trigger chosen: ___________
Max hold bars: 48 / 64
Phase: Challenge 0.8% / Funded 0.4%
Forward week result (fill Monday): $_____
```

### Step 4 — cTrader

Change **only** `Entry trigger` / `Max hold` **before** Monday open if Step 2 says so.  
Do **not** change risk or DD mid-week.

---

## What “weekly profit” really means

- The bot targets **many small edges** (~3 M15 trades per week in sim), closed within **12–16 hours**.  
- Some weeks will still be **red** — the **3.5% week brake** stops digging.  
- **BrightFunded** daily **5%** and static **10%** rules are on **you** + broker dashboard — this bot does not read them automatically.  
- **News** (NFP, FOMC, CPI, Core PCE): pause the cBot manually or avoid entries — not coded (broker calendars differ).

---

## Files

| File | Purpose |
|------|---------|
| `BrightFundedBtcM15Bot.cs` | Prop-focused M15 cBot |
| `scripts/weekly_prop_sim.py` | Weekly green-week / max-hold research |
| `TRADING_RULES.md` | Your broader prop stack (forecast, funded A2) |

---

## Disclaimer

Simulation on Yahoo BTC ≠ BrightFunded fills, commission ($10/rt), or rules changes. **Demo forward** on cTrader before challenge.
