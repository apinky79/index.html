using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    public enum PaceTradeDirection
    {
        LongAndShort,
        LongOnly,
        ShortOnly
    }

    /// <summary>
    /// BrightFunded Challenge Pace cBot — higher trade frequency for Phase 1/2 speed.
    ///
    /// Idea: N-bar breakout (momentum) on m15, with hard daily loss / daily profit locks
    /// so you can push for throughput without ignoring challenge DD rules.
    ///
    /// NOT the slow EMA grinder — this aims for more trades and faster equity movement.
    /// Always validate with Strategy Selector (MC DD / MC PF) before live challenge use.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BrightFundedChallengePace : Robot
    {
        // ===========================
        // Trade Options
        // ===========================
        [Parameter("Bot Trade ID", Group = "Trade Options", DefaultValue = "BF_PACE")]
        public string BotTradeId { get; set; }

        [Parameter("Order Direction", Group = "Trade Options", DefaultValue = PaceTradeDirection.LongAndShort)]
        public PaceTradeDirection OrderDirection { get; set; }

        [Parameter("Enable Disconnect Pause", Group = "Trade Options", DefaultValue = true)]
        public bool EnableDisconnectPause { get; set; }

        // ===========================
        // Entry - Breakout
        // ===========================
        [Parameter("Breakout Lookback (bars)", Group = "Entry - Breakout", DefaultValue = 20, MinValue = 5, MaxValue = 100)]
        public int BreakoutLookback { get; set; }

        [Parameter("Require Close Beyond Level", Group = "Entry - Breakout", DefaultValue = true)]
        public bool RequireCloseBeyond { get; set; }

        // ===========================
        // Filters
        // ===========================
        [Parameter("Use EMA Trend Filter", Group = "Filters", DefaultValue = true)]
        public bool UseEmaTrendFilter { get; set; }

        [Parameter("Trend EMA Length", Group = "Filters", DefaultValue = 50, MinValue = 5, MaxValue = 300)]
        public int TrendEmaLength { get; set; }

        [Parameter("Use Min ATR Filter", Group = "Filters", DefaultValue = false)]
        public bool UseMinAtrFilter { get; set; }

        [Parameter("Min ATR (price)", Group = "Filters", DefaultValue = 0.0, MinValue = 0.0)]
        public double MinAtrPrice { get; set; }

        // ===========================
        // Stoploss / Take Profit
        // ===========================
        [Parameter("SL ATR Multiple", Group = "Exits", DefaultValue = 1.5, MinValue = 0.5)]
        public double SlAtrMultiple { get; set; }

        [Parameter("ATR Period", Group = "Exits", DefaultValue = 14, MinValue = 1)]
        public int AtrPeriod { get; set; }

        [Parameter("TP Risk Multiple", Group = "Exits", DefaultValue = 1.5, MinValue = 0.5)]
        public double TpRiskMultiple { get; set; }

        [Parameter("Move SL to BE at R", Group = "Exits", DefaultValue = 1.0, MinValue = 0.0)]
        public double BreakEvenAtR { get; set; }

        [Parameter("Trail ATR Multiple (0=off)", Group = "Exits", DefaultValue = 0.0, MinValue = 0.0)]
        public double TrailAtrMultiple { get; set; }

        // ===========================
        // Challenge Risk Wrapper
        // ===========================
        [Parameter("Trade Risk (USD)", Group = "Challenge Risk", DefaultValue = 300.0, MinValue = 0.0)]
        public double TradeRiskUsd { get; set; }

        [Parameter("Daily Loss Limit (USD)", Group = "Challenge Risk", DefaultValue = 1500.0, MinValue = 0.0)]
        public double DailyLossLimitUsd { get; set; }

        [Parameter("Daily Profit Lock (USD)", Group = "Challenge Risk", DefaultValue = 800.0, MinValue = 0.0)]
        public double DailyProfitLockUsd { get; set; }

        [Parameter("Max Trades Per Day", Group = "Challenge Risk", DefaultValue = 4, MinValue = 1, MaxValue = 20)]
        public int MaxTradesPerDay { get; set; }

        [Parameter("Halt On Daily Limits", Group = "Challenge Risk", DefaultValue = true)]
        public bool HaltOnDailyLimits { get; set; }

        // ===========================
        // Fitness Filters (optimizer)
        // ===========================
        [Parameter("Use Min Trades", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMinTrades { get; set; }

        [Parameter("Min Trades", Group = "Fitness Filters", DefaultValue = 40, MinValue = 1)]
        public int MinTrades { get; set; }

        [Parameter("Use Min Profit Factor", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMinProfitFactor { get; set; }

        [Parameter("Min Profit Factor", Group = "Fitness Filters", DefaultValue = 1.15, MinValue = 0.0)]
        public double MinProfitFactor { get; set; }

        [Parameter("Use Min Net Profit", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMinNetProfit { get; set; }

        [Parameter("Min Net Profit (USD)", Group = "Fitness Filters", DefaultValue = 2500.0)]
        public double MinNetProfitUsd { get; set; }

        [Parameter("Use Max Balance DD %", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMaxBalanceDd { get; set; }

        [Parameter("Max Balance Drawdown %", Group = "Fitness Filters", DefaultValue = 8.0, MinValue = 0.0, MaxValue = 100.0)]
        public double MaxBalanceDrawdownPercent { get; set; }

        [Parameter("Use Max Consecutive Losers", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMaxConsecLosers { get; set; }

        [Parameter("Max Consecutive Losing Trades", Group = "Fitness Filters", DefaultValue = 7, MinValue = 1)]
        public int MaxConsecutiveLosingTrades { get; set; }

        private ExponentialMovingAverage _trendEma;
        private AverageTrueRange _atr;

        private int _lastBarIndex = -1;
        private bool _isConnected = true;
        private const int ReconnectDelaySeconds = 10;

        private DateTime _dayKey = DateTime.MinValue;
        private double _dayStartEquity;
        private int _dayTradeCount;
        private bool _dailyHaltActive;

        protected override void OnStart()
        {
            BindIndicators();
            ResetDayIfNeeded();

            if (EnableDisconnectPause)
            {
                Server.Connected += OnConnected;
                Server.Disconnected += OnDisconnected;
            }

            Print("BrightFundedChallengePace started. TF={0} Lookback={1} Risk={2} DailyLoss={3} DailyLock={4}",
                TimeFrame, BreakoutLookback, TradeRiskUsd, DailyLossLimitUsd, DailyProfitLockUsd);
        }

        private void BindIndicators()
        {
            _trendEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, TrendEmaLength);
            _atr = Indicators.AverageTrueRange(Bars, AtrPeriod, MovingAverageType.Simple);
            _lastBarIndex = Bars.Count - 1;
        }

        private void OnDisconnected()
        {
            Timer.Stop();
            _isConnected = false;
            Print("Disconnected — trading paused");
        }

        private void OnConnected()
        {
            Print("Reconnected — waiting {0}s", ReconnectDelaySeconds);
            Timer.Start(ReconnectDelaySeconds);
        }

        protected override void OnTimer()
        {
            Timer.Stop();
            BindIndicators();
            _isConnected = true;
            Print("Connection settled — trading resumed");
        }

        protected override void OnTick()
        {
            if (!_isConnected)
                return;

            ResetDayIfNeeded();
            UpdateDailyHalt();
            ApplyBreakEven();
            ApplyTrail();

            if (Bars.Count - 1 <= _lastBarIndex)
                return;

            _lastBarIndex = Bars.Count - 1;
            OnBarClose();
        }

        private void OnBarClose()
        {
            if (_dailyHaltActive)
                return;

            if (Positions.Find(BotTradeId, Symbol.Name) != null)
                return;

            if (_dayTradeCount >= MaxTradesPerDay)
                return;

            int signal = GetBreakoutSignal();
            if (signal == 0)
                return;

            if (signal > 0 && CanLong() && PassesLongFilters())
            {
                if (ExecuteTrade(TradeType.Buy))
                    _dayTradeCount++;
            }
            else if (signal < 0 && CanShort() && PassesShortFilters())
            {
                if (ExecuteTrade(TradeType.Sell))
                    _dayTradeCount++;
            }
        }

        private bool CanLong()
        {
            return OrderDirection == PaceTradeDirection.LongAndShort
                || OrderDirection == PaceTradeDirection.LongOnly;
        }

        private bool CanShort()
        {
            return OrderDirection == PaceTradeDirection.LongAndShort
                || OrderDirection == PaceTradeDirection.ShortOnly;
        }

        /// <summary>
        /// +1 long breakout, -1 short breakout, 0 none.
        /// Uses prior Lookback bars only (no look-ahead on the signal bar).
        /// </summary>
        private int GetBreakoutSignal()
        {
            int signalBar = Bars.Count - 2;
            int start = signalBar - BreakoutLookback;
            if (start < 0 || signalBar < 1)
                return 0;

            double highest = double.MinValue;
            double lowest = double.MaxValue;
            for (int i = start; i < signalBar; i++)
            {
                if (Bars.HighPrices[i] > highest)
                    highest = Bars.HighPrices[i];
                if (Bars.LowPrices[i] < lowest)
                    lowest = Bars.LowPrices[i];
            }

            double close = Bars.ClosePrices[signalBar];
            double high = Bars.HighPrices[signalBar];
            double low = Bars.LowPrices[signalBar];

            bool longBreak = RequireCloseBeyond ? close > highest : high > highest;
            bool shortBreak = RequireCloseBeyond ? close < lowest : low < lowest;

            if (longBreak && !shortBreak)
                return 1;
            if (shortBreak && !longBreak)
                return -1;
            return 0;
        }

        private bool PassesLongFilters()
        {
            int i = Bars.Count - 2;
            if (i < 0)
                return false;

            if (UseEmaTrendFilter && Bars.ClosePrices[i] <= _trendEma.Result[i])
                return false;

            if (UseMinAtrFilter && _atr.Result[i] < MinAtrPrice)
                return false;

            return true;
        }

        private bool PassesShortFilters()
        {
            int i = Bars.Count - 2;
            if (i < 0)
                return false;

            if (UseEmaTrendFilter && Bars.ClosePrices[i] >= _trendEma.Result[i])
                return false;

            if (UseMinAtrFilter && _atr.Result[i] < MinAtrPrice)
                return false;

            return true;
        }

        private void ResetDayIfNeeded()
        {
            var key = Server.Time.Date;
            if (key == _dayKey)
                return;

            _dayKey = key;
            _dayStartEquity = Account.Equity;
            _dayTradeCount = 0;
            _dailyHaltActive = false;
            Print("New day {0:yyyy-MM-dd} equity={1:F2}", key, _dayStartEquity);
        }

        private void UpdateDailyHalt()
        {
            if (!HaltOnDailyLimits)
            {
                _dailyHaltActive = false;
                return;
            }

            double dayPnl = Account.Equity - _dayStartEquity;

            if (DailyLossLimitUsd > 0 && dayPnl <= -DailyLossLimitUsd)
            {
                if (!_dailyHaltActive)
                {
                    _dailyHaltActive = true;
                    Print("Daily LOSS lock hit ({0:F2}). No new entries today.", dayPnl);
                }
                return;
            }

            if (DailyProfitLockUsd > 0 && dayPnl >= DailyProfitLockUsd)
            {
                if (!_dailyHaltActive)
                {
                    _dailyHaltActive = true;
                    Print("Daily PROFIT lock hit ({0:F2}). Banking the day.", dayPnl);
                }
            }
        }

        private void ApplyBreakEven()
        {
            var position = Positions.Find(BotTradeId, Symbol.Name);
            if (position == null || BreakEvenAtR <= 0 || !position.StopLoss.HasValue)
                return;

            if (Math.Abs(position.StopLoss.Value - position.EntryPrice) < Symbol.PipSize * 0.1)
                return;

            double riskPips = Math.Abs((position.EntryPrice - position.StopLoss.Value) / Symbol.PipSize);
            if (position.Pips >= riskPips * BreakEvenAtR)
                position.ModifyStopLossPrice(position.EntryPrice);
        }

        private void ApplyTrail()
        {
            if (TrailAtrMultiple <= 0)
                return;

            var position = Positions.Find(BotTradeId, Symbol.Name);
            if (position == null)
                return;

            double trailPips = (_atr.Result.Last(1) * TrailAtrMultiple) / Symbol.PipSize;
            if (trailPips <= 0)
                return;

            if (position.TradeType == TradeType.Buy)
            {
                double newStop = Symbol.Bid - trailPips * Symbol.PipSize;
                if (!position.StopLoss.HasValue || newStop > position.StopLoss.Value)
                    position.ModifyStopLossPrice(newStop);
            }
            else
            {
                double newStop = Symbol.Ask + trailPips * Symbol.PipSize;
                if (!position.StopLoss.HasValue || newStop < position.StopLoss.Value)
                    position.ModifyStopLossPrice(newStop);
            }
        }

        private bool ExecuteTrade(TradeType tradeType)
        {
            if (Positions.Find(BotTradeId, Symbol.Name) != null)
                return false;

            double atr = _atr.Result.Last(1);
            if (atr <= 0)
                return false;

            double stopLossPips = (atr * SlAtrMultiple) / Symbol.PipSize;
            if (stopLossPips <= 0)
                return false;

            double volumeInUnits;
            if (TradeRiskUsd > 0)
            {
                double volumeInLots = TradeRiskUsd / ((stopLossPips * Symbol.PipValue) * Symbol.LotSize);
                volumeInUnits = Symbol.QuantityToVolumeInUnits(volumeInLots);
            }
            else
            {
                volumeInUnits = Symbol.VolumeInUnitsMin;
            }

            if (volumeInUnits < Symbol.VolumeInUnitsMin)
                volumeInUnits = Symbol.VolumeInUnitsMin;
            volumeInUnits = Symbol.NormalizeVolumeInUnits(volumeInUnits);

            double takeProfitPips = stopLossPips * TpRiskMultiple;

            var result = ExecuteMarketOrder(
                tradeType,
                Symbol.Name,
                volumeInUnits,
                BotTradeId,
                stopLossPips,
                takeProfitPips);

            if (result.IsSuccessful)
            {
                Print("Pace fill {0} vol={1} SL={2:F1} TP={3:F1}",
                    tradeType, volumeInUnits, stopLossPips, takeProfitPips);
                return true;
            }

            Print("Order failed: {0}", result.Error);
            return false;
        }

        protected override void OnStop()
        {
            if (EnableDisconnectPause)
            {
                Server.Connected -= OnConnected;
                Server.Disconnected -= OnDisconnected;
            }

            Timer.Stop();
            Print("BrightFundedChallengePace stopped.");
        }

        private static double CalcMaxDdPctRounded(double startingBalance, List<double> runningPnL)
        {
            double peakBal = startingBalance;
            double troughBal = startingBalance;
            double maxDdAbs = 0.0;

            foreach (var cum in runningPnL)
            {
                double bal = startingBalance + cum;
                if (bal > peakBal)
                {
                    peakBal = bal;
                    troughBal = bal;
                }
                else if (bal < troughBal)
                {
                    troughBal = bal;
                    double dd = peakBal - troughBal;
                    if (dd > maxDdAbs)
                        maxDdAbs = dd;
                }
            }

            double ddPct = peakBal > 0.0 ? 100.0 * maxDdAbs / peakBal : 0.0;
            return Math.Round(ddPct, 2, MidpointRounding.AwayFromZero);
        }

        protected override double GetFitness(GetFitnessArgs args)
        {
            var trades = History
                .Where(h => h.SymbolName == Symbol.Name && h.Label == BotTradeId)
                .OrderBy(h => h.ClosingTime)
                .ToList();

            int total = trades.Count;
            double netProfit = trades.Sum(t => t.NetProfit);

            // Pace bot: reject losers and slow results early.
            if (netProfit <= 0)
                return 0;

            if (UseMinTrades && total < MinTrades)
                return 0;

            if (UseMinNetProfit && netProfit < MinNetProfitUsd)
                return 0;

            if (UseMinProfitFactor)
            {
                double grossProfit = 0.0;
                double grossLoss = 0.0;
                foreach (var t in trades)
                {
                    if (t.NetProfit >= 0)
                        grossProfit += t.NetProfit;
                    else
                        grossLoss += -t.NetProfit;
                }

                double pf = grossLoss > 0.0
                    ? grossProfit / grossLoss
                    : (grossProfit > 0.0 ? double.PositiveInfinity : 0.0);

                if (pf < MinProfitFactor)
                    return 0;
            }

            if (UseMaxConsecLosers)
            {
                int maxConsec = 0;
                int current = 0;
                foreach (var t in trades)
                {
                    if (t.NetProfit < 0)
                    {
                        current++;
                        if (current > maxConsec)
                            maxConsec = current;
                    }
                    else
                    {
                        current = 0;
                    }
                }

                if (maxConsec > MaxConsecutiveLosingTrades)
                    return 0;
            }

            if (UseMaxBalanceDd)
            {
                var running = new List<double>(total);
                double cum = 0.0;
                foreach (var t in trades)
                {
                    cum += t.NetProfit;
                    running.Add(cum);
                }

                double startingBalance = Account.Balance - netProfit;
                double ddPct = CalcMaxDdPctRounded(startingBalance, running);
                if (ddPct > MaxBalanceDrawdownPercent)
                    return 0;
            }

            // Prefer higher net profit for challenge pace (still filtered above).
            return netProfit;
        }
    }
}
