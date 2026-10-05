//───────────────────────────────────────────────────────────────────────────────
// cBot: EMA Cross Phone Alert
// Author: Adam Pink
// Purpose: Telegram when this cBot starts, then again when the fast EMA crosses
//          the slow EMA on a closed bar. Each extra block can be switched off
//          when that week's UltimateTrader2026 set is not using it.
//
// Readings use the same rules as Ultimatetrader2026:
//   Double EMA trigger: fast crosses above/below slow, plus the same offset.
//   Volume trend: tick volume > EMA of tick volume.
//   BBWP: width = (upper − lower) / middle, stdev multiplier 1, simple basis.
//         BBWP% = 100 × (count of widths in the lookback window strictly < current) / lookback.
//         Pass = BBWP% > BBWP EMA, inside the lower/upper thresholds, and the
//         expansion rule when that mode is on.
//   ADX momentum: Trending passes when ADX > trending threshold.
//                 Ranging passes when ADX < ranging threshold.
//   ADX trend: long when +DI > −DI and the gap is at least the offset.
//              short when −DI > +DI and the gap is at least the offset.
//
// Defaults match the BTCUSD m15 set BTC_100K:
//   Fast 5 / Slow 21, offset none, entry EMA test of EMA 21
//   Volume EMA 21 on
//   BBWP 20 / lookback 252 / EMA 14, thresholds 0–100, expansion off
//   ADX trending, threshold 18, period 14, m15
//   ADX trend direction off (ADX Trend Mode is disabled in that set)
//   Price EMA and RSI are disabled in that set, so they are not in this alert
//
// Paste the Bot Token and Chat ID into the cBot parameters. They are not stored here.
// Run on desktop. cTrader Cloud blocks Telegram and email.
//───────────────────────────────────────────────────────────────────────────────
using System;
using System.Globalization;
using System.Text;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    public enum AlertEntryType
    {
        OnTrigger,
        EMATest
    }

    public enum AlertOffsetType
    {
        None,
        Pips,
        Percent,
        ATR
    }

    public enum AlertAdxCondition
    {
        Trending,
        Ranging
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class AdxPhoneAlert : Robot
    {
        // ===========================
        // Trigger: two EMAs cross (Double EMA Trend in the trading bot)
        // ===========================
        [Parameter("EMA TimeFrame", Group = "EMA Cross", DefaultValue = "Minute15")]
        public TimeFrame EmaTimeFrame { get; set; }

        [Parameter("Fast EMA Period", Group = "EMA Cross", DefaultValue = 5, MinValue = 1)]
        public int FastEmaPeriod { get; set; }

        [Parameter("Slow EMA Period", Group = "EMA Cross", DefaultValue = 21, MinValue = 1)]
        public int SlowEmaPeriod { get; set; }

        [Parameter("Offset Type", Group = "EMA Cross", DefaultValue = AlertOffsetType.None)]
        public AlertOffsetType EmaOffsetType { get; set; }

        [Parameter("Offset Value", Group = "EMA Cross", DefaultValue = 0.0)]
        public double EmaOffsetValue { get; set; }

        [Parameter("Entry Type", Group = "EMA Cross", DefaultValue = AlertEntryType.EMATest)]
        public AlertEntryType EntryTypeSetting { get; set; }

        [Parameter("Entry EMA Length", Group = "EMA Cross", DefaultValue = 21, MinValue = 1)]
        public int EntryEmaLength { get; set; }

        // ===========================
        // ADX momentum — optional
        // ===========================
        [Parameter("Include ADX", Group = "ADX", DefaultValue = true)]
        public bool IncludeAdx { get; set; }

        [Parameter("ADX TimeFrame", Group = "ADX", DefaultValue = "Minute15")]
        public TimeFrame AdxTimeFrame { get; set; }

        [Parameter("ADX Period", Group = "ADX", DefaultValue = 14, MinValue = 1)]
        public int AdxPeriod { get; set; }

        [Parameter("ADX Condition", Group = "ADX", DefaultValue = AlertAdxCondition.Trending)]
        public AlertAdxCondition AdxCondition { get; set; }

        [Parameter("Trending Threshold", Group = "ADX", DefaultValue = 18, MinValue = 0, MaxValue = 100)]
        public int TrendingThreshold { get; set; }

        [Parameter("Ranging Threshold", Group = "ADX", DefaultValue = 20, MinValue = 0, MaxValue = 100)]
        public int RangingThreshold { get; set; }

        // ===========================
        // ADX trend direction — optional, off when the trading bot's ADX Trend Mode is disabled
        // ===========================
        [Parameter("Include ADX Trend", Group = "ADX Trend", DefaultValue = false)]
        public bool IncludeAdxDirection { get; set; }

        [Parameter("ADX Trend TimeFrame", Group = "ADX Trend", DefaultValue = "Minute15")]
        public TimeFrame AdxTrendTimeFrame { get; set; }

        [Parameter("ADX Trend Period", Group = "ADX Trend", DefaultValue = 14, MinValue = 1)]
        public int AdxTrendPeriod { get; set; }

        [Parameter("ADX Trend Offset", Group = "ADX Trend", DefaultValue = 0, MinValue = 0)]
        public int AdxTrendOffset { get; set; }

        // ===========================
        // BBWP — optional
        // ===========================
        [Parameter("Include BBWP", Group = "BBWP", DefaultValue = true)]
        public bool IncludeBbwp { get; set; }

        [Parameter("BBWP TimeFrame", Group = "BBWP", DefaultValue = "Minute15")]
        public TimeFrame BbwpTimeFrame { get; set; }

        [Parameter("BB Period", Group = "BBWP", DefaultValue = 20, MinValue = 2)]
        public int BbPeriod { get; set; }

        [Parameter("BB StdDev", Group = "BBWP", DefaultValue = 1.0, MinValue = 0.1)]
        public double BbStdDev { get; set; }

        [Parameter("Basis MA", Group = "BBWP", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType BbBasisType { get; set; }

        [Parameter("Lookback", Group = "BBWP", DefaultValue = 252, MinValue = 1)]
        public int BbwpLookback { get; set; }

        [Parameter("BBWP EMA Period", Group = "BBWP", DefaultValue = 14, MinValue = 1)]
        public int BbwpEmaPeriod { get; set; }

        [Parameter("Upper BBWP Threshold", Group = "BBWP", DefaultValue = 100, MinValue = 0, MaxValue = 100)]
        public int UpperBbwpThreshold { get; set; }

        [Parameter("Lower BBWP Threshold", Group = "BBWP", DefaultValue = 0, MinValue = 0, MaxValue = 100)]
        public int LowerBbwpThreshold { get; set; }

        [Parameter("BBWP Expansion", Group = "BBWP", DefaultValue = false)]
        public bool UseBbwpExpansion { get; set; }

        [Parameter("Expansion Lookback", Group = "BBWP", DefaultValue = 20, MinValue = 1)]
        public int BbwpExpansionLookback { get; set; }

        [Parameter("Expansion Threshold", Group = "BBWP", DefaultValue = 60, MinValue = 0, MaxValue = 100)]
        public int BbwpExpansionThreshold { get; set; }

        // ===========================
        // Volume trend vs its EMA — optional
        // ===========================
        [Parameter("Include Volume Trend", Group = "Volume Trend", DefaultValue = true)]
        public bool IncludeVolumeTrend { get; set; }

        [Parameter("Volume TimeFrame", Group = "Volume Trend", DefaultValue = "Minute15")]
        public TimeFrame VolumeTimeFrame { get; set; }

        [Parameter("Volume EMA Period", Group = "Volume Trend", DefaultValue = 21, MinValue = 1)]
        public int VolumeEmaPeriod { get; set; }

        // ===========================
        // Telegram
        // ===========================
        [Parameter("Enable Telegram", Group = "Telegram", DefaultValue = true)]
        public bool EnableTelegram { get; set; }

        [Parameter("Bot Token", Group = "Telegram", DefaultValue = "")]
        public string TelegramBotToken { get; set; }

        [Parameter("Chat ID", Group = "Telegram", DefaultValue = "")]
        public string TelegramChatId { get; set; }

        // ===========================
        // Email
        // ===========================
        [Parameter("Enable Email", Group = "Email", DefaultValue = false)]
        public bool EnableEmail { get; set; }

        [Parameter("From Email", Group = "Email", DefaultValue = "")]
        public string EmailFrom { get; set; }

        [Parameter("To Email", Group = "Email", DefaultValue = "")]
        public string EmailTo { get; set; }

        private Bars _crossBars;
        private ExponentialMovingAverage _fastEma;
        private ExponentialMovingAverage _slowEma;
        private AverageTrueRange _atr;

        private Bars _adxBars;
        private DirectionalMovementSystem _dms;

        private Bars _adxTrendBars;
        private DirectionalMovementSystem _dmsTrend;

        private Bars _bbwpBars;
        private BollingerBands _bb;

        private Bars _volumeBars;
        private ExponentialMovingAverage _volumeEma;

        private DateTime _lastEvaluatedOpenTime = DateTime.MinValue;

        protected override void OnStart()
        {
            Print("*** EMA Cross Phone Alert BUILD 2026-10-05-v9 ***");
            Print("Symbol={0} | Cross TF={1} | Fast EMA={2} | Slow EMA={3} | Offset={4} {5}",
                SymbolName, EmaTimeFrame, FastEmaPeriod, SlowEmaPeriod, EmaOffsetType, EmaOffsetValue);
            Print("Sections: ADX={0} | ADX trend={1} | BBWP={2} | Volume={3}",
                OnOff(IncludeAdx), OnOff(IncludeAdxDirection), OnOff(IncludeBbwp), OnOff(IncludeVolumeTrend));
            Print("Entry={0} | Entry EMA={1}", EntryTypeSetting, EntryEmaLength);

            if (FastEmaPeriod == SlowEmaPeriod && EmaOffsetType == AlertOffsetType.None)
                Print("WARNING: Fast EMA and Slow EMA use the same period — a cross may never print.");

            if (IncludeAdx)
            {
                Print("ADX {0}: period {1} on {2}. Trending passes above {3}. Ranging passes below {4}.",
                    AdxCondition, AdxPeriod, AdxTimeFrame, TrendingThreshold, RangingThreshold);
            }
            if (IncludeAdxDirection)
                Print("ADX trend: period {0} on {1}, offset {2}.", AdxTrendPeriod, AdxTrendTimeFrame, AdxTrendOffset);
            if (IncludeBbwp)
            {
                Print("BBWP: BB({0}, {1:0.##}, {2}) lookback {3} | EMA({4}) on {5}. Pass when BBWP% > EMA, inside {6}–{7}. Expansion={8}.",
                    BbPeriod, BbStdDev, BbBasisType, BbwpLookback, BbwpEmaPeriod, BbwpTimeFrame,
                    LowerBbwpThreshold, UpperBbwpThreshold, OnOff(UseBbwpExpansion));
            }
            if (IncludeVolumeTrend)
                Print("Volume trend: tick volume must be above EMA({0}) on {1}.", VolumeEmaPeriod, VolumeTimeFrame);

            if (EnableTelegram && (string.IsNullOrWhiteSpace(TelegramBotToken) || string.IsNullOrWhiteSpace(TelegramChatId)))
                Print("WARNING: Telegram enabled but Bot Token / Chat ID is empty — Telegram alerts disabled.");

            if (EnableEmail && (string.IsNullOrWhiteSpace(EmailFrom) || string.IsNullOrWhiteSpace(EmailTo)))
                Print("WARNING: Email enabled but From/To is empty — Email alerts disabled.");

            Print("REMINDER: Run this cBot on DESKTOP (not Cloud) for Telegram/Email to reach your phone.");
            Print("A Telegram message is sent on start, then when the fast EMA crosses the slow EMA on a closed bar.");

            _crossBars = GetBars(EmaTimeFrame);
            _fastEma = Indicators.ExponentialMovingAverage(_crossBars.ClosePrices, FastEmaPeriod);
            _slowEma = Indicators.ExponentialMovingAverage(_crossBars.ClosePrices, SlowEmaPeriod);
            if (EmaOffsetType == AlertOffsetType.ATR)
                _atr = Indicators.AverageTrueRange(_crossBars, 14, MovingAverageType.Simple);
            _crossBars.BarClosed += OnCrossBarClosed;

            if (IncludeAdx)
            {
                _adxBars = GetBars(AdxTimeFrame);
                _dms = Indicators.DirectionalMovementSystem(_adxBars, AdxPeriod);
            }

            if (IncludeAdxDirection)
            {
                _adxTrendBars = GetBars(AdxTrendTimeFrame);
                if (_dms != null && AdxTrendTimeFrame == AdxTimeFrame && AdxTrendPeriod == AdxPeriod)
                    _dmsTrend = _dms;
                else
                    _dmsTrend = Indicators.DirectionalMovementSystem(_adxTrendBars, AdxTrendPeriod);
            }

            if (IncludeBbwp)
            {
                _bbwpBars = GetBars(BbwpTimeFrame);
                _bb = Indicators.BollingerBands(_bbwpBars.ClosePrices, BbPeriod, BbStdDev, BbBasisType);
            }

            if (IncludeVolumeTrend)
            {
                _volumeBars = GetBars(VolumeTimeFrame);
                _volumeEma = Indicators.ExponentialMovingAverage(_volumeBars.TickVolumes, VolumeEmaPeriod);
            }

            ArmCrossWatch();
            SendStartupAlert();
            Timer.Start(15);
        }

        protected override void OnTimer()
        {
            CheckForNewCrossBars();
        }

        protected override void OnBar()
        {
            CheckForNewCrossBars();
        }

        protected override void OnStop()
        {
            Timer.Stop();
            if (_crossBars != null)
                _crossBars.BarClosed -= OnCrossBarClosed;
            Print("EMA Cross Phone Alert stopped.");
        }

        private void OnCrossBarClosed(BarClosedEventArgs args)
        {
            CheckForNewCrossBars();
        }

        private Bars GetBars(TimeFrame timeFrame)
        {
            if (_crossBars != null && timeFrame == EmaTimeFrame)
                return _crossBars;
            if (timeFrame == Bars.TimeFrame)
                return Bars;
            return MarketData.GetBars(timeFrame);
        }

        private void ArmCrossWatch()
        {
            if (_crossBars == null || _crossBars.Count < 3)
                return;

            int closed = _crossBars.Count - 2;
            _lastEvaluatedOpenTime = _crossBars.OpenTimes[closed];
            Print("Armed at bar {0:dd/MM/yyyy HH:mm} UTC. Crosses on this bar and earlier are ignored.",
                _lastEvaluatedOpenTime);
        }

        private void CheckForNewCrossBars()
        {
            if (_crossBars == null || _fastEma == null || _slowEma == null)
                return;

            int minIndex = Math.Max(FastEmaPeriod, SlowEmaPeriod);
            if (_crossBars.Count < minIndex + 3)
                return;

            int closed = _crossBars.Count - 2;
            if (closed < minIndex)
                return;

            DateTime closedTime = _crossBars.OpenTimes[closed];
            if (_lastEvaluatedOpenTime == DateTime.MinValue)
            {
                _lastEvaluatedOpenTime = closedTime;
                return;
            }

            if (closedTime <= _lastEvaluatedOpenTime)
                return;

            int from = closed;
            while (from > minIndex && _crossBars.OpenTimes[from - 1] > _lastEvaluatedOpenTime)
                from--;

            _lastEvaluatedOpenTime = closedTime;

            for (int i = from; i <= closed; i++)
            {
                string side;
                if (TryGetCross(i, out side))
                    SendCrossAlert(i, side);
            }
        }

        private bool TryGetCross(int index, out string side)
        {
            side = null;
            if (index < 1)
                return false;

            if (!EmaValuesReady(index) || !EmaValuesReady(index - 1))
                return false;

            bool longNow = IsLongAligned(index);
            if (!IsLongAligned(index - 1) && longNow)
            {
                side = "LONG";
                return true;
            }

            if (!IsShortAligned(index - 1) && IsShortAligned(index))
            {
                side = "SHORT";
                return true;
            }

            return false;
        }

        private bool EmaValuesReady(int index)
        {
            if (index < 0)
                return false;
            return IsFinite(_fastEma.Result[index]) && IsFinite(_slowEma.Result[index]);
        }

        private bool IsLongAligned(int index)
        {
            if (!EmaValuesReady(index))
                return false;
            double slow = _slowEma.Result[index];
            return _fastEma.Result[index] > slow + SlowOffset(slow, index);
        }

        private bool IsShortAligned(int index)
        {
            if (!EmaValuesReady(index))
                return false;
            double slow = _slowEma.Result[index];
            return _fastEma.Result[index] < slow - SlowOffset(slow, index);
        }

        private double SlowOffset(double slow, int index)
        {
            if (EmaOffsetType == AlertOffsetType.Pips)
                return EmaOffsetValue * Symbol.PipSize;
            if (EmaOffsetType == AlertOffsetType.Percent)
                return slow * (EmaOffsetValue / 100.0);
            if (EmaOffsetType == AlertOffsetType.ATR)
            {
                if (_atr == null || index < 0 || !IsFinite(_atr.Result[index]))
                    return 0;
                return _atr.Result[index] * EmaOffsetValue;
            }
            return 0;
        }

        private void SendCrossAlert(int index, string side)
        {
            double fast = _fastEma.Result[index];
            double slow = _slowEma.Result[index];
            double close = _crossBars.ClosePrices[index];
            string crossed = side == "LONG" ? "ABOVE" : "BELOW";

            string adxTag = null;
            string directionTag = null;
            string bbwpTag = null;
            string volumeTag = null;

            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "EMA Cross Alert | {0} | TF {1}\n", SymbolName, EmaTimeFrame);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Bar: {0:dd/MM/yyyy HH:mm} UTC\n", _crossBars.OpenTimes[index]);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Cross: {0} — Fast EMA({1}) crossed {2} Slow EMA({3})\n",
                side, FastEmaPeriod, crossed, SlowEmaPeriod);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Fast EMA = {0} | Slow EMA = {1} | close = {2}\n",
                FormatPrice(fast), FormatPrice(slow), FormatPrice(close));
            AppendEntryNote(sb);

            if (IncludeAdx)
                AppendAdx(sb, ref adxTag);
            if (IncludeAdxDirection)
                AppendAdxTrend(sb, side, ref directionTag);
            if (IncludeBbwp)
                AppendBbwp(sb, ref bbwpTag);
            if (IncludeVolumeTrend)
                AppendVolume(sb, ref volumeTag);
            AppendFilterSummary(sb, side, adxTag, directionTag, bbwpTag, volumeTag);

            var subject = new StringBuilder();
            subject.AppendFormat(CultureInfo.InvariantCulture,
                "EMA {0} cross {1} {2}", side, SymbolName, EmaTimeFrame);
            AppendSubjectTag(subject, adxTag);
            AppendSubjectTag(subject, directionTag);
            AppendSubjectTag(subject, bbwpTag);
            AppendSubjectTag(subject, volumeTag);

            Deliver(sb.ToString().TrimEnd(), subject.ToString());
        }

        private void SendStartupAlert()
        {
            string adxTag = null;
            string directionTag = null;
            string bbwpTag = null;
            string volumeTag = null;

            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "EMA Cross Alert STARTED | {0} | TF {1}\n", SymbolName, EmaTimeFrame);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Watching Fast EMA({0}) and Slow EMA({1})\n", FastEmaPeriod, SlowEmaPeriod);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Sections: ADX {0} | ADX trend {1} | BBWP {2} | Volume {3}\n",
                OnOff(IncludeAdx), OnOff(IncludeAdxDirection), OnOff(IncludeBbwp), OnOff(IncludeVolumeTrend));
            AppendEntryNote(sb);

            if (_crossBars != null && _fastEma != null && _slowEma != null && _crossBars.Count >= 3)
            {
                int closed = _crossBars.Count - 2;
                if (EmaValuesReady(closed))
                {
                    string position = IsLongAligned(closed) ? "ABOVE"
                        : IsShortAligned(closed) ? "BELOW"
                        : EmaOffsetType == AlertOffsetType.None ? "EQUAL TO" : "INSIDE THE OFFSET";
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "Now: Fast EMA is {0} Slow EMA | Fast {1} | Slow {2} | close {3}\n",
                        position,
                        FormatPrice(_fastEma.Result[closed]),
                        FormatPrice(_slowEma.Result[closed]),
                        FormatPrice(_crossBars.ClosePrices[closed]));
                }
            }

            sb.Append("Next alert when the fast EMA crosses the slow EMA on a closed bar.\n");

            if (IncludeAdx)
                AppendAdx(sb, ref adxTag);
            if (IncludeAdxDirection)
                AppendAdxTrend(sb, null, ref directionTag);
            if (IncludeBbwp)
                AppendBbwp(sb, ref bbwpTag);
            if (IncludeVolumeTrend)
                AppendVolume(sb, ref volumeTag);
            AppendFilterSummary(sb, null, adxTag, directionTag, bbwpTag, volumeTag);

            var subject = new StringBuilder();
            subject.AppendFormat(CultureInfo.InvariantCulture, "EMA alert started {0} {1}", SymbolName, EmaTimeFrame);
            AppendSubjectTag(subject, adxTag);
            AppendSubjectTag(subject, directionTag);
            AppendSubjectTag(subject, bbwpTag);
            AppendSubjectTag(subject, volumeTag);

            Deliver(sb.ToString().TrimEnd(), subject.ToString());
        }

        private void AppendEntryNote(StringBuilder sb)
        {
            if (EntryTypeSetting == AlertEntryType.EMATest)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "Bot entry: EMA test of EMA({0}). A passing cross arms the trade. The fill is a later bar that wicks through that EMA.\n",
                    EntryEmaLength);
            }
            else
            {
                sb.Append("Bot entry: market entry on the cross when the filters pass.\n");
            }
        }

        private void AppendFilterSummary(StringBuilder sb, string side, string adxTag, string directionTag, string bbwpTag, string volumeTag)
        {
            bool any = false;
            bool fail = false;
            bool waiting = false;

            NoteFilter(adxTag, ref any, ref fail, ref waiting);
            NoteFilter(bbwpTag, ref any, ref fail, ref waiting);
            NoteFilter(volumeTag, ref any, ref fail, ref waiting);

            if (directionTag != null)
            {
                any = true;
                if (directionTag == "WAIT")
                    waiting = true;
                else if (side == null)
                {
                    if (directionTag == "FLAT")
                        fail = true;
                }
                else if (directionTag != side)
                {
                    fail = true;
                }
            }

            if (!any)
                return;

            if (waiting)
                sb.Append("Strategy filters: NOT READY\n");
            else if (fail)
                sb.Append("Strategy filters: FAIL\n");
            else
                sb.Append("Strategy filters: PASS\n");
        }

        private static void NoteFilter(string tag, ref bool any, ref bool fail, ref bool waiting)
        {
            if (tag == null)
                return;
            any = true;
            if (tag == "FAIL")
                fail = true;
            if (tag == "WAIT")
                waiting = true;
        }

        private static void AppendSubjectTag(StringBuilder subject, string tag)
        {
            if (!string.IsNullOrEmpty(tag))
                subject.Append(" | ").Append(tag);
        }

        private void Deliver(string message, string emailSubject)
        {
            Print(message.Replace("\n", " | "));

            if (EnableTelegram
                && !string.IsNullOrWhiteSpace(TelegramBotToken)
                && !string.IsNullOrWhiteSpace(TelegramChatId))
            {
                SendTelegram(message);
            }

            if (EnableEmail
                && !string.IsNullOrWhiteSpace(EmailFrom)
                && !string.IsNullOrWhiteSpace(EmailTo))
            {
                try
                {
                    Notifications.SendEmail(EmailFrom, EmailTo, emailSubject, message);
                    Print("Email alert sent to {0}", EmailTo);
                }
                catch (Exception ex)
                {
                    Print("Email alert failed: {0}", ex.Message);
                }
            }
        }

        private void AppendAdx(StringBuilder sb, ref string adxTag)
        {
            if (_dms == null || _adxBars == null || _adxBars.Count < AdxPeriod + 2)
            {
                sb.Append("ADX: not ready (need more bars)\n");
                adxTag = "WAIT";
                return;
            }

            int closed = _adxBars.Count - 2;
            double adx = _dms.ADX[closed];
            if (!IsFinite(adx))
            {
                sb.Append("ADX: not ready (need more bars)\n");
                adxTag = "WAIT";
                return;
            }

            bool pass;
            string versus;
            int level;
            if (AdxCondition == AlertAdxCondition.Trending)
            {
                pass = adx > TrendingThreshold;
                versus = pass ? "ABOVE" : "NOT ABOVE";
                level = TrendingThreshold;
            }
            else
            {
                pass = adx < RangingThreshold;
                versus = pass ? "BELOW" : "NOT BELOW";
                level = RangingThreshold;
            }

            adxTag = pass ? "PASS" : "FAIL";
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "ADX({0}) {1} = {2:0.00} — {3} — {4} {5} — {6}\n",
                AdxPeriod, AdxTimeFrame, adx, AdxCondition, versus, level, adxTag);
        }

        private void AppendAdxTrend(StringBuilder sb, string crossSide, ref string directionTag)
        {
            if (_dmsTrend == null || _adxTrendBars == null || _adxTrendBars.Count < AdxTrendPeriod + 2)
            {
                sb.Append("ADX trend: not ready (need more bars)\n");
                directionTag = "WAIT";
                return;
            }

            int closed = _adxTrendBars.Count - 2;
            double diPlus = _dmsTrend.DIPlus[closed];
            double diMinus = _dmsTrend.DIMinus[closed];
            if (!IsFinite(diPlus) || !IsFinite(diMinus))
            {
                sb.Append("ADX trend: not ready (need more bars)\n");
                directionTag = "WAIT";
                return;
            }

            double gap = Math.Abs(diPlus - diMinus);
            string direction;
            if (gap < AdxTrendOffset)
                direction = "FLAT";
            else if (diPlus > diMinus)
                direction = "LONG";
            else if (diMinus > diPlus)
                direction = "SHORT";
            else
                direction = "FLAT";

            directionTag = direction;
            string result = "PASS";
            if (direction == "FLAT")
                result = "FAIL";
            else if (crossSide != null && direction != crossSide)
                result = "FAIL";

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "ADX trend: {0} ({1} | +DI {2:0.00} | −DI {3:0.00} | gap {4:0.00} | offset {5}) — {6}\n",
                direction,
                direction == "LONG" ? "+DI > −DI" : direction == "SHORT" ? "−DI > +DI" : "gap below offset",
                diPlus, diMinus, gap, AdxTrendOffset, result);
        }

        private void AppendBbwp(StringBuilder sb, ref string bbwpTag)
        {
            if (_bb == null || _bbwpBars == null || _bbwpBars.Count < 3)
            {
                sb.Append("BBWP: not ready (need more bars)\n");
                bbwpTag = "WAIT";
                return;
            }

            int closed = _bbwpBars.Count - 2;
            double bbwp;
            double bbwpEma;
            bool hasEma;
            bool expansionOk;
            if (!TryComputeBbwp(closed, out bbwp, out bbwpEma, out hasEma, out expansionOk))
            {
                sb.Append("BBWP: not ready (need more bars)\n");
                bbwpTag = "WAIT";
                return;
            }

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "BBWP({0}, {1:0.##}, lookback {2}) {3} = {4:0.00}%",
                BbPeriod, BbStdDev, BbwpLookback, BbwpTimeFrame, bbwp);

            if (!hasEma)
            {
                sb.Append(" | EMA not ready\n");
                bbwpTag = "WAIT";
                return;
            }

            bool aboveEma = bbwp > bbwpEma;
            bool inside = bbwp <= UpperBbwpThreshold && bbwp >= LowerBbwpThreshold;
            bool pass = aboveEma && inside && expansionOk;
            bbwpTag = pass ? "PASS" : "FAIL";

            string relation = aboveEma
                ? "BBWP% is ABOVE the EMA"
                : bbwp < bbwpEma
                    ? "BBWP% is BELOW the EMA"
                    : "BBWP% is EQUAL to the EMA";

            sb.AppendFormat(CultureInfo.InvariantCulture,
                " | EMA({0}) = {1:0.00}\n{2}",
                BbwpEmaPeriod, bbwpEma, relation);

            if (!inside)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    " | outside {0}–{1}", LowerBbwpThreshold, UpperBbwpThreshold);
            }
            if (UseBbwpExpansion && !expansionOk)
                sb.Append(" | expansion not met");

            sb.Append(" — ").Append(bbwpTag).Append('\n');
        }

        private void AppendVolume(StringBuilder sb, ref string volumeTag)
        {
            if (_volumeEma == null || _volumeBars == null || _volumeBars.Count < VolumeEmaPeriod + 2)
            {
                sb.Append("Volume trend: not ready (need more bars)\n");
                volumeTag = "WAIT";
                return;
            }

            int closed = _volumeBars.Count - 2;
            if (closed < VolumeEmaPeriod)
            {
                sb.Append("Volume trend: not ready (need more bars)\n");
                volumeTag = "WAIT";
                return;
            }

            double volume = _volumeBars.TickVolumes[closed];
            double ema = _volumeEma.Result[closed];
            if (!IsFinite(volume) || !IsFinite(ema))
            {
                sb.Append("Volume trend: not ready (need more bars)\n");
                volumeTag = "WAIT";
                return;
            }

            string relation;
            if (volume > ema)
            {
                relation = "ABOVE";
                volumeTag = "PASS";
            }
            else if (volume < ema)
            {
                relation = "BELOW";
                volumeTag = "FAIL";
            }
            else
            {
                relation = "EQUAL TO";
                volumeTag = "FAIL";
            }

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Volume trend {0}: {1} the EMA({2}) — {3} | volume {4} | EMA {5}\n",
                VolumeTimeFrame,
                relation,
                VolumeEmaPeriod,
                volumeTag,
                volume.ToString("0.##", CultureInfo.InvariantCulture),
                ema.ToString("0.##", CultureInfo.InvariantCulture));
        }

        private bool TryComputeBbwp(int closedIndex, out double bbwp, out double bbwpEma, out bool hasEma, out bool expansionOk)
        {
            bbwp = 0;
            bbwpEma = 0;
            hasEma = false;
            expansionOk = !UseBbwpExpansion;

            if (closedIndex < BbPeriod - 1)
                return false;

            var width = new double[closedIndex + 1];
            var bbwpAt = new double[closedIndex + 1];
            for (int i = 0; i <= closedIndex; i++)
            {
                width[i] = BandWidth(i);
                bbwpAt[i] = double.NaN;
            }

            int first = -1;
            for (int i = 0; i <= closedIndex; i++)
            {
                if (!IsFinite(width[i]))
                    continue;
                bbwpAt[i] = BotBbwp(width, i);
                if (first < 0)
                    first = i;
            }

            if (first < 0 || !IsFinite(bbwpAt[closedIndex]))
                return false;

            int length = 0;
            for (int i = first; i <= closedIndex; i++)
            {
                if (IsFinite(bbwpAt[i]))
                    length++;
            }

            var series = new double[length];
            int n = 0;
            for (int i = first; i <= closedIndex; i++)
            {
                if (IsFinite(bbwpAt[i]))
                    series[n++] = bbwpAt[i];
            }

            bbwp = bbwpAt[closedIndex];
            if (length < BbwpEmaPeriod)
                return true;

            bbwpEma = ExponentialAverage(series, BbwpEmaPeriod);
            hasEma = IsFinite(bbwpEma);
            if (UseBbwpExpansion)
                expansionOk = ExpansionMet(bbwpAt, closedIndex);
            return true;
        }

        private bool ExpansionMet(double[] bbwpAt, int closedIndex)
        {
            int start = closedIndex - BbwpExpansionLookback + 1;
            if (start < 0)
                start = 0;

            for (int i = start; i <= closedIndex; i++)
            {
                if (IsFinite(bbwpAt[i]) && bbwpAt[i] <= BbwpExpansionThreshold)
                    return true;
            }

            return false;
        }

        private double BandWidth(int index)
        {
            if (index < BbPeriod - 1)
                return double.NaN;

            double basis = _bb.Main[index];
            double upper = _bb.Top[index];
            double lower = _bb.Bottom[index];
            if (!IsFinite(basis) || !IsFinite(upper) || !IsFinite(lower) || basis == 0)
                return double.NaN;

            return (upper - lower) / basis;
        }

        /// <summary>
        /// Same percentile as Ultimatetrader2026: count widths in the lookback window
        /// that are strictly below the current width, including the current bar, then
        /// divide by the full lookback.
        /// </summary>
        private double BotBbwp(double[] width, int index)
        {
            double current = width[index];
            if (!IsFinite(current))
                return double.NaN;

            int start = index - BbwpLookback + 1;
            if (start < 0)
                start = 0;

            int countBelow = 0;
            for (int i = start; i <= index; i++)
            {
                if (IsFinite(width[i]) && width[i] < current)
                    countBelow++;
            }

            return 100.0 * countBelow / BbwpLookback;
        }

        private static double ExponentialAverage(double[] values, int period)
        {
            if (values == null || period < 1 || values.Length < period)
                return double.NaN;

            double sum = 0;
            for (int i = 0; i < period; i++)
            {
                if (!IsFinite(values[i]))
                    return double.NaN;
                sum += values[i];
            }

            double ema = sum / period;
            double k = 2.0 / (period + 1.0);
            for (int i = period; i < values.Length; i++)
            {
                if (!IsFinite(values[i]))
                    return double.NaN;
                ema = (values[i] * k) + (ema * (1.0 - k));
            }

            return ema;
        }

        private string FormatPrice(double value)
        {
            int digits = Symbol.Digits;
            if (digits < 0)
                digits = 0;
            return value.ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static string OnOff(bool enabled)
        {
            return enabled ? "ON" : "OFF";
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void SendTelegram(string text)
        {
            try
            {
                string colored = ToColoredTelegramHtml(text);
                string encoded = Uri.EscapeDataString(colored);
                string url = string.Format(
                    "https://api.telegram.org/bot{0}/sendMessage?chat_id={1}&parse_mode=HTML&text={2}",
                    TelegramBotToken.Trim(),
                    TelegramChatId.Trim(),
                    encoded);

                var response = Http.Get(url);
                if (response == null || !response.IsSuccessful)
                {
                    string detail = response == null ? "null response" : ("status=" + response.StatusCode);
                    Print("Telegram failed: {0}", detail);
                    return;
                }

                Print("Telegram alert sent.");
            }
            catch (Exception ex)
            {
                Print("Telegram threw: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Telegram bot text cannot set a font color. A diff code block renders
        /// lines that start with + in green and lines that start with - in red.
        /// </summary>
        private static string ToColoredTelegramHtml(string text)
        {
            var sb = new StringBuilder();
            var diff = new StringBuilder();
            string normalized = text.Replace("\r", "");
            string[] lines = normalized.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string kind = IndicatorLineColor(lines[i]);
                if (kind == null)
                {
                    FlushDiffBlock(sb, diff);
                    if (lines[i].Length > 0)
                        sb.Append(HtmlEscape(lines[i]));
                    if (i < lines.Length - 1)
                        sb.Append('\n');
                }
                else
                {
                    diff.Append(kind == "green" ? '+' : '-');
                    diff.Append(' ');
                    diff.Append(lines[i]);
                    diff.Append('\n');
                }
            }

            FlushDiffBlock(sb, diff);
            return sb.ToString().TrimEnd();
        }

        private static string IndicatorLineColor(string line)
        {
            if (line.IndexOf(" — PASS") >= 0 || line == "Strategy filters: PASS")
                return "green";
            if (line.IndexOf(" — FAIL") >= 0 || line == "Strategy filters: FAIL")
                return "red";
            return null;
        }

        private static void FlushDiffBlock(StringBuilder sb, StringBuilder diff)
        {
            if (diff.Length == 0)
                return;

            sb.Append("<pre><code class=\"language-diff\">");
            sb.Append(HtmlEscape(diff.ToString().TrimEnd()));
            sb.Append("</code></pre>\n");
            diff.Length = 0;
        }

        private static string HtmlEscape(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
