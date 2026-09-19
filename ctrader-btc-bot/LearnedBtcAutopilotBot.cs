// Learned BTC Autopilot — runs on its own from consolidated research.
// M15 | EMA 50/200 | H4 trend + ADX gates | prop risk | ~12h max hold | no weekly param hopping.
// Pair with telegram_advisor/ for Sunday regime messages (optional).
// cTrader stops cBots after ~7 days — restart the instance; week/risk state is persisted locally.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess, AddIndicators = true)]
    public class LearnedBtcAutopilotBot : Robot
    {
        private const string Label = "Learned-Autopilot";
        private const double RestartWarnDays = 6.0;

        [Parameter("Challenge risk % (Monday equity)", Group = "Autopilot", DefaultValue = 0.8, MinValue = 0.1)]
        public double ChallengeRiskPercent { get; set; }

        [Parameter("Funded phase (half risk)", Group = "Autopilot", DefaultValue = false)]
        public bool FundedPhase { get; set; }

        [Parameter("Week drawdown brake %", Group = "Autopilot", DefaultValue = 3.5, MinValue = 0.5)]
        public double WeekMaxDrawdownPercent { get; set; }

        [Parameter("Persist week state across restarts", Group = "Autopilot", DefaultValue = true)]
        public bool PersistState { get; set; }

        [Parameter("Monday skip if H4 ADX <", Group = "Regime", DefaultValue = 20, MinValue = 5)]
        public double MondaySkipAdx { get; set; }

        [Parameter("Min M15 ADX to enter", Group = "Regime", DefaultValue = 20, MinValue = 5)]
        public double MinM15Adx { get; set; }

        [Parameter("Pause entries if H4 ADX <", Group = "Regime", DefaultValue = 18, MinValue = 5)]
        public double IntraWeekH4AdxPause { get; set; }

        [Parameter("Require DI alignment", Group = "Regime", DefaultValue = true)]
        public bool RequireDiAlignment { get; set; }

        [Parameter("Max hold (M15 bars)", Group = "Trade", DefaultValue = 48, MinValue = 4)]
        public int MaxHoldBars { get; set; }

        [Parameter("Stop = ATR ×", Group = "Trade", DefaultValue = 2.5, MinValue = 0.5)]
        public double StopAtrMultiple { get; set; }

        [Parameter("Take-profit R", Group = "Trade", DefaultValue = 2.5, MinValue = 1.0)]
        public double RewardRiskMultiple { get; set; }

        [Parameter("Breakeven at 1R", Group = "Trade", DefaultValue = true)]
        public bool BreakevenAtOneR { get; set; }

        [Parameter("Flatten Friday 20:00 UTC", Group = "Trade", DefaultValue = true)]
        public bool FlattenBeforeWeekend { get; set; }

        [Parameter("Allow long", Group = "Trade", DefaultValue = true)]
        public bool AllowLong { get; set; }

        [Parameter("Allow short", Group = "Trade", DefaultValue = true)]
        public bool AllowShort { get; set; }

        [Parameter("Status log each H4 bar", Group = "Debug", DefaultValue = true)]
        public bool LogStatus { get; set; }

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
        private DateTime _sessionStartedUtc;
        private DateTime _lastH4Log = DateTime.MinValue;

        private string StateFilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "cAlgo",
                "Data",
                "cBots",
                nameof(LearnedBtcAutopilotBot),
                "autopilot_state.txt");

        protected override void OnStart()
        {
            if (TimeFrame != TimeFrame.Minute15)
                Print("Learned Autopilot expects M15; current: {0}", TimeFrame);

            _atr = Indicators.AverageTrueRange(14, MovingAverageType.Exponential);
            _ema50 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 50);
            _ema200 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 200);
            _m15Dms = Indicators.DirectionalMovementSystem(14);
            _h4Bars = MarketData.GetBars(TimeFrame.Hour4);
            _h4Dms = Indicators.DirectionalMovementSystem(_h4Bars, 14);
            _h4Ema55 = Indicators.ExpponentialMovingAverage(_h4Bars.ClosePrices, 55);

            _sessionStartedUtc = Server.Time;
            if (!TryRestoreWeekState(Server.Time))
                ResetWeek(Server.Time, persist: false);

            SaveState();

            var open = BotPositions();
            if (open.Length > 0)
                Print("Reattached to {0} open position(s) with label {1}", open.Length, Label);

            Print("Learned Autopilot ON | fixed EMA50/200 | risk {0:F2}% | hold≤{1} bars",
                _activeRiskPercent, MaxHoldBars);
            Print("cTrader ~7-day limit: restart this cBot before it stops. State file: {0}", StateFilePath);
        }

        protected override void OnBarClosed()
        {
            WarnIfRestartDue();
            EnsureWeekRollover();
            UpdateMondayGate();
            LogH4StatusIfNewBar();

            if (FlattenBeforeWeekend && Server.Time.DayOfWeek == DayOfWeek.Friday && Server.Time.Hour >= 20)
                CloseAll("Weekend flat");

            ManagePositions();

            if (!CanEnter() || BotCount() >= 1)
                return;

            TryEntry();
        }

        protected override void OnStop()
        {
            SaveState();
        }

        private void WarnIfRestartDue()
        {
            double days = (Server.Time - _sessionStartedUtc).TotalDays;
            if (days < RestartWarnDays)
                return;
            Print("REMINDER: cBot running {0:F1} days — restart Learned Autopilot soon (cTrader ~7-day limit).",
                days);
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

            if (_m15Dms.ADX.Last(i) < MinM15Adx)
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
            if (LogStatus)
                Print("Entry {0} @ {1:F2}", side, entry);
        }

        private void ManagePositions()
        {
            foreach (var p in BotPositions())
            {
                int barsHeld = BarsHeldM15(p);
                if (BreakevenAtOneR)
                    TryBreakeven(p);
                if (barsHeld >= MaxHoldBars)
                    ClosePosition(p);
            }
        }

        private int BarsHeldM15(Position p)
        {
            double minutes = Math.Max(0, (Server.Time - p.EntryTime).TotalMinutes);
            return Math.Max(0, (int)(minutes / 15.0));
        }

        private void TryBreakeven(Position p)
        {
            if (!p.StopLoss.HasValue)
                return;
            double entry = p.EntryPrice;
            double risk = Math.Abs(entry - p.StopLoss.Value);
            if (risk <= 0)
                return;
            double oneR = p.TradeType == TradeType.Buy ? entry + risk : entry - risk;
            bool past = p.TradeType == TradeType.Buy ? Symbol.Bid >= oneR : Symbol.Ask <= oneR;
            bool slRisk = p.TradeType == TradeType.Buy ? p.StopLoss.Value < entry : p.StopLoss.Value > entry;
            if (past && slRisk)
                ModifyPosition(p, entry, p.TakeProfit, ProtectionType.Absolute);
        }

        private bool CanEnter()
        {
            if (!_weekTradingAllowed)
                return false;

            if (_h4Dms.ADX.Last(1) < IntraWeekH4AdxPause)
                return false;

            if (_weekStartEquity <= 0)
                return true;

            double dd = (_weekStartEquity - Account.Equity) / _weekStartEquity * 100.0;
            return dd < WeekMaxDrawdownPercent;
        }

        private void LogH4StatusIfNewBar()
        {
            if (!LogStatus || _h4Bars.Count < 2)
                return;
            var t = _h4Bars.OpenTimes.Last(1);
            if (t <= _lastH4Log)
                return;
            _lastH4Log = t;
            double adx = _h4Dms.ADX.Last(1);
            Print("Regime H4 ADX={0:F1} | week={1} | entries={2}",
                adx, _weekTradingAllowed ? "TRADE" : "SKIP", CanEnter() ? "allowed" : "paused");
        }

        private void CloseAll(string why)
        {
            foreach (var p in BotPositions())
                ClosePosition(p);
        }

        private void EnsureWeekRollover()
        {
            int w = GetIsoWeek(Server.Time);
            if (w == _isoWeek)
                return;
            ResetWeek(Server.Time);
        }

        private void ResetWeek(DateTime utc, bool persist = true)
        {
            _isoWeek = GetIsoWeek(utc);
            _weekStartEquity = Account.Equity;
            _activeRiskPercent = FundedPhase ? ChallengeRiskPercent / 2.0 : ChallengeRiskPercent;
            _weekTradingAllowed = true;
            _lastMondayCheck = DateTime.MinValue;
            Print("New week {0} | equity {1:F2} | risk {2:F2}%", _isoWeek, _weekStartEquity, _activeRiskPercent);
            if (persist)
                SaveState();
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
            Print("Monday gate ADX={0:F1} → {1}", adx, _weekTradingAllowed ? "TRADE" : "SKIP");
            SaveState();
        }

        private bool TryRestoreWeekState(DateTime utc)
        {
            if (!PersistState || !File.Exists(StateFilePath))
                return false;

            try
            {
                var lines = File.ReadAllLines(StateFilePath);
                if (lines.Length < 1)
                    return false;

                var parts = lines[0].Split('|');
                if (parts.Length < 5)
                    return false;

                int savedWeek = int.Parse(parts[0], CultureInfo.InvariantCulture);
                int currentWeek = GetIsoWeek(utc);
                if (savedWeek != currentWeek)
                    return false;

                _isoWeek = savedWeek;
                _weekStartEquity = double.Parse(parts[1], CultureInfo.InvariantCulture);
                _weekTradingAllowed = parts[2] == "1";
                long mondayTicks = long.Parse(parts[3], CultureInfo.InvariantCulture);
                if (mondayTicks > 0)
                    _lastMondayCheck = new DateTime(mondayTicks, DateTimeKind.Utc);
                _activeRiskPercent = FundedPhase ? ChallengeRiskPercent / 2.0 : ChallengeRiskPercent;

                Print("Restored week {0} state | week-start equity {1:F2} | Monday gate {2}",
                    _isoWeek, _weekStartEquity, _weekTradingAllowed ? "TRADE" : "SKIP");
                return true;
            }
            catch (Exception ex)
            {
                Print("Could not load state ({0}); starting fresh week.", ex.Message);
                return false;
            }
        }

        private void SaveState()
        {
            if (!PersistState)
                return;

            try
            {
                var dir = Path.GetDirectoryName(StateFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                long mondayTicks = _lastMondayCheck == DateTime.MinValue ? 0 : _lastMondayCheck.ToUniversalTime().Ticks;
                long sessionTicks = _sessionStartedUtc.ToUniversalTime().Ticks;
                var line = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}|{1}|{2}|{3}|{4}",
                    _isoWeek,
                    _weekStartEquity,
                    _weekTradingAllowed ? "1" : "0",
                    mondayTicks,
                    sessionTicks);

                File.WriteAllText(StateFilePath, line);
            }
            catch (Exception ex)
            {
                Print("State save failed: {0}", ex.Message);
            }
        }

        private long VolumeForRisk(double stopDistancePrice)
        {
            double cash = Account.Equity * (_activeRiskPercent / 100.0);
            if (Symbol.TickSize <= 0 || Symbol.TickValue <= 0 || stopDistancePrice <= 0)
                return 0;
            double ticks = stopDistancePrice / Symbol.TickSize;
            return Symbol.NormalizeVolumeInUnits(cash / (ticks * Symbol.TickValue), RoundingMode.Down);
        }

        private int BotCount() => BotPositions().Length;

        private Position[] BotPositions() =>
            Positions.Where(p => p.Label == Label && p.SymbolName == SymbolName).ToArray();

        private static int GetIsoWeek(DateTime utc)
        {
            var cal = CultureInfo.InvariantCulture.Calendar;
            return cal.GetWeekOfYear(utc, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }
    }
}
