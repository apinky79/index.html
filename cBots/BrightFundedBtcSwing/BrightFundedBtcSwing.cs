using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    public enum SwingTradeDirection
    {
        LongAndShort,
        LongOnly,
        ShortOnly
    }

    /// <summary>
    /// BrightFunded BTC Swing — designed after EMA (too slow) and Pace breakout (failed forward).
    ///
    /// Logic (H1 recommended):
    /// 1) Trade only with the higher-timeframe trend (slow EMA).
    /// 2) Enter on RSI reclaim from pullback zone (not breakout chase).
    /// 3) Optional ADX gate so we skip dead/choppy ranges.
    /// 4) Wider ATR stops, modest R target, strict daily risk wrapper.
    ///
    /// Goal: MC DD under ~8% AND multi-week forward stability on BTCUSD.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BrightFundedBtcSwing : Robot
    {
        // ===========================
        // Trade Options
        // ===========================
        [Parameter("Bot Trade ID", Group = "Trade Options", DefaultValue = "BF_SWING")]
        public string BotTradeId { get; set; }

        [Parameter("Order Direction", Group = "Trade Options", DefaultValue = SwingTradeDirection.LongAndShort)]
        public SwingTradeDirection OrderDirection { get; set; }

        [Parameter("Enable Disconnect Pause", Group = "Trade Options", DefaultValue = true)]
        public bool EnableDisconnectPause { get; set; }

        // ===========================
        // Trend Filter
        // ===========================
        [Parameter("Trend EMA Length", Group = "Trend Filter", DefaultValue = 100, MinValue = 20, MaxValue = 300)]
        public int TrendEmaLength { get; set; }

        [Parameter("Use ADX Filter", Group = "Trend Filter", DefaultValue = true)]
        public bool UseAdxFilter { get; set; }

        [Parameter("ADX Period", Group = "Trend Filter", DefaultValue = 14, MinValue = 5)]
        public int AdxPeriod { get; set; }

        [Parameter("ADX Min Level", Group = "Trend Filter", DefaultValue = 18, MinValue = 5, MaxValue = 50)]
        public int AdxMinLevel { get; set; }

        // ===========================
        // Entry - RSI Reclaim
        // ===========================
        [Parameter("RSI Period", Group = "Entry - RSI Reclaim", DefaultValue = 14, MinValue = 2)]
        public int RsiPeriod { get; set; }

        [Parameter("Long RSI Floor", Group = "Entry - RSI Reclaim", DefaultValue = 35, MinValue = 5, MaxValue = 50)]
        public int LongRsiFloor { get; set; }

        [Parameter("Long RSI Reclaim", Group = "Entry - RSI Reclaim", DefaultValue = 45, MinValue = 20, MaxValue = 60)]
        public int LongRsiReclaim { get; set; }

        [Parameter("Short RSI Ceiling", Group = "Entry - RSI Reclaim", DefaultValue = 65, MinValue = 50, MaxValue = 95)]
        public int ShortRsiCeiling { get; set; }

        [Parameter("Short RSI Reclaim", Group = "Entry - RSI Reclaim", DefaultValue = 55, MinValue = 40, MaxValue = 80)]
        public int ShortRsiReclaim { get; set; }

        // ===========================
        // Exits
        // ===========================
        [Parameter("SL ATR Multiple", Group = "Exits", DefaultValue = 2.0, MinValue = 0.8)]
        public double SlAtrMultiple { get; set; }

        [Parameter("ATR Period", Group = "Exits", DefaultValue = 14, MinValue = 1)]
        public int AtrPeriod { get; set; }

        [Parameter("TP Risk Multiple", Group = "Exits", DefaultValue = 2.0, MinValue = 0.8)]
        public double TpRiskMultiple { get; set; }

        [Parameter("Break Even at R", Group = "Exits", DefaultValue = 1.0, MinValue = 0.0)]
        public double BreakEvenAtR { get; set; }

        [Parameter("Trail ATR Multiple (0=off)", Group = "Exits", DefaultValue = 0.0, MinValue = 0.0)]
        public double TrailAtrMultiple { get; set; }

        // ===========================
        // Challenge Risk
        // ===========================
        [Parameter("Trade Risk (USD)", Group = "Challenge Risk", DefaultValue = 200.0, MinValue = 0.0)]
        public double TradeRiskUsd { get; set; }

        [Parameter("Daily Loss Limit (USD)", Group = "Challenge Risk", DefaultValue = 1500.0, MinValue = 0.0)]
        public double DailyLossLimitUsd { get; set; }

        [Parameter("Daily Profit Lock (USD)", Group = "Challenge Risk", DefaultValue = 600.0, MinValue = 0.0)]
        public double DailyProfitLockUsd { get; set; }

        [Parameter("Max Trades Per Day", Group = "Challenge Risk", DefaultValue = 2, MinValue = 1, MaxValue = 10)]
        public int MaxTradesPerDay { get; set; }

        [Parameter("Halt On Daily Limits", Group = "Challenge Risk", DefaultValue = true)]
        public bool HaltOnDailyLimits { get; set; }

        // ===========================
        // Fitness
        // ===========================
        [Parameter("Use Min Trades", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMinTrades { get; set; }

        [Parameter("Min Trades", Group = "Fitness Filters", DefaultValue = 25, MinValue = 1)]
        public int MinTrades { get; set; }

        [Parameter("Use Min Profit Factor", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMinProfitFactor { get; set; }

        [Parameter("Min Profit Factor", Group = "Fitness Filters", DefaultValue = 1.20, MinValue = 0.0)]
        public double MinProfitFactor { get; set; }

        [Parameter("Use Min Net Profit", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMinNetProfit { get; set; }

        [Parameter("Min Net Profit (USD)", Group = "Fitness Filters", DefaultValue = 2000.0)]
        public double MinNetProfitUsd { get; set; }

        [Parameter("Use Max Balance DD %", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMaxBalanceDd { get; set; }

        [Parameter("Max Balance Drawdown %", Group = "Fitness Filters", DefaultValue = 7.0, MinValue = 0.0, MaxValue = 100.0)]
        public double MaxBalanceDrawdownPercent { get; set; }

        [Parameter("Use Max Consecutive Losers", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMaxConsecLosers { get; set; }

        [Parameter("Max Consecutive Losing Trades", Group = "Fitness Filters", DefaultValue = 6, MinValue = 1)]
        public int MaxConsecutiveLosingTrades { get; set; }

        private ExponentialMovingAverage _trendEma;
        private RelativeStrengthIndex _rsi;
        private AverageTrueRange _atr;
        private DirectionalMovementSystem _dms;

        private int _lastBarIndex = -1;
        private bool _isConnected = true;
        private const int ReconnectDelaySeconds = 10;

        private DateTime _dayKey = DateTime.MinValue;
        private double _dayStartEquity;
        private int _dayTradeCount;
        private bool _dailyHaltActive;

        // RSI pullback memory (must dip into zone before reclaim)
        private bool _longPullbackArmed;
        private bool _shortPullbackArmed;

        protected override void OnStart()
        {
            if (LongRsiFloor >= LongRsiReclaim || ShortRsiReclaim >= ShortRsiCeiling)
            {
                Print("Invalid RSI levels. Bot stopped.");
                Stop();
                return;
            }

            BindIndicators();
            ResetDayIfNeeded();

            if (EnableDisconnectPause)
            {
                Server.Connected += OnConnected;
                Server.Disconnected += OnDisconnected;
            }

            Print("BrightFundedBtcSwing started. TF={0} TrendEMA={1} Risk={2}",
                TimeFrame, TrendEmaLength, TradeRiskUsd);
        }

        private void BindIndicators()
        {
            _trendEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, TrendEmaLength);
            _rsi = Indicators.RelativeStrengthIndex(Bars.ClosePrices, RsiPeriod);
            _atr = Indicators.AverageTrueRange(Bars, AtrPeriod, MovingAverageType.Simple);
            _dms = Indicators.DirectionalMovementSystem(Bars, AdxPeriod);
            _lastBarIndex = Bars.Count - 1;
        }

        private void OnDisconnected()
        {
            Timer.Stop();
            _isConnected = false;
        }

        private void OnConnected()
        {
            Timer.Start(ReconnectDelaySeconds);
        }

        protected override void OnTimer()
        {
            Timer.Stop();
            BindIndicators();
            _isConnected = true;
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
            UpdatePullbackArms();

            if (_dailyHaltActive)
                return;

            if (Positions.Find(BotTradeId, Symbol.Name) != null)
                return;

            if (_dayTradeCount >= MaxTradesPerDay)
                return;

            if (CanLong() && IsLongReclaim() && PassesLongTrend())
            {
                if (ExecuteTrade(TradeType.Buy))
                {
                    _dayTradeCount++;
                    _longPullbackArmed = false;
                }
            }
            else if (CanShort() && IsShortReclaim() && PassesShortTrend())
            {
                if (ExecuteTrade(TradeType.Sell))
                {
                    _dayTradeCount++;
                    _shortPullbackArmed = false;
                }
            }
        }

        private bool CanLong()
        {
            return OrderDirection == SwingTradeDirection.LongAndShort
                || OrderDirection == SwingTradeDirection.LongOnly;
        }

        private bool CanShort()
        {
            return OrderDirection == SwingTradeDirection.LongAndShort
                || OrderDirection == SwingTradeDirection.ShortOnly;
        }

        private void UpdatePullbackArms()
        {
            int i = Bars.Count - 2;
            if (i < 0)
                return;

            double rsi = _rsi.Result[i];

            if (rsi <= LongRsiFloor)
                _longPullbackArmed = true;
            if (rsi >= ShortRsiCeiling)
                _shortPullbackArmed = true;

            // Disarm if RSI goes deep into opposite extreme without entry
            if (rsi >= ShortRsiCeiling)
                _longPullbackArmed = false;
            if (rsi <= LongRsiFloor)
                _shortPullbackArmed = false;
        }

        private bool IsLongReclaim()
        {
            int i = Bars.Count - 2;
            if (i < 1 || !_longPullbackArmed)
                return false;

            double prev = _rsi.Result[i - 1];
            double cur = _rsi.Result[i];
            return prev < LongRsiReclaim && cur >= LongRsiReclaim;
        }

        private bool IsShortReclaim()
        {
            int i = Bars.Count - 2;
            if (i < 1 || !_shortPullbackArmed)
                return false;

            double prev = _rsi.Result[i - 1];
            double cur = _rsi.Result[i];
            return prev > ShortRsiReclaim && cur <= ShortRsiReclaim;
        }

        private bool PassesLongTrend()
        {
            int i = Bars.Count - 2;
            if (i < 0)
                return false;

            if (Bars.ClosePrices[i] <= _trendEma.Result[i])
                return false;

            if (UseAdxFilter && _dms.ADX.Last(1) < AdxMinLevel)
                return false;

            return true;
        }

        private bool PassesShortTrend()
        {
            int i = Bars.Count - 2;
            if (i < 0)
                return false;

            if (Bars.ClosePrices[i] >= _trendEma.Result[i])
                return false;

            if (UseAdxFilter && _dms.ADX.Last(1) < AdxMinLevel)
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
                _dailyHaltActive = true;
                return;
            }

            if (DailyProfitLockUsd > 0 && dayPnl >= DailyProfitLockUsd)
                _dailyHaltActive = true;
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
                Print("Swing fill {0} SL={1:F1} TP={2:F1}", tradeType, stopLossPips, takeProfitPips);
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
                    else current = 0;
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
                if (CalcMaxDdPctRounded(startingBalance, running) > MaxBalanceDrawdownPercent)
                    return 0;
            }

            return netProfit;
        }
    }
}
