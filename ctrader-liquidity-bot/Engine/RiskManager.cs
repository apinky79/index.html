using System;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.Robots.LiquiditySweep.Models;

namespace cAlgo.Robots.LiquiditySweep.Engine
{
    public sealed class RiskManager
    {
        private readonly RiskMode _mode;
        private readonly double _fixedUsdRisk;
        private readonly double _riskPercent;
        private readonly double _maxSpreadPips;
        private readonly double _stopBufferAtrMultiplier;

        public RiskManager(
            RiskMode mode,
            double fixedUsdRisk,
            double riskPercent,
            double maxSpreadPips,
            double stopBufferAtrMultiplier)
        {
            _mode = mode;
            _fixedUsdRisk = Math.Max(1, fixedUsdRisk);
            _riskPercent = Math.Max(0.01, riskPercent);
            _maxSpreadPips = maxSpreadPips;
            _stopBufferAtrMultiplier = stopBufferAtrMultiplier;
        }

        public bool IsSpreadAcceptable(Symbol symbol)
        {
            if (_maxSpreadPips <= 0)
                return true;
            return symbol.Spread / symbol.PipSize <= _maxSpreadPips;
        }

        public double CalculateRiskAmount(Account account)
        {
            return _mode switch
            {
                RiskMode.FixedUsd => _fixedUsdRisk,
                RiskMode.PercentEquity => account.Equity * _riskPercent / 100.0,
                RiskMode.PercentBalance => account.Balance * _riskPercent / 100.0,
                _ => _fixedUsdRisk
            };
        }

        public double BuildStopLoss(TradeType direction, double sweepWickExtreme, double atr, Symbol symbol)
        {
            double buffer = atr * _stopBufferAtrMultiplier;
            double stop = direction == TradeType.Buy
                ? sweepWickExtreme - buffer
                : sweepWickExtreme + buffer;

            return symbol.NormalizePrice(stop);
        }

        public double CalculateVolumeInUnits(Symbol symbol, double entryPrice, double stopLoss, double riskAmount)
        {
            double stopDistance = Math.Abs(entryPrice - stopLoss);
            if (stopDistance <= 0)
                return symbol.VolumeInUnitsMin;

            double amountRiskedPerUnit = stopDistance * symbol.TickValue / symbol.TickSize;
            if (amountRiskedPerUnit <= 0)
                return symbol.VolumeInUnitsMin;

            double rawVolume = riskAmount / amountRiskedPerUnit;
            rawVolume = symbol.NormalizeVolumeInUnits(rawVolume, RoundingMode.Down);

            if (rawVolume < symbol.VolumeInUnitsMin)
                return 0;

            if (rawVolume > symbol.VolumeInUnitsMax)
                rawVolume = symbol.VolumeInUnitsMax;

            return rawVolume;
        }
    }
}
