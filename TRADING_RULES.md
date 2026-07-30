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
| VolumeTrend | **OFF** |
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
- Run Funded at Challenge size  
- Buy during a digger cluster  
- Change 3.5% → 3% mid-plan without a dedicated A/B  
