using System;
using cAlgo.API;

namespace cAlgo.Robots.LiquiditySweep.Models
{
    public enum LiquiditySide
    {
        BuySide,
        SellSide
    }

    public enum LiquiditySource
    {
        SwingHigh,
        SwingLow,
        EqualHigh,
        EqualLow,
        PreviousDayHigh,
        PreviousDayLow,
        PreviousWeekHigh,
        PreviousWeekLow
    }

    public enum SweepPhase
    {
        None,
        Swept,
        Confirmed,
        Invalidated
    }

    public enum RiskMode
    {
        FixedUsd,
        PercentEquity,
        PercentBalance
    }

    public sealed class LiquidityZone
    {
        public double LevelPrice { get; set; }
        public double ZoneTop { get; set; }
        public double ZoneBottom { get; set; }
        public LiquiditySide Side { get; set; }
        public LiquiditySource Source { get; set; }
        public int TouchCount { get; set; }
        public int StrengthScore { get; set; }
        public DateTime FormedAt { get; set; }
        public int FormedBarIndex { get; set; }
        public bool IsActive { get; set; } = true;
        public string Label { get; set; }
    }

    public sealed class SweepSetup
    {
        public LiquidityZone Zone { get; set; }
        public TradeType Direction { get; set; }
        public SweepPhase Phase { get; set; }
        public int SweepBarIndex { get; set; }
        public double SweepWickExtreme { get; set; }
        public int ConfirmationBarsRemaining { get; set; }
        public bool StructureShiftConfirmed { get; set; }
        public DateTime DetectedAt { get; set; }
    }

    public sealed class ManagedTradeState
    {
        public long PositionId { get; set; }
        public double EntryPrice { get; set; }
        public double InitialStopLoss { get; set; }
        public double RiskDistance { get; set; }
        public bool MovedToBreakeven { get; set; }
        public bool PartialTaken { get; set; }
        public double HighestPrice { get; set; }
        public double LowestPrice { get; set; }
    }
}
