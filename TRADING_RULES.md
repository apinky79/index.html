# UltimateTrader2026 — Locked Reading Rules

BrightFunded path: **$50k 2-Step Classic** first.  
Bot: BTCUSD m15 · Hunt weekly · max hold 1 · Approve off.

---

## 1. Risk (Monday resize)

| Phase | Risk per trade |
|---|---|
| **Challenge** | **0.8% of Monday Account.Equity** (not fixed $400) |
| **Funded** | **0.4% of Monday Account.Equity** (half Challenge) |

- Recalculate every **Monday 00:00 UTC** from **equity** (same clock as week DD brake).
- Fixed $400 / $200 is only a shortcut when equity ≈ $50k — not the live rule.
- When down, % shrinks size (survival). When up, % uses static-floor cushion (faster targets/payouts).

---

## 2. Always on (both phases)

| Control | Setting |
|---|---|
| Week DD brake | **ON** · **3.5%** from week-start equity · **no new entries** (don’t flatten) |
| News pause | **ON** · entries only · NFP / FOMC / CPI / CorePCE |
| Hard gates | MC P95 Max DD ≤ **8%** · MC PF ≥ **1.2** · min trades **5** |
| VolumeTrend | **ON as Condition** (always was — do not flip mid-plan) |
| SL range (opt) | **0.6 → 1.0** · step **0.1** |
| TP range (opt) | **1.8 → 2.8** · step **0.2** |
| Sort | Challenge-safe: MC P95 DD → MC PF → MC Win → P95 Min |
| Pick | **TOP PICK badge** (even if not row 1) |
| Commission | **10** (match scans) |
| Auth / stack | One TOP PICK per week · no mid-week param hopping |

If nothing passes hard gates → **SKIP** (flat week).

---

## 3. Phase modes

### Challenge
- Risk: **0.8% Monday equity**
- Trade every pass week (unless forecast gate says SKIP — §4)
- Job: hit Phase 1 **+10%** then Phase 2 **+5%** without daily 5% / static 10% breach
- After target hit for the phase: stop pushing size

### Funded (A2 — locked from testing)
- Risk: **0.4% Monday equity**
- After **2 consecutive red forward weeks** → **SKIP next 2 forward weeks**, then streak = 0
- Green week → streak = 0
- Take **first allowed payout** when green enough
- If down **~6–8% from funded start** → sit or cut further until a green week

---

## 4. Week forecast (scan → next week)

You cannot perfectly predict BTC week N+1 from week N.  
You **can** refuse bad odds. Use this gate **after** the scan, **before** you forward-trade.

### Step A — Scan quality (older week / opt)

| Check | Action |
|---|---|
| No row passes hard gates | **SKIP** next week |
| Red / failed / junk field on the pick | **SKIP** |
| TOP PICK missing or you’re forcing Final Score rank | **SKIP** — re-sort challenge-safe |

### Step B — Regime (recent forward weeks)

| Last forward results | Next week |
|---|---|
| **Challenge:** last 2–3 forwards green or SKIP | Trade (buy / continue OK) |
| **Challenge:** digger cluster (multiple reds) | **Do not buy / do not restart** until 2 clean weeks |
| **Funded:** 2 reds already | **Mandatory double-SKIP** (A2) — no forecast override |
| **Funded:** 1 red | Trade if Step A passes (streak = 1) |

### Step C — Soft caution (optional, don’t override A/B)

| Signal | Bias |
|---|---|
| MC P95 DD of pick is in the top band near 8% | Smaller edge — still trade only if A/B clear |
| Prior week was a deep digger and you’re still Challenge full size | Prefer SKIP / wait over “make it back” |

### Forecast log (fill each Sunday)

```
Scan week: ____ → Forward week: ____
Pass gates? Y/N
TOP PICK id: ____
Last 2 forwards: ____ / ____
Streak (funded): ____
Decision: TRADE / SKIP
Risk % this Monday: 0.8% or 0.4%
```

---

## 5. Pass and print (operating sequence)

1. **Test E** — 2–3 consecutive Challenge forwards green/SKIP before buying.  
   - E1 done: 20–26 Jul **+$942**  
   - E2: scan 20–26 Jul → forward **27 Jul–2 Aug** (after week closes)  
   - E3 if needed: scan 27 Jul–2 Aug → forward **3–9 Aug**
2. Buy **$50k** Classic only if Test E clean (not $100k first).
3. Run **Challenge rules** until funded.
4. Same day as funded: flip to **Funded A2** (0.4% + double-SKIP).
5. Payout early — equity ≠ money until withdrawn.

---

## 6. What testing already proved

| Idea | Result |
|---|---|
| Week DD ON 3.5% | Helps diggers; keep |
| Full 0.8% through kill zone | Does **not** print — floor risk |
| Funded A2 (half + skip 2 after 2 reds) | Best survival on digger stretch |
| Fixed $400 vs % of balance | Near $50k similar; **% better** when up/down |
| Floating % alone | Does **not** fix diggers without A2 skips |

---

## 7. Do not

- Sort/trade by Final Score over challenge-safe order  
- Turn week DD brake OFF to “make more”  
- Turn VolumeTrend OFF (baseline always had it ON as Condition)  
- Run Funded at Challenge size  
- Buy during a digger cluster  
- Change 3.5% → 3% mid-plan without a dedicated A/B  
- Deploy ML price prediction as the week gate before a simple HTF regime A/B  

---

## 8. Market forecast / favourability (research → next test)

**Truth:** You cannot reliably forecast next week’s BTC direction from last week’s candles alone.  
**What works in research + fits this EMA bot:** classify **regime** and only trade when it matches a trend system.

### What the literature / prop practice agrees on

| Filter type | Typical rule | Why it helps an EMA/trend bot |
|---|---|---|
| **HTF trend strength** | H4 or D1 **ADX(14) ≥ 25** = trade; **&lt; 20** = chop → SKIP | Stops trend logic in sideways bleed |
| **HTF direction (optional)** | Price vs EMA50/200 on H4/D1 | Prefer with-trend or block countertrend |
| **Volatility regime** | ATR(14) percentile; skip **high ATR + low ADX** (violent chop) | Digger weeks are often expansion + no clean trend |
| **Weekly EMA slope** | e.g. Weekly EMA50 slope over 4 bars; near-zero = FLAT → SKIP | Used on BTC prop EAs to block flat markets |
| **Hysteresis** | Need 3 bars in new state before flipping; exit chop only below 20 if enter above 25 | Stops flicker SKIP/TRADE every Monday |

Reported effect class (templates / systematic writeups): often **~30–50% less max DD** for **~10–20% less return** — survival trade, not magic alpha.

Your Strategy Selector already had **regime settings (disabled)** — same idea: chop vs trend gate.

### What we will NOT chase first
- Neural nets / sign forecasts of next-week return (costs + noise kill edge; research shows weak signals need cost-aware filters)
- Changing opt window length mid-challenge without a walk-forward study

### Test F — Regime gate (do after Test E / in parallel on kill zone)

**Candidate v1 (simplest, test first):**

Every Monday before forward week:

1. On **BTCUSD H4**, read **ADX(14)** at last closed bar Sunday/Monday open.  
2. If **ADX &lt; 20** → **SKIP** the forward week (no bot).  
3. If **ADX ≥ 25** → TRADE under normal Challenge/Funded rules.  
4. If **20–25** → TRADE but Funded only at **0.4%** (already); Challenge optional half-size trial later.

**Candidate v2 (if v1 skips too much or not enough):**

- SKIP when **ADX &lt; 20** **OR** (**ATR(14) H4 &gt; 70th percentile of last 90 days** AND **ADX &lt; 25**)  
  = block violent chop.

**How to score Test F**

Replay **same kill-zone forwards** as A2 (24 Nov → mid-Jun):

| Arm | Rule |
|---|---|
| Control | A2 rules only (no ADX gate) |
| F1 | A2 + Monday H4 ADX SKIP if &lt; 20 |

**Pass if:** end equity ≥ A2 **and** fewer / softer digger weeks, without skipping most green weeks (E1-type).

Log each Monday: `ADX_H4=__ decision=TRADE/SKIP`.

### Forecast checklist (updated)

```
Scan week: ____ → Forward: ____
Gates/TOP PICK: OK?
Last 2 forwards: ____ / ____
H4 ADX(14): ____  (≥25 trade / 20–25 caution / <20 SKIP)
Funded streak: ____
Decision: TRADE / SKIP
Risk: 0.8% Challenge or 0.4% Funded
```
