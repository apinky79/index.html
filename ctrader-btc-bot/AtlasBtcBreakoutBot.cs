// ---------------------------------------------------------------------------
// Atlas BTC Breakout Bot — fresh build from full BTC/USD history research
//
// Idea (plain English): When Bitcoin starts a REAL move (trend strength + new
// high/low), join the move. When the market is sleepy, stay out.
//
// Attach to: BTCUSD on the H4 chart (recommended). Works on any TF up to H4.
// ---------------------------------------------------------------------------

using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None, AddIndicators = true)]
    public class AtlasBtcBreakoutBot : Robot
    {
        private const string Label = "Atlas-BTC-Breakout";

        [Parameter("Donchian lookback (bars)", Group = "Breakout", DefaultValue = 55, MinValue = 10)]
        public int DonchianPeriod { get; set; }

        [Parameter("Min ADX to trade", Group = "Breakout", DefaultValue = 22, MinValue = 10)]
        public double MinAdx { get; set; }

        [Parameter("Require DI direction", Group = "Breakout", DefaultValue = true)]
        public bool RequireDiDirection { get; set; }

        [Parameter("ATR period", Group = "Exit", DefaultValue = 14, MinValue = 5)]
        public int AtrPeriod { get; set; }

        [Parameter("Stop loss = ATR ×", Group = "Exit", DefaultValue = 2.5, MinValue = 0.5)]
        public double StopAtrMultiple { get; set; }

        [Parameter("Take-profit R multiple", Group = "Exit", DefaultValue = 2.5, MinValue = 1.0)]
        public double RewardRiskMultiple { get; set; }

        [Parameter("Risk % of equity per trade", Group = "Risk", DefaultValue = 1.0, MinValue = 0.1)]
        public double RiskPercent { get; set; }

        [Parameter("Max open positions", Group = "Risk", DefaultValue = 1, MinValue = 1)]
        public int MaxPositions { get; set; }

        [Parameter("Allow long", Group = "Risk", DefaultValue = true)]
        public bool AllowLong { get; set; }

        [Parameter("Allow short", Group = "Risk", DefaultValue = true)]
        public bool AllowShort { get; set; }

        [Parameter("Log decisions", Group = "Debug", DefaultValue = true)]
        public bool LogDecisions { get; set; }

        private AverageTrueRange _atr;
        private DirectionalMovementSystem _dms;

        protected override void OnStart()
        {
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Exponential);
            _dms = Indicators.DirectionalMovementSystem(14);

            if (LogDecisions)
            {
                Print("Atlas BTC Breakout started on {0} ({1}). Donchian={2}, ADX≥{3}.",
                    SymbolName, TimeFrame, DonchianPeriod, MinAdx);
            }
        }

        protected override void OnBarClosed()
        {
            if (Bars.Count < DonchianPeriod + 5)
                return;

            if (CountPositions() >= MaxPositions)
                return;

            double adx = _dms.ADX.Last(1);
            if (adx < MinAdx)
                return;

            double close1 = Bars.ClosePrices.Last(1);
            double close2 = Bars.ClosePrices.Last(2);
            double priorHigh = HighestHigh(DonchianPeriod, 2);
            double priorLow = LowestLow(DonchianPeriod, 2);
            double atr = _atr.Result.Last(1);

            if (atr <= 0 || double.IsNaN(atr))
                return;

            bool diUp = _dms.DIPlus.Last(1) > _dms.DIMinus.Last(1);
            bool diDown = _dms.DIMinus.Last(1) > _dms.DIPlus.Last(1);

            bool breakUp = close2 <= priorHigh && close1 > priorHigh;
            bool breakDown = close2 >= priorLow && close1 < priorLow;

            if (AllowLong && breakUp && (!RequireDiDirection || diUp))
                Enter(TradeType.Buy, close1, atr, "breakout high");

            else if (AllowShort && breakDown && (!RequireDiDirection || diDown))
                Enter(TradeType.Sell, close1, atr, "breakout low");
        }

        private void Enter(TradeType side, double entry, double atr, string reason)
        {
            double stopDist = StopAtrMultiple * atr;
            double sl = side == TradeType.Buy ? entry - stopDist : entry + stopDist;
            double tp = side == TradeType.Buy
                ? entry + RewardRiskMultiple * stopDist
                : entry - RewardRiskMultiple * stopDist;

            long volume = VolumeForRisk(stopDist);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                if (LogDecisions)
                    Print("Skipped {0}: size too small for {1:F2}% risk.", reason, RiskPercent);
                return;
            }

            var result = ExecuteMarketOrder(side, SymbolName, volume, Label);
            if (!result.IsSuccessful)
            {
                Print("Order error: {0}", result.Error);
                return;
            }

            ModifyPosition(result.Position, sl, tp, ProtectionType.Absolute);

            if (LogDecisions)
            {
                Print("{0} {1} @ {2:F2} | SL {3:F2} TP {4:F2} | {5}",
                    side, SymbolName, entry, sl, tp, reason);
            }
        }

        private long VolumeForRisk(double stopDistancePrice)
        {
            double riskCash = Account.Equity * (RiskPercent / 100.0);
            if (Symbol.TickSize <= 0 || Symbol.TickValue <= 0 || stopDistancePrice <= 0)
                return 0;
            double ticks = stopDistancePrice / Symbol.TickSize;
            double rawVolume = riskCash / (ticks * Symbol.TickValue);
            return Symbol.NormalizeVolumeInUnits(rawVolume, RoundingMode.Down);
        }

        /// <summary>Max high of the previous `period` bars, ending at bar offset `startOffset` (1 = last closed bar).</summary>
        private double HighestHigh(int period, int startOffset)
        {
            double max = double.MinValue;
            for (int i = startOffset; i < startOffset + period; i++)
                max = Math.Max(max, Bars.HighPrices.Last(i));
            return max;
        }

        private double LowestLow(int period, int startOffset)
        {
            double min = double.MaxValue;
            for (int i = startOffset; i < startOffset + period; i++)
                min = Math.Min(min, Bars.LowPrices.Last(i));
            return min;
        }

        private int CountPositions()
        {
            return Positions.Count(p => p.Label == Label && p.SymbolName == SymbolName);
        }
    }
}
