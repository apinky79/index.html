// -------------------------------------------------------------------------------------------------
// SMC_Testing — SIDE EXPERIMENT ONLY (does not replace UltimateTrader2026 / Test G)
//
// INSTALL IN cTRADER (important — fixes CS1022 / CS0106):
//   1. Open your "SMC Testing" cBot in Automate
//   2. Ctrl+A (select ALL) → Delete
//   3. Paste THIS ENTIRE file (from "using System" to the last })
//   4. Build
// Do NOT paste inside the empty OnStart/OnTick template — replace the whole file.
//
// Lean Smart Money Concepts cBot:
//   HTF (default H4)  → market structure bias (BOS direction)
//   LTF (chart, m15)  → liquidity sweep → CHoCH → order-block retest entry
// No Level-2 order book required — OHLC only.
// -------------------------------------------------------------------------------------------------

using System;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    public enum SmcTradeDirectionMode
    {
        LongAndShort,
        LongOnly,
        ShortOnly
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SMC_Testing : Robot
    {
        // ---- Identity / direction ---------------------------------------------------------------

        [Parameter("Bot Trade ID", DefaultValue = "SMC_BTC_SIDE", Group = "Trade Options")]
        public string BotTradeId { get; set; }

        [Parameter("Order Direction", DefaultValue = SmcTradeDirectionMode.LongAndShort, Group = "Trade Options")]
        public SmcTradeDirectionMode OrderDirection { get; set; }

        // ---- Timeframes -------------------------------------------------------------------------

        [Parameter("Bias Time Frame (HTF)", DefaultValue = "Hour4", Group = "Timeframes")]
        public TimeFrame BiasTimeFrame { get; set; }

        [Parameter("Swing Lookback (bars)", DefaultValue = 2, MinValue = 1, MaxValue = 5, Group = "Structure")]
        public int SwingLookback { get; set; }

        [Parameter("Structure Pivot Strength", DefaultValue = 3, MinValue = 2, MaxValue = 8, Group = "Structure")]
        public int PivotStrength { get; set; }

        // ---- SMC filters ------------------------------------------------------------------------

        [Parameter("Require HTF Bias Align", DefaultValue = true, Group = "SMC Filters")]
        public bool RequireHtfBias { get; set; }

        [Parameter("Require Liquidity Sweep", DefaultValue = true, Group = "SMC Filters")]
        public bool RequireLiquiditySweep { get; set; }

        [Parameter("Sweep Wick Ratio Min", DefaultValue = 0.35, MinValue = 0.1, MaxValue = 0.9, Group = "SMC Filters")]
        public double SweepWickRatioMin { get; set; }

        [Parameter("Max Bars After CHoCH for OB Entry", DefaultValue = 24, MinValue = 4, MaxValue = 100, Group = "SMC Filters")]
        public int MaxBarsAfterChoCh { get; set; }

        // ---- Risk -------------------------------------------------------------------------------

        [Parameter("Trade Risk (USD)", DefaultValue = 400, MinValue = 10, Group = "Risk")]
        public double TradeRiskUsd { get; set; }

        [Parameter("SL Buffer (pips)", DefaultValue = 20, MinValue = 0, Group = "Risk")]
        public double SlBufferPips { get; set; }

        [Parameter("TP Risk Multiplier", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 10, Group = "Risk")]
        public double TpRiskMultiplier { get; set; }

        [Parameter("Max Open Positions", DefaultValue = 1, MinValue = 1, MaxValue = 3, Group = "Risk")]
        public int MaxOpenPositions { get; set; }

        [Parameter("One Trade Per Setup", DefaultValue = true, Group = "Risk")]
        public bool OneTradePerSetup { get; set; }

        // ---- Week DD brake (optional, mirrors G stack spirit) ------------------------------------

        [Parameter("Enable Week DD Brake", DefaultValue = true, Group = "Week DD Brake")]
        public bool EnableWeekDdBrake { get; set; }

        [Parameter("Week DD Brake %", DefaultValue = 3.5, MinValue = 0.5, MaxValue = 20, Group = "Week DD Brake")]
        public double WeekDdBrakePct { get; set; }

        // ---- Internals --------------------------------------------------------------------------

        private Bars _htf;
        private int _htfBias; // 1 bull, -1 bear, 0 unknown

        private double _ltfLastSwingHigh;
        private double _ltfLastSwingLow;
        private int _ltfLastSwingHighIndex = -1;
        private int _ltfLastSwingLowIndex = -1;

        private bool _sweepHighDone;
        private bool _sweepLowDone;
        private int _choChBarIndex = -1;
        private int _choChDir; // 1 bullish CHoCH (after low sweep), -1 bearish
        private double _obHigh;
        private double _obLow;
        private bool _obArmed;
        private bool _setupTraded;

        private DateTime _weekStartUtc;
        private double _weekStartEquity;

        protected override void OnStart()
        {
            _htf = MarketData.GetBars(BiasTimeFrame);
            ResetWeekIfNeeded(force: true);
            Print("SMC_Testing started — SIDE EXPERIMENT. Chart TF={0} Bias TF={1}", Bars.TimeFrame, BiasTimeFrame);
            Print("No order book used — OHLC SMC only. Do not replace UltimateTrader2026 / Test G.");
        }

        protected override void OnBar()
        {
            ResetWeekIfNeeded(force: false);

            if (Bars.Count < PivotStrength * 4 + 10 || _htf.Count < PivotStrength * 4 + 10)
                return;

            UpdateHtfBias();
            UpdateLtfSwings();

            // Manage new closed bar = index Count-2
            int i = Bars.Count - 2;
            if (i < PivotStrength + 2)
                return;

            DetectSweep(i);
            DetectChoCh(i);
            TryEnterOnOrderBlock(i);
        }

        // =========================================================================================
        // HTF bias (last BOS on bias TF)
        // =========================================================================================

        private void UpdateHtfBias()
        {
            int last = _htf.Count - 2;
            if (last < PivotStrength * 3)
                return;

            double sh = double.NaN, sl = double.NaN;
            int shIdx = -1, slIdx = -1;

            for (int i = PivotStrength; i <= last - PivotStrength; i++)
            {
                if (IsPivotHigh(_htf, i, PivotStrength))
                {
                    sh = _htf.HighPrices[i];
                    shIdx = i;
                }
                if (IsPivotLow(_htf, i, PivotStrength))
                {
                    sl = _htf.LowPrices[i];
                    slIdx = i;
                }
            }

            // Walk recent closes for BOS
            for (int i = Math.Max(PivotStrength * 2, last - 40); i <= last; i++)
            {
                if (!double.IsNaN(sh) && _htf.ClosePrices[i] > sh)
                    _htfBias = 1;
                if (!double.IsNaN(sl) && _htf.ClosePrices[i] < sl)
                    _htfBias = -1;

                if (IsPivotHigh(_htf, i, PivotStrength))
                {
                    sh = _htf.HighPrices[i];
                    shIdx = i;
                }
                if (IsPivotLow(_htf, i, PivotStrength))
                {
                    sl = _htf.LowPrices[i];
                    slIdx = i;
                }
            }

            // silence unused
            _ = shIdx;
            _ = slIdx;
        }

        // =========================================================================================
        // LTF swings
        // =========================================================================================

        private void UpdateLtfSwings()
        {
            int last = Bars.Count - 2;
            for (int i = Math.Max(PivotStrength, last - 80); i <= last - PivotStrength; i++)
            {
                if (IsPivotHigh(Bars, i, PivotStrength))
                {
                    _ltfLastSwingHigh = Bars.HighPrices[i];
                    _ltfLastSwingHighIndex = i;
                }
                if (IsPivotLow(Bars, i, PivotStrength))
                {
                    _ltfLastSwingLow = Bars.LowPrices[i];
                    _ltfLastSwingLowIndex = i;
                }
            }
        }

        private static bool IsPivotHigh(Bars bars, int i, int strength)
        {
            double h = bars.HighPrices[i];
            for (int k = 1; k <= strength; k++)
            {
                if (bars.HighPrices[i - k] >= h || bars.HighPrices[i + k] > h)
                    return false;
            }
            return true;
        }

        private static bool IsPivotLow(Bars bars, int i, int strength)
        {
            double l = bars.LowPrices[i];
            for (int k = 1; k <= strength; k++)
            {
                if (bars.LowPrices[i - k] <= l || bars.LowPrices[i + k] < l)
                    return false;
            }
            return true;
        }

        // =========================================================================================
        // Liquidity sweep (wick beyond swing, close back inside)
        // =========================================================================================

        private void DetectSweep(int i)
        {
            double open = Bars.OpenPrices[i];
            double close = Bars.ClosePrices[i];
            double high = Bars.HighPrices[i];
            double low = Bars.LowPrices[i];
            double range = high - low;
            if (range <= Symbol.TickSize)
                return;

            // Sweep highs (sell-side liquidity) → sets up potential bearish CHoCH
            if (_ltfLastSwingHighIndex >= 0 && high > _ltfLastSwingHigh && close < _ltfLastSwingHigh)
            {
                double upperWick = high - Math.Max(open, close);
                if (upperWick / range >= SweepWickRatioMin)
                {
                    _sweepHighDone = true;
                    _sweepLowDone = false;
                    _obArmed = false;
                    _setupTraded = false;
                    _choChBarIndex = -1;
                    Print("{0} Sweep HIGH @ {1:F2} (swing {2:F2})", Bars.OpenTimes[i], high, _ltfLastSwingHigh);
                }
            }

            // Sweep lows (buy-side liquidity) → sets up potential bullish CHoCH
            if (_ltfLastSwingLowIndex >= 0 && low < _ltfLastSwingLow && close > _ltfLastSwingLow)
            {
                double lowerWick = Math.Min(open, close) - low;
                if (lowerWick / range >= SweepWickRatioMin)
                {
                    _sweepLowDone = true;
                    _sweepHighDone = false;
                    _obArmed = false;
                    _setupTraded = false;
                    _choChBarIndex = -1;
                    Print("{0} Sweep LOW @ {1:F2} (swing {2:F2})", Bars.OpenTimes[i], low, _ltfLastSwingLow);
                }
            }
        }

        // =========================================================================================
        // CHoCH + order block (last opposing candle before displacement)
        // =========================================================================================

        private void DetectChoCh(int i)
        {
            if (RequireLiquiditySweep && !_sweepHighDone && !_sweepLowDone)
                return;

            // Bullish CHoCH: after low sweep, close breaks last swing high
            if (_sweepLowDone && _ltfLastSwingHighIndex >= 0 && Bars.ClosePrices[i] > _ltfLastSwingHigh)
            {
                if (!(RequireHtfBias && _htfBias < 0))
                {
                    _choChDir = 1;
                    _choChBarIndex = i;
                    MarkOrderBlock(i, bullish: true);
                    _sweepLowDone = false;
                    Print("{0} Bullish CHoCH — OB [{1:F2} .. {2:F2}]", Bars.OpenTimes[i], _obLow, _obHigh);
                    return;
                }
            }

            // Bearish CHoCH: after high sweep, close breaks last swing low
            if (_sweepHighDone && _ltfLastSwingLowIndex >= 0 && Bars.ClosePrices[i] < _ltfLastSwingLow)
            {
                if (RequireHtfBias && _htfBias > 0)
                    return;

                _choChDir = -1;
                _choChBarIndex = i;
                MarkOrderBlock(i, bullish: false);
                _sweepHighDone = false;
                Print("{0} Bearish CHoCH — OB [{1:F2} .. {2:F2}]", Bars.OpenTimes[i], _obLow, _obHigh);
            }
        }

        private void MarkOrderBlock(int choChIndex, bool bullish)
        {
            // Walk back for last opposing candle
            for (int j = choChIndex - 1; j >= Math.Max(0, choChIndex - 15); j--)
            {
                bool bearishCandle = Bars.ClosePrices[j] < Bars.OpenPrices[j];
                bool bullishCandle = Bars.ClosePrices[j] > Bars.OpenPrices[j];

                if (bullish && bearishCandle)
                {
                    _obHigh = Bars.HighPrices[j];
                    _obLow = Bars.LowPrices[j];
                    _obArmed = true;
                    _setupTraded = false;
                    return;
                }
                if (!bullish && bullishCandle)
                {
                    _obHigh = Bars.HighPrices[j];
                    _obLow = Bars.LowPrices[j];
                    _obArmed = true;
                    _setupTraded = false;
                    return;
                }
            }

            // Fallback: use prior bar
            int p = Math.Max(0, choChIndex - 1);
            _obHigh = Bars.HighPrices[p];
            _obLow = Bars.LowPrices[p];
            _obArmed = true;
            _setupTraded = false;
        }

        // =========================================================================================
        // Entry on OB retest
        // =========================================================================================

        private void TryEnterOnOrderBlock(int i)
        {
            if (!_obArmed || _choChBarIndex < 0)
                return;
            if (OneTradePerSetup && _setupTraded)
                return;
            if (i - _choChBarIndex > MaxBarsAfterChoCh)
            {
                _obArmed = false;
                return;
            }
            if (CountOurPositions() >= MaxOpenPositions)
                return;
            if (EnableWeekDdBrake && IsWeekDdBrakeActive())
                return;

            double high = Bars.HighPrices[i];
            double low = Bars.LowPrices[i];
            double close = Bars.ClosePrices[i];

            bool longOk = OrderDirection != SmcTradeDirectionMode.ShortOnly;
            bool shortOk = OrderDirection != SmcTradeDirectionMode.LongOnly;

            // Long: price revisits OB and closes back above OB mid / high zone
            if (_choChDir > 0 && longOk)
            {
                bool touched = low <= _obHigh && low >= _obLow - Symbol.PipSize * SlBufferPips;
                bool reclaim = close >= (_obLow + _obHigh) * 0.5;
                if (touched && reclaim)
                {
                    EnterLong();
                    _setupTraded = true;
                    _obArmed = false;
                }
            }

            // Short: price revisits OB and closes back below OB mid
            if (_choChDir < 0 && shortOk)
            {
                bool touched = high >= _obLow && high <= _obHigh + Symbol.PipSize * SlBufferPips;
                bool reject = close <= (_obLow + _obHigh) * 0.5;
                if (touched && reject)
                {
                    EnterShort();
                    _setupTraded = true;
                    _obArmed = false;
                }
            }
        }

        private void EnterLong()
        {
            double slPrice = _obLow - Symbol.PipSize * SlBufferPips;
            double entry = Symbol.Ask;
            double riskPerUnit = entry - slPrice;
            if (riskPerUnit <= 0)
                return;

            double volume = VolumeForRisk(riskPerUnit);
            if (volume <= 0)
                return;

            double tpPrice = entry + riskPerUnit * TpRiskMultiplier;
            var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, volume, BotTradeId, slPrice, tpPrice);
            if (result.IsSuccessful)
                Print("LONG entry={0:F2} SL={1:F2} TP={2:F2} vol={3}", entry, slPrice, tpPrice, volume);
            else
                Print("LONG failed: {0}", result.Error);
        }

        private void EnterShort()
        {
            double slPrice = _obHigh + Symbol.PipSize * SlBufferPips;
            double entry = Symbol.Bid;
            double riskPerUnit = slPrice - entry;
            if (riskPerUnit <= 0)
                return;

            double volume = VolumeForRisk(riskPerUnit);
            if (volume <= 0)
                return;

            double tpPrice = entry - riskPerUnit * TpRiskMultiplier;
            var result = ExecuteMarketOrder(TradeType.Sell, SymbolName, volume, BotTradeId, slPrice, tpPrice);
            if (result.IsSuccessful)
                Print("SHORT entry={0:F2} SL={1:F2} TP={2:F2} vol={3}", entry, slPrice, tpPrice, volume);
            else
                Print("SHORT failed: {0}", result.Error);
        }

        private double VolumeForRisk(double riskPerUnit)
        {
            // riskPerUnit in price; volume in units (BTC lots on crypto often fractional)
            double raw = TradeRiskUsd / riskPerUnit;
            double step = Symbol.VolumeInUnitsStep;
            double min = Symbol.VolumeInUnitsMin;
            double max = Symbol.VolumeInUnitsMax;

            double vol = Math.Floor(raw / step) * step;
            if (vol < min)
                return 0;
            if (vol > max)
                vol = max;
            return Symbol.NormalizeVolumeInUnits(vol, RoundingMode.Down);
        }

        private int CountOurPositions()
        {
            int n = 0;
            foreach (var p in Positions)
            {
                if (p.SymbolName == SymbolName && p.Label == BotTradeId)
                    n++;
            }
            return n;
        }

        // =========================================================================================
        // Week DD brake
        // =========================================================================================

        private void ResetWeekIfNeeded(bool force)
        {
            var now = Server.Time;
            // Monday 00:00 UTC week start
            int diff = ((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var monday = now.Date.AddDays(-diff);

            if (force || monday != _weekStartUtc)
            {
                _weekStartUtc = monday;
                _weekStartEquity = Account.Equity;
                if (EnableWeekDdBrake)
                    Print("Week DD brake: week start {0:u} equity={1:F2}", _weekStartUtc, _weekStartEquity);
            }
        }

        private bool IsWeekDdBrakeActive()
        {
            if (!EnableWeekDdBrake || _weekStartEquity <= 0)
                return false;
            double ddPct = (_weekStartEquity - Account.Equity) / _weekStartEquity * 100.0;
            return ddPct >= WeekDdBrakePct;
        }
    }
}
