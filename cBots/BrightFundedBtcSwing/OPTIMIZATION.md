# BrightFunded BTC Swing

Third bot — built after:
- **EMA Challenge** → MC DD OK eventually, too slow / weak forward
- **Challenge Pace** → faster, but m15 DD too high; H1 passed MC then **failed multi-week forward**

## Idea
Stay on **BTCUSD**. Don’t chase breakouts.

1. Trade **with trend** (price vs slow EMA)  
2. Enter on **RSI reclaim** after a pullback (dip then recover)  
3. Optional **ADX** so we skip dead ranges  
4. Wider ATR stops, 2R target, **$200** risk, max **2** trades/day  
5. Daily loss + profit locks  

## Install
Copy `BrightFundedBtcSwing.cs` → `Documents\cAlgo\Sources\Robots\` → Build  
Attach to **BTCUSD H1**  
Bot Trade ID: **BF_SWING**

---

## Locked settings
| Parameter | Value |
|---|---|
| Timeframe | **H1** |
| Bot Trade ID | **BF_SWING** |
| Trend EMA | **100** (search later if needed) |
| Use ADX Filter | **Yes** |
| ADX Period | **14** |
| ADX Min Level | **18** |
| RSI Period | **14** |
| Break Even at R | **1.0** |
| Trail | **0** |
| Trade Risk | **$200** |
| Daily Loss Limit | **$1500** |
| Daily Profit Lock | **$600** |
| Max Trades Per Day | **2** |
| Capital / Spread | **50000** / **400** |
| Window | **20–26 weeks** |

## Search (Pass A)
| Parameter | Min | Max | Step |
|---|---:|---:|---:|
| Long RSI Floor | 25 | 40 | 5 |
| Long RSI Reclaim | 40 | 50 | 5 |
| Short RSI Ceiling | 60 | 75 | 5 |
| Short RSI Reclaim | 50 | 60 | 5 |
| SL ATR Multiple | 1.6 | 2.6 | 0.2 |
| TP Risk Multiple | 1.5 | 3.0 | 0.5 |
| Trend EMA Length | 55 | 144 | 11 |  *(or lock 100 first)* |
| ADX Min Level | 15 | 25 | 2 |

Keep reclaim **above** floor for longs, reclaim **below** ceiling for shorts.

## Fitness (cBot)
| Filter | Value |
|---|---|
| Min Trades | **25** |
| Min PF | **1.20** |
| Min Net Profit | **2000** |
| Max Balance DD % | **7** |
| Max Consec Losers | **6** |

## Strategy Selector
- Min trades **8**
- Exp PF ~**1.25–1.3**
- MC DD **8%**
- MC PF **1.20**
- Safe mode P95 min return as you use now  

## Hard rule (from Pace lesson)
A pass is **not** done when Selector looks good.

Forward-test **at least 3–4 unseen weeks**:
- Drop if cumulative forward is negative / fading  
- Only continue if forward stays stable with real trades  

## Success target
| Gate | Target |
|---|---|
| MC DD | ≤ **8%** |
| MC PF | ≥ **1.20** |
| In-sample net | Prefer **$3k+** / 20–26 weeks |
| Forward 3–4 weeks | Not a multi-week bleed like Pace 635 |
