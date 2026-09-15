using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.Robots.LiquiditySweep.Models;

namespace cAlgo.Robots.LiquiditySweep.Engine
{
    /// <summary>
    /// Detects ICT-style liquidity sweeps:
    /// 1) Wick trades through the pool (run)
    /// 2) Candle body closes back inside (failure to accept beyond level)
    /// 3) Optional market structure shift in reversal direction
    /// 4) Configurable confirmation bar delay before entry
    /// </summary>
    public sealed class SweepConfirmationEngine
    {
        private readonly int _confirmationBarDelay;
        private readonly int _maxSweepAgeBars;
        private readonly bool _requireStructureShift;
        private readonly int _structurePivotBars;
        private readonly double _minWickToBodyRatio;
        private readonly int _minZoneStrength;

        public SweepConfirmationEngine(
            int confirmationBarDelay,
            int maxSweepAgeBars,
            bool requireStructureShift,
            int structurePivotBars,
            double minWickToBodyRatio,
            int minZoneStrength)
        {
            _confirmationBarDelay = Math.Max(0, confirmationBarDelay);
            _maxSweepAgeBars = Math.Max(3, maxSweepAgeBars);
            _requireStructureShift = requireStructureShift;
            _structurePivotBars = Math.Max(2, structurePivotBars);
            _minWickToBodyRatio = minWickToBodyRatio;
            _minZoneStrength = minZoneStrength;
        }

        public List<SweepSetup> UpdateSetups(
            IReadOnlyList<LiquidityZone> zones,
            double[] opens,
            double[] highs,
            double[] lows,
            double[] closes,
            int lastClosedBarIndex,
            List<SweepSetup> existingSetups)
        {
            var setups = existingSetups ?? new List<SweepSetup>();
            PurgeExpired(setups, lastClosedBarIndex);

            if (lastClosedBarIndex < 1)
                return setups;

            int bar = lastClosedBarIndex;
            double open = opens[bar];
            double high = highs[bar];
            double low = lows[bar];
            double close = closes[bar];
            double body = Math.Abs(close - open);

            foreach (var zone in zones.Where(z => z.IsActive && z.StrengthScore >= _minZoneStrength))
            {
                if (setups.Any(s => s.Zone.LevelPrice == zone.LevelPrice && s.Zone.Source == zone.Source && s.Phase != SweepPhase.Invalidated))
                    continue;

                if (zone.Side == LiquiditySide.SellSide)
                {
                    TryDetectBullishSweep(zone, open, high, low, close, body, bar, setups, highs, lows, closes);
                }
                else
                {
                    TryDetectBearishSweep(zone, open, high, low, close, body, bar, setups, highs, lows, closes);
                }
            }

            AdvanceConfirmation(setups, opens, highs, lows, closes, lastClosedBarIndex);
            return setups;
        }

        public IReadOnlyList<SweepSetup> GetReadyEntries(IEnumerable<SweepSetup> setups)
        {
            return setups
                .Where(s => s.Phase == SweepPhase.Confirmed && s.ConfirmationBarsRemaining <= 0)
                .Where(s => !_requireStructureShift || s.StructureShiftConfirmed)
                .ToList();
        }

        private void TryDetectBullishSweep(
            LiquidityZone zone,
            double open,
            double high,
            double low,
            double close,
            double body,
            int bar,
            List<SweepSetup> setups,
            double[] highs,
            double[] lows,
            double[] closes)
        {
            if (low >= zone.LevelPrice)
                return;

            bool closedBackInside = close > zone.LevelPrice;
            if (!closedBackInside)
                return;

            double lowerWick = Math.Min(open, close) - low;
            if (body > 0 && lowerWick / body < _minWickToBodyRatio)
                return;

            setups.Add(new SweepSetup
            {
                Zone = zone,
                Direction = TradeType.Buy,
                Phase = SweepPhase.Swept,
                SweepBarIndex = bar,
                SweepWickExtreme = low,
                ConfirmationBarsRemaining = _confirmationBarDelay,
                StructureShiftConfirmed = !_requireStructureShift || HasBullishStructureShift(highs, lows, closes, bar),
                DetectedAt = DateTime.UtcNow
            });
        }

        private void TryDetectBearishSweep(
            LiquidityZone zone,
            double open,
            double high,
            double low,
            double close,
            double body,
            int bar,
            List<SweepSetup> setups,
            double[] highs,
            double[] lows,
            double[] closes)
        {
            if (high <= zone.LevelPrice)
                return;

            bool closedBackInside = close < zone.LevelPrice;
            if (!closedBackInside)
                return;

            double upperWick = high - Math.Max(open, close);
            if (body > 0 && upperWick / body < _minWickToBodyRatio)
                return;

            setups.Add(new SweepSetup
            {
                Zone = zone,
                Direction = TradeType.Sell,
                Phase = SweepPhase.Swept,
                SweepBarIndex = bar,
                SweepWickExtreme = high,
                ConfirmationBarsRemaining = _confirmationBarDelay,
                StructureShiftConfirmed = !_requireStructureShift || HasBearishStructureShift(highs, lows, closes, bar),
                DetectedAt = DateTime.UtcNow
            });
        }

        private void AdvanceConfirmation(
            List<SweepSetup> setups,
            double[] opens,
            double[] highs,
            double[] lows,
            double[] closes,
            int bar)
        {
            foreach (var setup in setups.Where(s => s.Phase == SweepPhase.Swept || s.Phase == SweepPhase.Confirmed))
            {
                if (bar <= setup.SweepBarIndex)
                    continue;

                if (setup.Direction == TradeType.Buy)
                {
                    if (closes[bar] < setup.Zone.LevelPrice)
                    {
                        setup.Phase = SweepPhase.Invalidated;
                        continue;
                    }
                }
                else if (closes[bar] > setup.Zone.LevelPrice)
                {
                    setup.Phase = SweepPhase.Invalidated;
                    continue;
                }

                if (_requireStructureShift && !setup.StructureShiftConfirmed)
                {
                    setup.StructureShiftConfirmed = setup.Direction == TradeType.Buy
                        ? HasBullishStructureShift(highs, lows, closes, bar)
                        : HasBearishStructureShift(highs, lows, closes, bar);
                }

                if (setup.Phase == SweepPhase.Swept)
                {
                    setup.Phase = SweepPhase.Confirmed;
                }

                if (setup.ConfirmationBarsRemaining > 0)
                    setup.ConfirmationBarsRemaining--;
            }
        }

        private bool HasBullishStructureShift(double[] highs, double[] lows, double[] closes, int bar)
        {
            var swings = SwingPointDetector.DetectConfirmedSwings(highs, lows, null, _structurePivotBars, bar);
            var lastHigh = swings.LastOrDefault(s => s.IsHigh && s.BarIndex < bar);
            if (lastHigh == null)
                return false;
            return closes[bar] > lastHigh.Price;
        }

        private bool HasBearishStructureShift(double[] highs, double[] lows, double[] closes, int bar)
        {
            var swings = SwingPointDetector.DetectConfirmedSwings(highs, lows, null, _structurePivotBars, bar);
            var lastLow = swings.LastOrDefault(s => !s.IsHigh && s.BarIndex < bar);
            if (lastLow == null)
                return false;
            return closes[bar] < lastLow.Price;
        }

        private void PurgeExpired(List<SweepSetup> setups, int bar)
        {
            setups.RemoveAll(s =>
                s.Phase == SweepPhase.Invalidated ||
                (s.Phase != SweepPhase.None && bar - s.SweepBarIndex > _maxSweepAgeBars));
        }
    }
}
