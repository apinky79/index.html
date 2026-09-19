// ---------------------------------------------------------------------------
// XTXBtcTrendRegimeBot — BTC/USD trend bot for cTrader (cAlgo)
//
// Plain idea: only trade when Bitcoin is in a clear trend (not sideways chop),
// enter on pullbacks to the fast moving average, exit with fixed risk/reward.
//
// Attach to: BTCUSD chart, recommended H1 (works up to H4).
// Regime filter always reads H4 ADX regardless of chart timeframe.
// ---------------------------------------------------------------------------

using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None, AddIndicators = true)]
    public class XTXBtcTrendRegimeBot : Robot
    {
        private const string BotLabel = "XTX-BTC-Regime";

        // --- Trend & entry (tuned on ~10y daily + 2y H1 research) ---
        [Parameter("Fast EMA", Group = "Strategy", DefaultValue = 13, MinValue = 5)]
        public int FastEmaPeriod { get; set; }

        [Parameter("Slow EMA", Group = "Strategy", DefaultValue = 34, MinValue = 10)]
        public int SlowEmaPeriod { get; set; }

        [Parameter("ATR period", Group = "Strategy", DefaultValue = 14, MinValue = 5)]
        public int AtrPeriod { get; set; }

        [Parameter("Stop = ATR ×", Group = "Strategy", DefaultValue = 2.5, MinValue = 0.5)]
        public double StopAtrMultiple { get; set; }

        [Parameter("Reward : Risk", Group = "Strategy", DefaultValue = 2.5, MinValue = 1.0)]
        public double RewardRiskRatio { get; set; }

        // --- H4 regime gate (matches prop "Test F" research in TRADING_RULES.md) ---
        [Parameter("Use ADX regime gate", Group = "Regime", DefaultValue = true)]
        public bool UseAdxRegimeGate { get; set; }

        [Parameter("ADX enter (≥ = building trend)", Group = "Regime", DefaultValue = 25, MinValue = 10)]
        public double AdxEnterLevel { get; set; }

        [Parameter("ADX exit chop (< = reset)", Group = "Regime", DefaultValue = 20, MinValue = 5)]
        public double AdxExitLevel { get; set; }

        [Parameter("ADX confirm bars (H4)", Group = "Regime", DefaultValue = 3, MinValue = 1)]
        public int AdxConfirmBars { get; set; }

        [Parameter("Monday skip if H4 ADX <", Group = "Regime", DefaultValue = 20, MinValue = 5)]
        public double MondaySkipAdx { get; set; }

        [Parameter("Use weekly Monday gate", Group = "Regime", DefaultValue = true)]
        public bool UseWeeklyMondayGate { get; set; }

        // --- Risk (BrightFunded-style defaults from your rules) ---
        [Parameter("Risk % of equity", Group = "Risk", DefaultValue = 0.8, MinValue = 0.1)]
        public double RiskPercent { get; set; }

        [Parameter("Recalc risk on Monday", Group = "Risk", DefaultValue = true)]
        public bool MondayRiskResize { get; set; }

        [Parameter("Week max drawdown %", Group = "Risk", DefaultValue = 3.5, MinValue = 0.5)]
        public double WeekMaxDrawdownPercent { get; set; }

        [Parameter("Max open positions", Group = "Risk", DefaultValue = 1, MinValue = 1)]
        public int MaxOpenPositions { get; set; }

        [Parameter("Max hold (bars on chart TF)", Group = "Risk", DefaultValue = 96, MinValue = 1)]
        public int MaxHoldBars { get; set; }

        [Parameter("Allow long", Group = "Risk", DefaultValue = true)]
        public bool AllowLong { get; set; }

        [Parameter("Allow short", Group = "Risk", DefaultValue = true)]
        public bool AllowShort { get; set; }

        [Parameter("Verbose logs", Group = "Debug", DefaultValue = true)]
        public bool VerboseLogs { get; set; }

        private ExponentialMovingAverage _fastEma;
        private ExponentialMovingAverage _slowEma;
        private AverageTrueRange _atr;

        private Bars _h4Bars;
        private DirectionalMovementSystem _h4Dms;

        private int _adxConfirmCount;
        private bool _regimeAllowsTrading;
        private bool _weekTradingAllowed = true;
        private double _weekStartEquity;
        private int _weekOfYear = -1;
        private double _activeRiskPercent;
        private int _positionBarCount;

        protected override void OnStart()
        {
            _fastEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, FastEmaPeriod);
            _slowEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, SlowEmaPeriod);
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Exponential);

            _h4Bars = MarketData.GetBars(TimeFrame.Hour4);
            _h4Dms = Indicators.DirectionalMovementSystem(14, _h4Bars);

            _activeRiskPercent = RiskPercent;
            ResetWeekState(Server.Time);
            UpdateRegimeState();
            Positions.Opened += OnPositionOpened;

            if (VerboseLogs)
            {
                Print("XTX BTC Trend Regime bot started on {0}. Chart TF: {1}. H4 ADX gate: {2}.",
                    SymbolName, TimeFrame, UseAdxRegimeGate ? "ON" : "OFF");
                PrintHumanStatus();
            }
        }

        protected override void OnBarClosed()
        {
            ResetWeekStateIfNeeded();
            UpdateRegimeState();
            UpdateWeeklyMondayGate();

            foreach (var position in BotPositions())
                ManageMaxHold(position);

            if (!CanOpenNewTrade())
                return;

            if (CountBotPositions() >= MaxOpenPositions)
                return;

            TryEnter();
        }

        protected override void OnStop()
        {
            Positions.Opened -= OnPositionOpened;
            Print("Bot stopped. Final equity: {0} {1}", Account.Equity, Account.Asset.Name);
        }

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            if (args.Position.Label != BotLabel)
                return;
            _positionBarCount = 0;
        }

        private void TryEnter()
        {
            var index = Bars.Count - 1;
            if (index < Math.Max(SlowEmaPeriod, 50) + 2)
                return;

            double close = Bars.ClosePrices.Last(1);
            double prevClose = Bars.ClosePrices.Last(2);
            double fastPrev = _fastEma.Result.Last(2);
            double fastNow = _fastEma.Result.Last(1);
            double slowNow = _slowEma.Result.Last(1);
            double atr = _atr.Result.Last(1);

            if (atr <= 0 || double.IsNaN(atr))
                return;

            bool trendUp = _h4Dms.DIPlus.Last(1) > _h4Dms.DIMinus.Last(1);
            bool trendDown = _h4Dms.DIMinus.Last(1) > _h4Dms.DIPlus.Last(1);
            bool emaUp = fastNow > slowNow;
            bool emaDown = fastNow < slowNow;

            bool crossUp = prevClose < fastPrev && close > fastNow;
            bool crossDown = prevClose > fastPrev && close < fastNow;

            if (AllowLong && trendUp && emaUp && crossUp)
                OpenWithRisk(TradeType.Buy, close, atr);

            else if (AllowShort && trendDown && emaDown && crossDown)
                OpenWithRisk(TradeType.Sell, close, atr);
        }

        private void OpenWithRisk(TradeType side, double entryPrice, double atr)
        {
            double stopDistance = StopAtrMultiple * atr;
            double stopPrice = side == TradeType.Buy
                ? entryPrice - stopDistance
                : entryPrice + stopDistance;

            double takeProfitPrice = side == TradeType.Buy
                ? entryPrice + RewardRiskRatio * stopDistance
                : entryPrice - RewardRiskRatio * stopDistance;

            long volume = CalculateVolumeInUnits(stopDistance);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                if (VerboseLogs)
                    Print("Skip entry: position size below minimum (risk {0:F2}%, stop {1:F2}).",
                        _activeRiskPercent, stopDistance);
                return;
            }

            var result = ExecuteMarketOrder(side, SymbolName, volume, BotLabel);
            if (!result.IsSuccessful)
            {
                Print("Order failed: {0}", result.Error);
                return;
            }

            ModifyPosition(result.Position, stopPrice, takeProfitPrice, ProtectionType.Absolute);

            if (VerboseLogs)
            {
                Print("{0} entry @ {1:F2} | SL {2:F2} | TP {3:F2} | vol {4} | risk {5:F2}% equity",
                    side, entryPrice, stopPrice, takeProfitPrice, volume, _activeRiskPercent);
            }
        }

        private long CalculateVolumeInUnits(double stopDistancePrice)
        {
            double riskMoney = Account.Equity * (_activeRiskPercent / 100.0);
            double tickValue = Symbol.TickValue;
            double tickSize = Symbol.TickSize;
            if (tickValue <= 0 || tickSize <= 0 || stopDistancePrice <= 0)
                return 0;

            double stopInTicks = stopDistancePrice / tickSize;
            double volume = riskMoney / (stopInTicks * tickValue);
            return Symbol.NormalizeVolumeInUnits(volume, RoundingMode.Down);
        }

        private void UpdateRegimeState()
        {
            if (!UseAdxRegimeGate)
            {
                _regimeAllowsTrading = true;
                return;
            }

            double adx = _h4Dms.ADX.Last(1);
            if (adx >= AdxEnterLevel)
                _adxConfirmCount = Math.Min(_adxConfirmCount + 1, AdxConfirmBars);
            else if (adx < AdxExitLevel)
                _adxConfirmCount = 0;

            _regimeAllowsTrading = _adxConfirmCount >= AdxConfirmBars;
        }

        private void UpdateWeeklyMondayGate()
        {
            if (!UseWeeklyMondayGate)
            {
                _weekTradingAllowed = true;
                return;
            }

            var now = Server.Time;
            if (now.DayOfWeek != DayOfWeek.Monday)
                return;

            // Evaluate once per UTC day
            if (_lastMondayCheckDate == now.Date)
                return;
            _lastMondayCheckDate = now.Date;

            double adx = _h4Dms.ADX.Last(1);
            _weekTradingAllowed = adx >= MondaySkipAdx;

            if (VerboseLogs)
            {
                Print("Monday gate: H4 ADX = {0:F1} → week is {1}.",
                    adx, _weekTradingAllowed ? "TRADE" : "SKIP (sit out chop)");
            }
        }

        private DateTime _lastMondayCheckDate = DateTime.MinValue;

        private bool CanOpenNewTrade()
        {
            if (!_regimeAllowsTrading)
                return false;

            if (!_weekTradingAllowed)
                return false;

            if (WeekMaxDrawdownPercent > 0 && _weekStartEquity > 0)
            {
                double dd = (_weekStartEquity - Account.Equity) / _weekStartEquity * 100.0;
                if (dd >= WeekMaxDrawdownPercent)
                {
                    if (VerboseLogs)
                        Print("Week drawdown brake: {0:F2}% ≥ {1:F2}% — no new entries.", dd, WeekMaxDrawdownPercent);
                    return false;
                }
            }

            return true;
        }

        private void ResetWeekStateIfNeeded()
        {
            var now = Server.Time;
            if (GetIsoWeek(now) == _weekOfYear)
                return;
            ResetWeekState(now);
        }

        private void ResetWeekState(DateTime now)
        {
            _weekOfYear = GetIsoWeek(now);
            _weekStartEquity = Account.Equity;
            if (MondayRiskResize)
                _activeRiskPercent = RiskPercent;

            // Fresh week: assume tradable until Monday ADX check says otherwise.
            _weekTradingAllowed = true;
            _lastMondayCheckDate = DateTime.MinValue;

            if (VerboseLogs)
                Print("New week (ISO {0}): week-start equity {1:F2}, risk {2:F2}%.",
                    _weekOfYear, _weekStartEquity, _activeRiskPercent);
        }

        private static int GetIsoWeek(DateTime utc)
        {
            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            return cal.GetWeekOfYear(utc, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        private Position[] BotPositions()
        {
            return Positions.Where(p => p.Label == BotLabel && p.SymbolName == SymbolName).ToArray();
        }

        private int CountBotPositions()
        {
            return BotPositions().Length;
        }

        private void ManageMaxHold(Position position)
        {
            _positionBarCount++;
            if (_positionBarCount < MaxHoldBars)
                return;

            if (VerboseLogs)
                Print("Max hold reached ({0} bars) — closing {1}.", MaxHoldBars, position.Id);

            ClosePosition(position);
        }

        private void PrintHumanStatus()
        {
            double adx = _h4Dms.ADX.Last(1);
            string mood = adx >= AdxEnterLevel ? "trending" : adx < AdxExitLevel ? "choppy / sideways" : "uncertain";
            Print("Market mood (H4 ADX {0:F1}): {1}. Bot may trade: {2}.",
                adx, mood, CanOpenNewTrade() ? "YES" : "NO");
        }
    }
}
