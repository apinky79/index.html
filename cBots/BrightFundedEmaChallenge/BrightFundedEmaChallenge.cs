using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    public enum TradeDirectionMode
    {
        LongAndShort,
        LongOnly,
        ShortOnly
    }

    public enum StopLossMode
    {
        Pips,
        ATR
    }

    public enum TakeProfitMode
    {
        None,
        Pips,
        RiskMultiplier
    }

    public enum BreakEvenMode
    {
        None,
        Pips,
        RiskMultiplier
    }

    /// <summary>
    /// BrightFunded-oriented EMA challenge bot.
    /// Flow: Double EMA trigger -> optional trend filters -> EMA retest entry.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BrightFundedEmaChallenge : Robot
    {
        // ===========================
        // Trade Options
        // ===========================
        [Parameter("Bot Trade ID", Group = "Trade Options", DefaultValue = "BF_EMA")]
        public string BotTradeId { get; set; }

        [Parameter("Order Direction", Group = "Trade Options", DefaultValue = TradeDirectionMode.LongAndShort)]
        public TradeDirectionMode OrderDirection { get; set; }

        [Parameter("Enable Disconnect Pause", Group = "Trade Options", DefaultValue = true)]
        public bool EnableDisconnectPause { get; set; }

        // ===========================
        // Entry - EMA Retest
        // ===========================
        [Parameter("Retest EMA Length", Group = "Entry - EMA Retest", DefaultValue = 12, MinValue = 2, MaxValue = 300)]
        public int RetestEmaLength { get; set; }

        [Parameter("Max Pending Bars (0=off)", Group = "Entry - EMA Retest", DefaultValue = 12, MinValue = 0, MaxValue = 200)]
        public int MaxPendingBars { get; set; }

        // ===========================
        // Trigger - Double EMA
        // ===========================
        [Parameter("Fast EMA Length", Group = "Trigger - Double EMA", DefaultValue = 8, MinValue = 2, MaxValue = 300)]
        public int FastEmaLength { get; set; }

        [Parameter("Slow EMA Length", Group = "Trigger - Double EMA", DefaultValue = 21, MinValue = 3, MaxValue = 400)]
        public int SlowEmaLength { get; set; }

        // ===========================
        // Filter - Price EMA Trend
        // ===========================
        [Parameter("Use Price EMA Filter", Group = "Filter - Price EMA", DefaultValue = true)]
        public bool UsePriceEmaFilter { get; set; }

        [Parameter("Trend EMA Length", Group = "Filter - Price EMA", DefaultValue = 55, MinValue = 2, MaxValue = 400)]
        public int TrendEmaLength { get; set; }

        // ===========================
        // Filter - ADX Trend (optional)
        // ===========================
        [Parameter("Use ADX Trend Filter", Group = "Filter - ADX Trend", DefaultValue = true)]
        public bool UseAdxTrendFilter { get; set; }

        [Parameter("ADX TimeFrame", Group = "Filter - ADX Trend", DefaultValue = "Hour")]
        public TimeFrame AdxTimeFrame { get; set; }

        [Parameter("ADX Period", Group = "Filter - ADX Trend", DefaultValue = 14, MinValue = 1)]
        public int AdxPeriod { get; set; }

        [Parameter("ADX DI Offset", Group = "Filter - ADX Trend", DefaultValue = 8, MinValue = 0)]
        public int AdxDiOffset { get; set; }

        // ===========================
        // Stoploss / Take Profit
        // ===========================
        [Parameter("SL Mode", Group = "Stoploss", DefaultValue = StopLossMode.ATR)]
        public StopLossMode SlMode { get; set; }

        [Parameter("SL Value", Group = "Stoploss", DefaultValue = 1.5, MinValue = 0.1)]
        public double SlValue { get; set; }

        [Parameter("ATR Period", Group = "Stoploss", DefaultValue = 14, MinValue = 1)]
        public int AtrPeriod { get; set; }

        [Parameter("Break Even Mode", Group = "Stoploss", DefaultValue = BreakEvenMode.RiskMultiplier)]
        public BreakEvenMode BeMode { get; set; }

        [Parameter("Break Even Value", Group = "Stoploss", DefaultValue = 1.0, MinValue = 0.0)]
        public double BeValue { get; set; }

        [Parameter("TP Mode", Group = "Take Profit", DefaultValue = TakeProfitMode.RiskMultiplier)]
        public TakeProfitMode TpMode { get; set; }

        [Parameter("TP Value", Group = "Take Profit", DefaultValue = 2.0, MinValue = 0.0)]
        public double TpValue { get; set; }

        // ===========================
        // Risk Management
        // ===========================
        [Parameter("Trade Risk (USD)", Group = "Risk Management", DefaultValue = 175.0, MinValue = 0.0)]
        public double TradeRiskUsd { get; set; }

        [Parameter("Daily Loss Limit (USD)", Group = "Risk Management", DefaultValue = 1500.0, MinValue = 0.0)]
        public double DailyLossLimitUsd { get; set; }

        [Parameter("Halt On Daily Limit", Group = "Risk Management", DefaultValue = true)]
        public bool HaltOnDailyLimit { get; set; }

        // ===========================
        // Fitness Filters (optimizer only)
        // ===========================
        [Parameter("Use Min Trades", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMinTrades { get; set; }

        [Parameter("Min Trades", Group = "Fitness Filters", DefaultValue = 50, MinValue = 1)]
        public int MinTrades { get; set; }

        [Parameter("Use Min Profit Factor", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMinProfitFactor { get; set; }

        [Parameter("Min Profit Factor", Group = "Fitness Filters", DefaultValue = 1.20, MinValue = 0.0)]
        public double MinProfitFactor { get; set; }

        [Parameter("Use Max Consecutive Losers", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMaxConsecLosers { get; set; }

        [Parameter("Max Consecutive Losing Trades", Group = "Fitness Filters", DefaultValue = 6, MinValue = 1)]
        public int MaxConsecutiveLosingTrades { get; set; }

        [Parameter("Use Max Equity DD %", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMaxEquityDd { get; set; }

        [Parameter("Max Equity Drawdown %", Group = "Fitness Filters", DefaultValue = 5.0, MinValue = 0.0, MaxValue = 100.0)]
        public double MaxEquityDrawdownPercent { get; set; }

        [Parameter("Use Max Balance DD %", Group = "Fitness Filters", DefaultValue = true)]
        public bool UseMaxBalanceDd { get; set; }

        [Parameter("Max Balance Drawdown %", Group = "Fitness Filters", DefaultValue = 5.0, MinValue = 0.0, MaxValue = 100.0)]
        public double MaxBalanceDrawdownPercent { get; set; }

        // ===========================
        // Indicators / state
        // ===========================
        private ExponentialMovingAverage _retestEma;
        private ExponentialMovingAverage _fastEma;
        private ExponentialMovingAverage _slowEma;
        private ExponentialMovingAverage _trendEma;
        private AverageTrueRange _atr;
        private DirectionalMovementSystem _dms;

        private int _lastBarIndex = -1;
        private bool _prevDoubleEmaLong;
        private bool _prevDoubleEmaShort;

        private enum PendingSide
        {
            None,
            Long,
            Short
        }

        private PendingSide _pending = PendingSide.None;
        private int _pendingBarIndex = -1;

        private bool _isConnected = true;
        private const int ReconnectDelaySeconds = 10;

        private DateTime _dayKey = DateTime.MinValue;
        private double _dayStartEquity;
        private bool _dailyHaltActive;

        protected override void OnStart()
        {
            if (FastEmaLength >= SlowEmaLength)
            {
                Print("Invalid EMA setup: Fast EMA must be < Slow EMA. Bot stopped.");
                Stop();
                return;
            }

            BindIndicators();
            ResetDayIfNeeded();

            // Seed previous double-EMA state from last closed bar.
            int i = Bars.Count - 2;
            if (i >= 0)
            {
                bool longState = _fastEma.Result[i] > _slowEma.Result[i];
                _prevDoubleEmaLong = longState;
                _prevDoubleEmaShort = !longState;
            }

            if (EnableDisconnectPause)
            {
                Server.Connected += OnConnected;
                Server.Disconnected += OnDisconnected;
            }

            Print("BrightFundedEmaChallenge started. Symbol={0} TF={1} Risk={2} DailyLimit={3}",
                Symbol.Name, TimeFrame, TradeRiskUsd, DailyLossLimitUsd);
        }

        private void BindIndicators()
        {
            _retestEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, RetestEmaLength);
            _fastEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, FastEmaLength);
            _slowEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, SlowEmaLength);
            _trendEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, TrendEmaLength);
            _atr = Indicators.AverageTrueRange(Bars, AtrPeriod, MovingAverageType.Simple);

            var adxBars = MarketData.GetBars(AdxTimeFrame);
            _dms = Indicators.DirectionalMovementSystem(adxBars, AdxPeriod);

            _lastBarIndex = Bars.Count - 1;
        }

        private void OnDisconnected()
        {
            Timer.Stop();
            _isConnected = false;
            Print("Disconnected at {0} — trading paused", Server.Time);
        }

        private void OnConnected()
        {
            Print("Reconnected at {0} — waiting {1}s", Server.Time, ReconnectDelaySeconds);
            Timer.Start(ReconnectDelaySeconds);
        }

        protected override void OnTimer()
        {
            Timer.Stop();
            BindIndicators();
            _isConnected = true;
            Print("Connection settled — trading resumed at {0}", Server.Time);
        }

        protected override void OnTick()
        {
            if (!_isConnected)
                return;

            ResetDayIfNeeded();
            UpdateDailyHalt();

            ApplyBreakEven();

            if (Bars.Count - 1 <= _lastBarIndex)
                return;

            _lastBarIndex = Bars.Count - 1;
            OnBarClose();
        }

        private void OnBarClose()
        {
            ExpirePendingIfNeeded();

            var position = Positions.Find(BotTradeId, Symbol.Name);
            bool isFlat = position == null;

            // New triggers only when flat and not daily-halted.
            if (isFlat && !_dailyHaltActive)
            {
                if (CanTradeLong() && IsDoubleEmaLongTrigger() && PassesLongFilters())
                {
                    _pending = PendingSide.Long;
                    _pendingBarIndex = Bars.Count - 1;
                    Print("Long pending — waiting EMA{0} retest", RetestEmaLength);
                }
                else if (CanTradeShort() && IsDoubleEmaShortTrigger() && PassesShortFilters())
                {
                    _pending = PendingSide.Short;
                    _pendingBarIndex = Bars.Count - 1;
                    Print("Short pending — waiting EMA{0} retest", RetestEmaLength);
                }
            }

            // Retest fill
            if (isFlat && !_dailyHaltActive && _pending != PendingSide.None)
            {
                if (_pending == PendingSide.Long && IsRetestEntry(TradeType.Buy) && PassesLongFilters())
                {
                    ExecuteTrade(TradeType.Buy);
                    _pending = PendingSide.None;
                }
                else if (_pending == PendingSide.Short && IsRetestEntry(TradeType.Sell) && PassesShortFilters())
                {
                    ExecuteTrade(TradeType.Sell);
                    _pending = PendingSide.None;
                }
            }

            // Keep previous double-EMA state updated every closed bar.
            int i = Bars.Count - 2;
            if (i >= 0)
            {
                bool longState = _fastEma.Result[i] > _slowEma.Result[i];
                _prevDoubleEmaLong = longState;
                _prevDoubleEmaShort = !longState;
            }
        }

        private bool CanTradeLong()
        {
            return OrderDirection == TradeDirectionMode.LongAndShort
                || OrderDirection == TradeDirectionMode.LongOnly;
        }

        private bool CanTradeShort()
        {
            return OrderDirection == TradeDirectionMode.LongAndShort
                || OrderDirection == TradeDirectionMode.ShortOnly;
        }

        private bool IsDoubleEmaLongTrigger()
        {
            int i = Bars.Count - 2;
            if (i < 0)
                return false;

            bool met = _fastEma.Result[i] > _slowEma.Result[i];
            bool rising = met && !_prevDoubleEmaLong;
            return rising;
        }

        private bool IsDoubleEmaShortTrigger()
        {
            int i = Bars.Count - 2;
            if (i < 0)
                return false;

            bool met = _fastEma.Result[i] < _slowEma.Result[i];
            bool rising = met && !_prevDoubleEmaShort;
            return rising;
        }

        private bool PassesLongFilters()
        {
            int i = Bars.Count - 2;
            if (i < 0)
                return false;

            if (UsePriceEmaFilter && Symbol.Ask <= _trendEma.Result[i])
                return false;

            if (UseAdxTrendFilter)
            {
                double plus = _dms.DIPlus.Last(1);
                double minus = _dms.DIMinus.Last(1);
                if (Math.Abs(plus - minus) < AdxDiOffset)
                    return false;
                if (plus <= minus)
                    return false;
            }

            return true;
        }

        private bool PassesShortFilters()
        {
            int i = Bars.Count - 2;
            if (i < 0)
                return false;

            if (UsePriceEmaFilter && Symbol.Bid >= _trendEma.Result[i])
                return false;

            if (UseAdxTrendFilter)
            {
                double plus = _dms.DIPlus.Last(1);
                double minus = _dms.DIMinus.Last(1);
                if (Math.Abs(plus - minus) < AdxDiOffset)
                    return false;
                if (minus <= plus)
                    return false;
            }

            return true;
        }

        private bool IsRetestEntry(TradeType tradeType)
        {
            int i = Bars.Count - 2;
            if (i < 0)
                return false;

            double level = _retestEma.Result[i];
            double low = Bars.LowPrices[i];
            double high = Bars.HighPrices[i];
            double close = Bars.ClosePrices[i];

            return tradeType == TradeType.Buy
                ? low < level && close > level
                : high > level && close < level;
        }

        private void ExpirePendingIfNeeded()
        {
            if (_pending == PendingSide.None || MaxPendingBars <= 0)
                return;

            int age = (Bars.Count - 1) - _pendingBarIndex;
            if (age >= MaxPendingBars)
            {
                Print("Pending {0} expired after {1} bars", _pending, age);
                _pending = PendingSide.None;
            }
        }

        private void ResetDayIfNeeded()
        {
            var key = Server.Time.Date;
            if (key == _dayKey)
                return;

            _dayKey = key;
            _dayStartEquity = Account.Equity;
            _dailyHaltActive = false;
            Print("New day {0:yyyy-MM-dd} — day-start equity {1:F2}", key, _dayStartEquity);
        }

        private void UpdateDailyHalt()
        {
            if (!HaltOnDailyLimit || DailyLossLimitUsd <= 0)
            {
                _dailyHaltActive = false;
                return;
            }

            double dayPnl = Account.Equity - _dayStartEquity;
            if (dayPnl <= -DailyLossLimitUsd)
            {
                if (!_dailyHaltActive)
                {
                    _dailyHaltActive = true;
                    _pending = PendingSide.None;
                    Print("Daily loss limit hit ({0:F2}). New entries halted for today.", dayPnl);
                }
            }
        }

        private void ApplyBreakEven()
        {
            var position = Positions.Find(BotTradeId, Symbol.Name);
            if (position == null || BeMode == BreakEvenMode.None || BeValue <= 0)
                return;

            if (position.StopLoss.HasValue &&
                Math.Abs(position.StopLoss.Value - position.EntryPrice) < Symbol.PipSize * 0.1)
                return;

            double thresholdPips = BeMode switch
            {
                BreakEvenMode.Pips => BeValue,
                BreakEvenMode.RiskMultiplier when position.StopLoss.HasValue =>
                    Math.Abs((position.EntryPrice - position.StopLoss.Value) / Symbol.PipSize) * BeValue,
                _ => double.MaxValue
            };

            if (position.Pips >= thresholdPips)
                position.ModifyStopLossPrice(position.EntryPrice);
        }

        private void ExecuteTrade(TradeType tradeType)
        {
            if (Positions.Find(BotTradeId, Symbol.Name) != null)
                return;

            double? stopLossPips = null;
            switch (SlMode)
            {
                case StopLossMode.Pips:
                    stopLossPips = SlValue;
                    break;
                case StopLossMode.ATR:
                    stopLossPips = (_atr.Result.Last(1) * SlValue) / Symbol.PipSize;
                    break;
            }

            if (!stopLossPips.HasValue || stopLossPips.Value <= 0)
            {
                Print("Trade skipped — invalid stop loss.");
                return;
            }

            double volumeInUnits;
            if (TradeRiskUsd > 0)
            {
                double volumeInLots = TradeRiskUsd / ((stopLossPips.Value * Symbol.PipValue) * Symbol.LotSize);
                volumeInUnits = Symbol.QuantityToVolumeInUnits(volumeInLots);
            }
            else
            {
                volumeInUnits = Symbol.VolumeInUnitsMin;
            }

            if (volumeInUnits < Symbol.VolumeInUnitsMin)
                volumeInUnits = Symbol.VolumeInUnitsMin;
            volumeInUnits = Symbol.NormalizeVolumeInUnits(volumeInUnits);

            double? takeProfitPips = null;
            switch (TpMode)
            {
                case TakeProfitMode.Pips:
                    takeProfitPips = TpValue;
                    break;
                case TakeProfitMode.RiskMultiplier:
                    takeProfitPips = stopLossPips.Value * TpValue;
                    break;
            }

            var result = ExecuteMarketOrder(
                tradeType,
                Symbol.Name,
                volumeInUnits,
                BotTradeId,
                stopLossPips,
                takeProfitPips);

            if (result.IsSuccessful)
            {
                Print("Filled {0} vol={1} SL={2:F1} TP={3}",
                    tradeType, volumeInUnits, stopLossPips, takeProfitPips);
            }
            else
            {
                Print("Order failed: {0}", result.Error);
            }
        }

        protected override void OnStop()
        {
            if (EnableDisconnectPause)
            {
                Server.Connected -= OnConnected;
                Server.Disconnected -= OnDisconnected;
            }

            Timer.Stop();
            Print("BrightFundedEmaChallenge stopped.");
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

            if (UseMaxEquityDd || UseMaxBalanceDd)
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

                if (UseMaxEquityDd && ddPct > MaxEquityDrawdownPercent)
                    return 0;
                if (UseMaxBalanceDd && ddPct > MaxBalanceDrawdownPercent)
                    return 0;
            }

            return netProfit;
        }
    }
}
