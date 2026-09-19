// Ultimate BTC Bot — single flagship build from full-history + M15 prop research.
// Chart: BTCUSD M15 only. One strategy, multiple filters (quality stack), prop-safe risk.
// Honest goal: best *evidence-based* stack we tested — not a guarantee of future profit.

using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None, AddIndicators = true)]
    public class UltimateBtcBot : Robot
    {
        private const string Label = "Ultimate-BTC";

        [Parameter("Challenge risk % (Monday equity)", Group = "Account", DefaultValue = 0.8, MinValue = 0.1)]
        public double ChallengeRiskPercent { get; set; }

        [Parameter("Funded phase (half risk)", Group = "Account", DefaultValue = false)]
        public bool FundedPhase { get; set; }

        [Parameter("Week drawdown brake %", Group = "Account", DefaultValue = 3.5, MinValue = 0.5)]
        public double WeekMaxDrawdownPercent { get; set; }

        [Parameter("Monday skip week if H4 ADX <", Group = "Regime", DefaultValue = 20, MinValue = 5)]
        public double MondaySkipAdx { get; set; }

        [Parameter("Min M15 ADX to enter", Group = "Regime", DefaultValue = 20, MinValue = 5)]
        public double MinM15Adx { get; set; }

        [Parameter("Require +DI/-DI with trade", Group = "Regime", DefaultValue = true)]
        public bool RequireDiAlignment { get; set; }

        [Parameter("Max hold (M15 bars)", Group = "Trade", DefaultValue = 48, MinValue = 4)]
        public int MaxHoldBars { get; set; }

        [Parameter("Stop = ATR ×", Group = "Trade", DefaultValue = 2.5, MinValue = 0.5)]
        public double StopAtrMultiple { get; set; }

        [Parameter("Take-profit R", Group = "Trade", DefaultValue = 2.5, MinValue = 1.0)]
        public double RewardRiskMultiple { get; set; }

        [Parameter("Move SL to breakeven at 1R", Group = "Trade", DefaultValue = true)]
        public bool BreakevenAtOneR { get; set; }

        [Parameter("Flatten Friday 20:00 UTC", Group = "Trade", DefaultValue = true)]
        public bool FlattenBeforeWeekend { get; set; }

        [Parameter("Allow long", Group = "Trade", DefaultValue = true)]
        public bool AllowLong { get; set; }

        [Parameter("Allow short", Group = "Trade", DefaultValue = true)]
        public bool AllowShort { get; set; }

        [Parameter("Log decisions", Group = "Debug", DefaultValue = true)]
        public bool LogDecisions { get; set; }

        private AverageTrueRange _atr;
        private ExponentialMovingAverage _ema50, _ema200;
        private DirectionalMovementSystem _m15Dms;
        private Bars _h4Bars;
        private DirectionalMovementSystem _h4Dms;
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
                Print("Ultimate BTC is built for M15; you are on {0}.", TimeFrame);

            _atr = Indicators.AverageTrueRange(14, MovingAverageType.Exponential);
            _ema50 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 50);
            _ema200 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 200);
            _m15Dms = Indicators.DirectionalMovementSystem(14);

            _h4Bars = MarketData.GetBars(TimeFrame.Hour4);
            _h4Dms = Indicators.DirectionalMovementSystem(_h4Bars, 14);
            _h4Ema55 = Indicators.ExponentialMovingAverage(_h4Bars.ClosePrices, 55);

            ResetWeek(Server.Time);
            Positions.Opened += OnPositionOpened;

            if (LogDecisions)
            {
                Print("Ultimate BTC | EMA50/200 + H4 trend + ADX gates | hold≤{0} bars (~{1:F0}h) | risk {2:F2}%",
                    MaxHoldBars, MaxHoldBars / 4.0, _activeRiskPercent);
            }
        }

        protected override void OnBarClosed()
        {
            EnsureWeekRollover();
            UpdateMondayGate();

            if (FlattenBeforeWeekend && Server.Time.DayOfWeek == DayOfWeek.Friday && Server.Time.Hour >= 20)
                CloseAll("Weekend flat");

            ManagePositions();

            if (!CanEnter() || BotPositionCount() >= 1)
                return;

            TryEntry();
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

        private void TryEntry()
        {
            if (Bars.Count < 220)
                return;

            int i = 1;
            bool crossUp = _ema50.Result.Last(i + 1) <= _ema200.Result.Last(i + 1) && _ema50.Result.Last(i) > _ema200.Result.Last(i);
            bool crossDn = _ema50.Result.Last(i + 1) >= _ema200.Result.Last(i + 1) && _ema50.Result.Last(i) < _ema200.Result.Last(i);

            if (!crossUp && !crossDn)
                return;

            double m15Adx = _m15Dms.ADX.Last(i);
            if (m15Adx < MinM15Adx)
                return;

            bool h4Up = _h4Bars.ClosePrices.Last(1) > _h4Ema55.Result.Last(1);
            bool h4Dn = _h4Bars.ClosePrices.Last(1) < _h4Ema55.Result.Last(1);

            bool diUp = _m15Dms.DIPlus.Last(i) > _m15Dms.DIMinus.Last(i);
            bool diDn = _m15Dms.DIMinus.Last(i) > _m15Dms.DIPlus.Last(i);

            bool wantLong = crossUp && h4Up && (!RequireDiAlignment || diUp);
            bool wantShort = crossDn && h4Dn && (!RequireDiAlignment || diDn);

            double atr = _atr.Result.Last(i);
            if (atr <= 0)
                return;

            if (AllowLong && wantLong)
                Open(TradeType.Buy, atr);
            else if (AllowShort && wantShort)
                Open(TradeType.Sell, atr);
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

            if (LogDecisions)
                Print("{0} EMA50/200 cross | entry {1:F2} SL {2:F2} TP {3:F2}", side, entry, sl, tp);
        }

        private void ManagePositions()
        {
            foreach (var p in BotPositions())
            {
                _barsInTrade++;

                if (BreakevenAtOneR && p.StopLoss != null)
                    TryBreakeven(p);

                if (_barsInTrade >= MaxHoldBars)
                    ClosePosition(p);
            }
        }

        private void TryBreakeven(Position p)
        {
            double entry = p.EntryPrice;
            double? sl = p.StopLoss;
            if (!sl.HasValue)
                return;

            double risk = Math.Abs(entry - sl.Value);
            if (risk <= 0)
                return;

            double oneR = p.TradeType == TradeType.Buy ? entry + risk : entry - risk;
            bool pastOneR = p.TradeType == TradeType.Buy ? Symbol.Bid >= oneR : Symbol.Ask <= oneR;
            bool slStillRisk = p.TradeType == TradeType.Buy ? sl.Value < entry : sl.Value > entry;

            if (pastOneR && slStillRisk)
                ModifyPosition(p, entry, p.TakeProfit, ProtectionType.Absolute);
        }

        private void CloseAll(string reason)
        {
            foreach (var p in BotPositions())
            {
                ClosePosition(p);
                if (LogDecisions)
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

            if (LogDecisions)
                Print("Week {0} start equity {1:F2} | risk {2:F2}%", _isoWeek, _weekStartEquity, _activeRiskPercent);
        }

        private void UpdateMondayGate()
        {
            if (Server.Time.DayOfWeek != DayOfWeek.Monday)
                return;
            if (_lastMondayCheck.Date == Server.Time.Date)
                return;
            _lastMondayCheck = Server.Time.Date;

            double adx = _h4Dms.ADX.Last(1);
            _weekTradingAllowed = adx >= MondaySkipAdx;

            if (LogDecisions)
                Print("Monday H4 ADX {0:F1} → {1}", adx, _weekTradingAllowed ? "TRADE week" : "SKIP week");
        }

        private long VolumeForRisk(double stopDistancePrice)
        {
            double riskCash = Account.Equity * (_activeRiskPercent / 100.0);
            if (Symbol.TickSize <= 0 || Symbol.TickValue <= 0 || stopDistancePrice <= 0)
                return 0;
            double ticks = stopDistancePrice / Symbol.TickSize;
            return Symbol.NormalizeVolumeInUnits(riskCash / (ticks * Symbol.TickValue), RoundingMode.Down);
        }

        private int BotPositionCount() => BotPositions().Length;

        private Position[] BotPositions()
        {
            return Positions.Where(p => p.Label == Label && p.SymbolName == SymbolName).ToArray();
        }

        private static int GetIsoWeek(DateTime utc)
        {
            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            return cal.GetWeekOfYear(utc, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }
    }
}
