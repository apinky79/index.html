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
| ADX Momentum | **ON** · Condition · Trending · **25** / Ranging **20** · Period **14** · TF **h4** (Test G winner — locked) |
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
| **Challenge:** digger cluster (multiple reds) | Sit / don’t restart until 2 clean weeks under **ADX H4** |
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

**Test E cancelled.** Buy / Challenge runs under **Test G stack** (ADX H4 on).

1. Buy **$50k** Classic when ready (not $100k first).  
2. **Live weekly cycle:** Opt **and** forward with **ADX H4 on** (same settings — you can’t see next week, so opt must match live). If no TOP PICK / hard-gate fail → **SKIP**.  
   *(Test G A/B used “opt ADX off then flip H4 on” only to measure the filter — not the live method.)* 
3. Challenge: **0.8% Monday equity** + always-on stack (§2) including **ADX H4**.  
4. Same day as funded: flip to **Funded A2** (0.4% + double-SKIP).  
5. Payout early — equity ≠ money until withdrawn.

E1 archive (not a buy gate anymore): ADX off +$942 · H4 +$301 · m15 −$427 on 20–26 Jul.

---

## 6. What testing already proved

| Idea | Result |
|---|---|
| Week DD ON 3.5% | Helps diggers; keep |
| Full 0.8% through kill zone | Does **not** print — floor risk |
| Funded A2 (half + skip 2 after 2 reds) | Best survival on digger stretch |
| Fixed $400 vs % of balance | Near $50k similar; **% better** when up/down |
| ADX Momentum (Test G) | **Enable H4** — Condition · Trending · 25/20 · Period 14 · TF **h4**. Beat Control by ~$6.2k on panel; leads m15. |

---

## 7. Do not

- Run Challenge with ADX **off** or **m15** (locked = **H4 only**)  
- Sort/trade by Final Score over challenge-safe order  
- Turn week DD brake OFF to “make more”  
- Turn VolumeTrend OFF (baseline always had it ON as Condition)  
- Widen / shrink SL or TP ranges mid-plan (locked: SL 0.6–1.0 / 0.1 · TP 1.8–2.8 / 0.2)  
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

### Test G — ADX Momentum in-bot (run while waiting on Test E)

**Status:** Active side A/B.  
**Purpose:** See if ADX Momentum as an entry Condition changes digger vs green weeks.  
**Does not replace Test E** — keep E on the current stack (ADX Momentum **Disabled**). Buy decision stays on E only.

#### Optset flip (only these)

| Param | Control | ADX H4 | ADX m15 |
|---|---|---|---|
| ADX Momentum Mode | **Disabled** | **Condition** | **Condition** |
| ADX Momentum Condition | — | **Trending** | **Trending** |
| Trending Threshold | — | **25** | **25** |
| Ranging Threshold | — | **20** | **20** |
| ADX Period | — | **14** | **14** |
| ADX Time Frame | — | **h4** | **m15** |

Leave alone: VolumeTrend Condition/Rising · Week DD 3.5% · News · SL 0.6–1.0/0.1 · TP 1.8–2.8/0.2 · Entry EMATest · Double EMA Trigger · Challenge **0.8% Monday equity** · hard gates · challenge-safe sort · TOP PICK.

Do **not** change Exit ADX / ADX Trend Mode for this first panel.

**G1 finding:** m15 filtered harder than H4 on the digger → continue panel with **m15 as primary arm**; H4 optional.

#### Fixed scan panel (8 weeks)

Same scan → forward pair. Reuse Control; re-run ADX arms as needed.

**Opt protocol**

| Mode | How |
|---|---|
| **Live (now)** | Opt **with ADX H4 on** (Condition · Trending · 25/20 · Period 14). Forward the **same** settings. No pass → SKIP. |
| **Test G A/B only (done)** | Opt ADX off, then forward-flip arms — was for measuring the filter, not live. |

| # | Type | Scan → Forward | Control | ADX H4 | ADX m15 | Notes |
|---|---|---|---|---|---|---|
| G1 | Digger | 17–23 Nov → **24–30 Nov** | **−2152 / 4.56%** (5t) | **−1699 / 3.65%** (4t) | **−816 / 1.89%** (2t) | m15 best digger save |
| G2 | Digger | 1–7 Dec → **8–14 Dec** | **−1788 / 4.09%** (4t) | **0 / 0%** (0t) | **−167 / 3.38%** (3t) | H4 best (full skip); m15 still +1621 vs Control |
| G3 | Digger | 15–21 Dec → **22–28 Dec** | **−1006 / 4.31%** (6t) | **0 / 0%** (0t) | **+252 / 2.64%** (3t) | H4 full skip; m15 turns digger → green |
| G4 | Green | 12–18 Jan → **19–25 Jan** | **+2169 / 2.54%** (5t) | **+2249 / 2.53%** (5t) | **+2000 / 1.25%** (2t) | All green; m15 quieter (−169 vs Control) |
| G5 | Digger | 9–15 Feb → **16–22 Feb** | **−1939 / 4.28%** (4t) | **0 / 0%** (0t) | **−2021 / 4.45%** (4t) | H4 full skip; **m15 no help** (−82 vs Control) |
| G6 | Late digger | 1–7 Jun → **8–14 Jun** | **−1930 / 5.69%** (4t) | **−1930 / 5.69%** (4t) | **−1581 / 4.05%** (3t) | H4 no delta; m15 softer (−349) |
| G7 | Matched | 13–19 Jul → **20–26 Jul** | **−263 / 3.66%** (9t) | **+640 / 2.00%** (4t) | **+817 / 0.61%** (1t) | m15 best; E1 +942 ≠ this Control |
| G8 | Live | 20–26 Jul → **27 Jul–2 Aug** | (E2) | | | |

**FINAL G1–G7:** Control **−6909** · H4 **−740** · m15 **−1516**  
→ Both beat Control by ~$5–6k. **H4 wins panel** (best digger skips G2/G3/G5). m15 strong; only failed G5.  
→ **Lock for Challenge:** ADX Momentum Condition · Trending · 25/20 · Period 14 · **h4**. Test E dropped — G stack is live.

If a scan has **no TOP PICK / hard-gate fail** on either arm → log **SKIP** (flat) — still a valid comparison.

#### How to score

| Metric | Pass (esp. **m15**) if… |
|---|---|
| Diggers G1–G6 | Sum PnL **better** than Control (less red) **or** more SKIP weeks that would have been diggers |
| Green G7 | Still green, or SKIP (not a new digger) |
| G8 | Informational vs E2 |
| Overall | Sum(G1–G7) ADX ≥ Control, without turning G7 red |

**If m15 helps diggers but kills G7** → fail (don’t enable for live Challenge).  
**If m15 helps diggers and G7 stays green/SKIP** → consider enabling for Challenge; then decide Funded later.  
**If no clear delta** → leave Disabled; move on.

Threshold fallback (only if ADX arm is almost always SKIP): retry G1 + G7 with Trending Threshold **20** once — not the full panel.

### Test F — Manual Monday H4 ADX week gate (later)

Separate from in-bot Momentum (Test G). After Test E / after G panel if useful:

1. On BTCUSD H4, ADX(14) at last closed bar Sunday/Monday open.  
2. ADX &lt; 20 → SKIP week · ≥ 25 → TRADE · 20–25 caution.  

Replay kill-zone vs A2; log `ADX_H4=__ decision=TRADE/SKIP`.

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
