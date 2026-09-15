using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.Robots.LiquiditySweep.Models;

namespace cAlgo.Robots.LiquiditySweep.Engine
{
    public sealed class ProfitManager
    {
        private readonly double _rewardRiskRatio;
        private readonly bool _moveToBreakevenAt1R;
        private readonly bool _partialCloseAt1R;
        private readonly double _partialClosePercent;
        private readonly bool _useTrailingStop;
        private readonly double _trailingStopPercent;
        private readonly double _activateTrailAfterR;

        private readonly Dictionary<long, ManagedTradeState> _states = new Dictionary<long, ManagedTradeState>();

        public ProfitManager(
            double rewardRiskRatio,
            bool moveToBreakevenAt1R,
            bool partialCloseAt1R,
            double partialClosePercent,
            bool useTrailingStop,
            double trailingStopPercent,
            double activateTrailAfterR)
        {
            _rewardRiskRatio = Math.Max(0.5, rewardRiskRatio);
            _moveToBreakevenAt1R = moveToBreakevenAt1R;
            _partialCloseAt1R = partialCloseAt1R;
            _partialClosePercent = Math.Min(90, Math.Max(10, partialClosePercent));
            _useTrailingStop = useTrailingStop;
            _trailingStopPercent = Math.Max(0.1, trailingStopPercent);
            _activateTrailAfterR = Math.Max(0.5, activateTrailAfterR);
        }

        public void RegisterPosition(Position position, double initialStopLoss)
        {
            double riskDistance = Math.Abs(position.EntryPrice - initialStopLoss);
            if (riskDistance <= 0)
                return;

            _states[position.Id] = new ManagedTradeState
            {
                PositionId = position.Id,
                EntryPrice = position.EntryPrice,
                InitialStopLoss = initialStopLoss,
                RiskDistance = riskDistance,
                HighestPrice = position.EntryPrice,
                LowestPrice = position.EntryPrice
            };
        }

        public void ManageOpenPositions(Robot robot, Symbol symbol)
        {
            foreach (var position in robot.Positions.Where(p => p.SymbolName == symbol.Name))
            {
                if (!_states.TryGetValue(position.Id, out var state))
                    continue;

                double price = position.TradeType == TradeType.Buy ? symbol.Bid : symbol.Ask;
                state.HighestPrice = Math.Max(state.HighestPrice, price);
                state.LowestPrice = Math.Min(state.LowestPrice, price);

                double profitR = GetProfitInR(position, state, price);
                double? newStop = null;

                if (_moveToBreakevenAt1R && !state.MovedToBreakeven && profitR >= 1.0)
                {
                    newStop = state.EntryPrice;
                    state.MovedToBreakeven = true;
                }

                if (_partialCloseAt1R && !state.PartialTaken && profitR >= 1.0)
                {
                    double closeVolume = symbol.NormalizeVolumeInUnits(
                        position.VolumeInUnits * (_partialClosePercent / 100.0),
                        RoundingMode.Down);

                    if (closeVolume >= symbol.VolumeInUnitsMin && closeVolume < position.VolumeInUnits)
                    {
                        robot.ClosePosition(position, closeVolume);
                        state.PartialTaken = true;
                    }
                }

                if (_useTrailingStop && profitR >= _activateTrailAfterR)
                {
                    double trailDistance = price * (_trailingStopPercent / 100.0);
                    double trailStop = position.TradeType == TradeType.Buy
                        ? state.HighestPrice - trailDistance
                        : state.LowestPrice + trailDistance;

                    trailStop = symbol.NormalizePrice(trailStop);
                    newStop = BetterStop(position, newStop, trailStop);
                }

                if (newStop.HasValue && IsImprovement(position, newStop.Value))
                {
                    robot.ModifyPosition(position, newStop, position.TakeProfit, ProtectionType.None);
                }
            }

            CleanupClosed(robot);
        }

        public double CalculateTakeProfit(TradeType direction, double entryPrice, double stopLoss)
        {
            double risk = Math.Abs(entryPrice - stopLoss);
            return direction == TradeType.Buy
                ? entryPrice + risk * _rewardRiskRatio
                : entryPrice - risk * _rewardRiskRatio;
        }

        private static double GetProfitInR(Position position, ManagedTradeState state, double price)
        {
            double move = position.TradeType == TradeType.Buy
                ? price - state.EntryPrice
                : state.EntryPrice - price;
            return move / state.RiskDistance;
        }

        private static double? BetterStop(Position position, double? current, double candidate)
        {
            if (!current.HasValue)
                return candidate;

            if (position.TradeType == TradeType.Buy)
                return Math.Max(current.Value, candidate);

            return Math.Min(current.Value, candidate);
        }

        private static bool IsImprovement(Position position, double newStop)
        {
            if (!position.StopLoss.HasValue)
                return true;

            return position.TradeType == TradeType.Buy
                ? newStop > position.StopLoss.Value
                : newStop < position.StopLoss.Value;
        }

        private void CleanupClosed(Robot robot)
        {
            var openIds = robot.Positions.Select(p => p.Id).ToHashSet();
            foreach (var id in _states.Keys.Where(k => !openIds.Contains(k)).ToList())
                _states.Remove(id);
        }
    }
}
