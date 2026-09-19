// BrightFunded $50k Classic — BTCUSD M15 weekly regime bot
// Research: M15 EMA 50/200 + H4 filter + max hold 12–16h → frequent trades, not multi-day holds.
// See BRIGHTFUNDED_M15.md and scripts/weekly_prop_sim.py

using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    public enum WeeklyTriggerMode
    {
        Ema50200,
        Ema2155,
        KeltnerBreakHtf,
        Aroon25Htf,
        DiCrossHtf
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None, AddIndicators = true)]
    public class BrightFundedBtcM15Bot : Robot
    {
        private const string Label = "BF-BTC-M15";

        [Parameter("Challenge risk % (Monday equity)", Group = "BrightFunded", DefaultValue = 0.8, MinValue = 0.1)]
        public double ChallengeRiskPercent { get; set; }

        [Parameter("Funded phase (use half risk)", Group = "BrightFunded", DefaultValue = false)]
        public bool FundedPhase { get; set; }

        [Parameter("Week drawdown brake %", Group = "BrightFunded", DefaultValue = 3.5, MinValue = 0.5)]
        public double WeekMaxDrawdownPercent { get; set; }

        [Parameter("Monday skip if H4 ADX <", Group = "BrightFunded", DefaultValue = 20, MinValue = 5)]
        public double MondaySkipAdx { get; set; }

        [Parameter("Use Monday ADX week gate", Group = "BrightFunded", DefaultValue = true)]
        public bool UseMondayAdxGate { get; set; }

        [Parameter("Entry trigger (weekly tune)", Group = "Signal", DefaultValue = WeeklyTriggerMode.Ema50200)]
        public WeeklyTriggerMode Trigger { get; set; }

        [Parameter("H4 EMA55 trend filter", Group = "Signal", DefaultValue = true)]
        public bool UseH4TrendFilter { get; set; }

        [Parameter("Max hold (M15 bars)", Group = "Exit", DefaultValue = 48, MinValue = 4)]
        public int MaxHoldBars { get; set; }

        [Parameter("Stop = ATR ×", Group = "Exit", DefaultValue = 2.5, MinValue = 0.5)]
        public double StopAtrMultiple { get; set; }

        [Parameter("Take-profit R", Group = "Exit", DefaultValue = 2.5, MinValue = 1.0)]
        public double RewardRiskMultiple { get; set; }

        [Parameter("ATR period", Group = "Exit", DefaultValue = 14, MinValue = 5)]
        public int AtrPeriod { get; set; }

        [Parameter("Close open trades Friday 20:00 UTC", Group = "Exit", DefaultValue = true)]
        public bool FlattenBeforeWeekend { get; set; }

        [Parameter("Max positions", Group = "Risk", DefaultValue = 1, MinValue = 1)]
        public int MaxPositions { get; set; }

        [Parameter("Allow long", Group = "Risk", DefaultValue = true)]
        public bool AllowLong { get; set; }

        [Parameter("Allow short", Group = "Risk", DefaultValue = true)]
        public bool AllowShort { get; set; }

        [Parameter("Log weekly state", Group = "Debug", DefaultValue = true)]
        public bool LogState { get; set; }

        private AverageTrueRange _atr;
        private ExponentialMovingAverage _ema20, _ema50, _ema200, _ema21, _ema55;
        private DirectionalMovementSystem _dms;
        private DirectionalMovementSystem _h4Dms;
        private AroonOscillator _aroon;
        private Bars _h4Bars;
        private ExponentialMovingAverage _h4Ema55;

        private double _activeRiskPercent;
        private double _weekStartEquity;
        private int _isoWeek = -1;
        private bool _weekTradingAllowed = true;
        private DateTime _lastMondayCheck = DateTime.MinValue;
        private int _barsInTrade;

        protected override void OnStart()
        {
            if (TimeFrame != TimeFrame.Minute15)
                Print("Warning: designed for M15; current chart is {0}", TimeFrame);

            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Exponential);
            _ema20 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 20);
            _ema50 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 50);
            _ema200 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 200);
            _ema21 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 21);
            _ema55 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 55);
            _dms = Indicators.DirectionalMovementSystem(14);
            _aroon = Indicators.AroonOscillator(25);

            _h4Bars = MarketData.GetBars(TimeFrame.Hour4);
            _h4Dms = Indicators.DirectionalMovementSystem(14, _h4Bars);
            _h4Ema55 = Indicators.ExponentialMovingAverage(_h4Bars.ClosePrices, 55);

            ResetWeek(Server.Time);
            Positions.Opened += OnPositionOpened;

            if (LogState)
            {
                Print("BrightFunded M15 bot | Trigger={0} | Max hold={1} bars (~{2}h) | Risk={3:F2}%",
                    Trigger, MaxHoldBars, MaxHoldBars / 4.0, _activeRiskPercent);
            }
        }

        protected override void OnBarClosed()
        {
            EnsureWeekRollover();
            UpdateMondayGate();

            if (FlattenBeforeWeekend && Server.Time.DayOfWeek == DayOfWeek.Friday && Server.Time.Hour >= 20)
                CloseAllBotPositions("Weekend flat");

            ManageOpenPositions();

            if (!CanEnter())
                return;
            if (CountBotPositions() >= MaxPositions)
                return;

            TrySignal();
        }

        protected override void OnStop()
        {
            Positions.Opened -= OnPositionOpened;
        }

        private void OnPositionOpened(PositionOpenedEventArgs args)
        {
            if (args.Position.Label == Label)
                _barsInTrade = 0;
        }

        private void TrySignal()
        {
            int i = 1;
            if (Bars.Count < 220)
                return;

            bool wantLong, wantShort;
            EvaluateTrigger(i, out wantLong, out wantShort);

            if (UseH4TrendFilter)
            {
                bool h4Up = _h4Bars.ClosePrices.Last(1) > _h4Ema55.Result.Last(1);
                bool h4Dn = _h4Bars.ClosePrices.Last(1) < _h4Ema55.Result.Last(1);
                wantLong &= h4Up;
                wantShort &= h4Dn;
            }

            double atr = _atr.Result.Last(i);
            if (atr <= 0)
                return;

            if (AllowLong && wantLong)
                Open(TradeType.Buy, atr);
            else if (AllowShort && wantShort)
                Open(TradeType.Sell, atr);
        }

        private void EvaluateTrigger(int i, out bool wantLong, out bool wantShort)
        {
            wantLong = wantShort = false;

            switch (Trigger)
            {
                case WeeklyTriggerMode.Ema50200:
                    wantLong = _ema50.Result.Last(i + 1) <= _ema200.Result.Last(i + 1) && _ema50.Result.Last(i) > _ema200.Result.Last(i);
                    wantShort = _ema50.Result.Last(i + 1) >= _ema200.Result.Last(i + 1) && _ema50.Result.Last(i) < _ema200.Result.Last(i);
                    break;
                case WeeklyTriggerMode.Ema2155:
                    wantLong = _ema21.Result.Last(i + 1) <= _ema55.Result.Last(i + 1) && _ema21.Result.Last(i) > _ema55.Result.Last(i);
                    wantShort = _ema21.Result.Last(i + 1) >= _ema55.Result.Last(i + 1) && _ema21.Result.Last(i) < _ema55.Result.Last(i);
                    break;
                case WeeklyTriggerMode.KeltnerBreakHtf:
                    KeltnerBreak(i, out wantLong, out wantShort);
                    break;
                case WeeklyTriggerMode.Aroon25Htf:
                    double up = _aroon.Up.Last(i), down = _aroon.Down.Last(i);
                    double up1 = _aroon.Up.Last(i + 1), down1 = _aroon.Down.Last(i + 1);
                    wantLong = up1 <= down1 && up > down && up > 50;
                    wantShort = down1 <= up1 && down > up && down > 50;
                    break;
                case WeeklyTriggerMode.DiCrossHtf:
                    wantLong = _dms.DIPlus.Last(i + 1) <= _dms.DIMinus.Last(i + 1) && _dms.DIPlus.Last(i) > _dms.DIMinus.Last(i);
                    wantShort = _dms.DIPlus.Last(i + 1) >= _dms.DIMinus.Last(i + 1) && _dms.DIPlus.Last(i) < _dms.DIMinus.Last(i);
                    break;
            }
        }

        private void KeltnerBreak(int i, out bool lng, out bool shrt)
        {
            double band = 2.0 * _atr.Result.Last(i);
            double up = _ema20.Result.Last(i) + band;
            double lo = _ema20.Result.Last(i) - band;
            lng = Bars.ClosePrices.Last(i + 1) <= up && Bars.ClosePrices.Last(i) > up;
            shrt = Bars.ClosePrices.Last(i + 1) >= lo && Bars.ClosePrices.Last(i) < lo;
        }

        private void Open(TradeType side, double atr)
        {
            double entry = Bars.ClosePrices.Last(1);
            double stopDist = StopAtrMultiple * atr;
            double sl = side == TradeType.Buy ? entry - stopDist : entry + stopDist;
            double tp = side == TradeType.Buy ? entry + RewardRiskMultiple * stopDist : entry - RewardRiskMultiple * stopDist;

            long vol = VolumeForRisk(stopDist);
            if (vol < Symbol.VolumeInUnitsMin)
                return;

            var res = ExecuteMarketOrder(side, SymbolName, vol, Label);
            if (!res.IsSuccessful)
                return;
            ModifyPosition(res.Position, sl, tp, ProtectionType.Absolute);
        }

        private void ManageOpenPositions()
        {
            foreach (var p in BotPositions())
            {
                _barsInTrade++;
                if (_barsInTrade >= MaxHoldBars)
                    ClosePosition(p);
            }
        }

        private void CloseAllBotPositions(string reason)
        {
            foreach (var p in BotPositions())
            {
                ClosePosition(p);
                if (LogState)
                    Print("Closed {0}: {1}", p.Id, reason);
            }
        }

        private bool CanEnter()
        {
            if (!_weekTradingAllowed)
                return false;

            if (_weekStartEquity <= 0)
                return true;

            double dd = (_weekStartEquity - Account.Equity) / _weekStartEquity * 100.0;
            return dd < WeekMaxDrawdownPercent;
        }

        private void EnsureWeekRollover()
        {
            int w = GetIsoWeek(Server.Time);
            if (w == _isoWeek)
                return;
            ResetWeek(Server.Time);
        }

        private void ResetWeek(DateTime utc)
        {
            _isoWeek = GetIsoWeek(utc);
            _weekStartEquity = Account.Equity;
            _activeRiskPercent = FundedPhase ? ChallengeRiskPercent / 2.0 : ChallengeRiskPercent;
            _weekTradingAllowed = true;
            _lastMondayCheck = DateTime.MinValue;

            if (LogState)
            {
                Print("--- ISO week {0} --- equity {1:F2} risk {2:F2}% trigger {3}",
                    _isoWeek, _weekStartEquity, _activeRiskPercent, Trigger);
            }
        }

        private void UpdateMondayGate()
        {
            if (!UseMondayAdxGate || Server.Time.DayOfWeek != DayOfWeek.Monday)
                return;
            if (_lastMondayCheck.Date == Server.Time.Date)
                return;
            _lastMondayCheck = Server.Time.Date;

            double adx = _h4Dms.ADX.Last(1);
            _weekTradingAllowed = adx >= MondaySkipAdx;

            if (LogState)
            {
                Print("Monday H4 ADX={0:F1} → week {1}", adx, _weekTradingAllowed ? "TRADE" : "SKIP (chop)");
            }
        }

        private long VolumeForRisk(double stopDistancePrice)
        {
            double riskCash = Account.Equity * (_activeRiskPercent / 100.0);
            if (Symbol.TickSize <= 0 || Symbol.TickValue <= 0 || stopDistancePrice <= 0)
                return 0;
            double ticks = stopDistancePrice / Symbol.TickSize;
            return Symbol.NormalizeVolumeInUnits(riskCash / (ticks * Symbol.TickValue), RoundingMode.Down);
        }

        private Position[] BotPositions()
        {
            return Positions.Where(p => p.Label == Label && p.SymbolName == SymbolName).ToArray();
        }

        private int CountBotPositions() => BotPositions().Length;

        private static int GetIsoWeek(DateTime utc)
        {
            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            return cal.GetWeekOfYear(utc, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }
    }
}
