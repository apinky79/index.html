# SmcBtcSideExperiment / SMC_Testing (cTrader)

**Side experiment — CLOSED.** Stay on **UltimateTrader + ADX H4 (Test G)** for Challenge / funded.

### Panel result (S1–S7, then Lite / max-hold / Enter On CHoCH)
- Never cleared weekly hard gates (min **5** closed trades) in the Strategy Finder / file inspector
- Inspector max on retest weeks was ~**2–4** trades → always **SKIP**
- Not a replacement for G

Code kept for reference only (`SMC_Testing.cs` v1.4). Do not deploy live.

---

## Install (reference only)

1. Open **SMC Testing** in Automate  
2. **Ctrl+A → Delete** (wipe template)  
3. Paste entire `SMC_Testing.cs`  
4. **Build**  
5. Attach **BTCUSD m15** · Bias TF **Hour4**

## What was tried

### Core SMC
H4 BOS bias → m15 liquidity sweep → CHoCH → order-block retest.

### Extras
News pause 90/45 · Max hold 24h · SL 0.6–1.0 / TP 1.8–2.8 · optional L2 DOM (OFF in BT)

### Lite toggles tried (still failed min 5)
HTF bias OFF · Sweep OFF · Enter On CHoCH ON · OB Reclaim 0.30 · Max bars 72 · One trade/setup OFF

## Live Challenge
**UltimateTrader + ADX H4 (G)** only.
