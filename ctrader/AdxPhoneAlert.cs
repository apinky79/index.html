//───────────────────────────────────────────────────────────────────────────────
// cBot: EMA Cross Phone Alert
// Author: Adam Pink
// Purpose: Send a Telegram message when the cBot starts, then again when two
//          selected EMAs cross on a closed bar. The message can include:
//            • ADX ranging or trending, above or below the selected level
//            • ADX direction: long or short (+DI vs −DI)
//            • whether the BBWP EMA is above the BBWP% value
//            • whether tick volume is above or below its selected EMA
//          Turn each block off when that week's bot is not using it.
//          The EMA cross itself is the trigger and always runs.
// Phone delivery: Telegram. Journal always logged.
// NOTE: Outbound alerts need a DESKTOP/local instance — cTrader Cloud blocks
//       HTTP and email. Attach this cBot to any chart; the cross uses the
//       timeframe you pick, not necessarily the chart timeframe.
//
// BBWP (Bollinger Band Width Percentile), same idea as TradingView ta.percentrank:
//   band width = (upper − lower) / middle
//   BBWP%      = 100 × (how many of the previous Lookback widths are <= current) / Lookback
//   "BBWP EMA is ABOVE the BBWP% value" means the EMA of that series is greater
//   than the current BBWP%.
// Paste the Bot Token and Chat ID into the cBot parameters. They are not stored here.
//───────────────────────────────────────────────────────────────────────────────
using System;
using System.Globalization;
using System.Text;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    // AccessRights.None — Http.Get still works on desktop for Telegram (.NET 6+).
    // Cloud blocks outbound HTTP/email anyway.
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class AdxPhoneAlert : Robot
    {
        // ===========================
        // Trigger: two EMAs cross
        // ===========================
        [Parameter("EMA TimeFrame", Group = "EMA Cross", DefaultValue = "Hour")]
        public TimeFrame EmaTimeFrame { get; set; }

        [Parameter("Fast EMA Period", Group = "EMA Cross", DefaultValue = 20, MinValue = 1)]
        public int FastEmaPeriod { get; set; }

        [Parameter("Slow EMA Period", Group = "EMA Cross", DefaultValue = 50, MinValue = 1)]
        public int SlowEmaPeriod { get; set; }

        // ===========================
        // ADX level (ranging / trending) — optional
        // ===========================
        [Parameter("Include ADX", Group = "ADX", DefaultValue = true)]
        public bool IncludeAdx { get; set; }

        [Parameter("ADX TimeFrame", Group = "ADX", DefaultValue = "Hour")]
        public TimeFrame AdxTimeFrame { get; set; }

        [Parameter("ADX Period", Group = "ADX", DefaultValue = 14, MinValue = 1)]
        public int AdxPeriod { get; set; }

        [Parameter("ADX Level", Group = "ADX", DefaultValue = 25.0, MinValue = 0.0, MaxValue = 100.0)]
        public double AdxLevel { get; set; }

        [Parameter("Include Long / Short", Group = "ADX", DefaultValue = true)]
        public bool IncludeAdxDirection { get; set; }

        // ===========================
        // BBWP — optional
        // ===========================
        [Parameter("Include BBWP", Group = "BBWP", DefaultValue = true)]
        public bool IncludeBbwp { get; set; }

        [Parameter("BBWP TimeFrame", Group = "BBWP", DefaultValue = "Hour")]
        public TimeFrame BbwpTimeFrame { get; set; }

        [Parameter("BB Period", Group = "BBWP", DefaultValue = 20, MinValue = 2)]
        public int BbPeriod { get; set; }

        [Parameter("BB StdDev", Group = "BBWP", DefaultValue = 2.0, MinValue = 0.1)]
        public double BbStdDev { get; set; }

        [Parameter("Basis MA", Group = "BBWP", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType BbBasisType { get; set; }

        [Parameter("Lookback", Group = "BBWP", DefaultValue = 100, MinValue = 1)]
        public int BbwpLookback { get; set; }

        [Parameter("BBWP EMA Period", Group = "BBWP", DefaultValue = 20, MinValue = 1)]
        public int BbwpEmaPeriod { get; set; }

        // ===========================
        // Volume trend vs its EMA — optional
        // ===========================
        [Parameter("Include Volume Trend", Group = "Volume Trend", DefaultValue = true)]
        public bool IncludeVolumeTrend { get; set; }

        [Parameter("Volume TimeFrame", Group = "Volume Trend", DefaultValue = "Hour")]
        public TimeFrame VolumeTimeFrame { get; set; }

        [Parameter("Volume EMA Period", Group = "Volume Trend", DefaultValue = 20, MinValue = 1)]
        public int VolumeEmaPeriod { get; set; }

        // ===========================
        // Telegram (phone message)
        // ===========================
        [Parameter("Enable Telegram", Group = "Telegram", DefaultValue = true)]
        public bool EnableTelegram { get; set; }

        // Create a bot via @BotFather, then get the chat id via @userinfobot or getUpdates.
        // Enter both on the cBot instance. Leave them blank in source.
        [Parameter("Bot Token", Group = "Telegram", DefaultValue = "")]
        public string TelegramBotToken { get; set; }

        [Parameter("Chat ID", Group = "Telegram", DefaultValue = "")]
        public string TelegramChatId { get; set; }

        // ===========================
        // Email (optional phone push via mail app)
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

        private Bars _adxBars;
        private DirectionalMovementSystem _dms;

        private Bars _bbwpBars;
        private BollingerBands _bb;

        private Bars _volumeBars;
        private ExponentialMovingAverage _volumeEma;

        private DateTime _lastEvaluatedOpenTime = DateTime.MinValue;

        protected override void OnStart()
        {
            Print("*** EMA Cross Phone Alert BUILD 2026-10-04-v7 ***");
            Print("Symbol={0} | Cross TF={1} | Fast EMA={2} | Slow EMA={3}",
                SymbolName, EmaTimeFrame, FastEmaPeriod, SlowEmaPeriod);
            Print("Sections: ADX={0} | Long/Short={1} | BBWP={2} | Volume trend={3}",
                OnOff(IncludeAdx), OnOff(IncludeAdxDirection), OnOff(IncludeBbwp), OnOff(IncludeVolumeTrend));

            if (FastEmaPeriod == SlowEmaPeriod)
                Print("WARNING: Fast EMA and Slow EMA use the same period — a cross may never print.");

            if (IncludeAdx)
                Print("ADX level: {0:0.##} on {1}. Above = TRENDING, below = RANGING.", AdxLevel, AdxTimeFrame);
            if (IncludeBbwp)
            {
                Print("BBWP: BB({0}, {1:0.##}, {2}) lookback {3} | EMA({4}) on {5}.",
                    BbPeriod, BbStdDev, BbBasisType, BbwpLookback, BbwpEmaPeriod, BbwpTimeFrame);
            }
            if (IncludeVolumeTrend)
                Print("Volume trend: tick volume vs EMA({0}) on {1}.", VolumeEmaPeriod, VolumeTimeFrame);

            if (EnableTelegram && (string.IsNullOrWhiteSpace(TelegramBotToken) || string.IsNullOrWhiteSpace(TelegramChatId)))
                Print("WARNING: Telegram enabled but Bot Token / Chat ID is empty — Telegram alerts disabled.");

            if (EnableEmail && (string.IsNullOrWhiteSpace(EmailFrom) || string.IsNullOrWhiteSpace(EmailTo)))
                Print("WARNING: Email enabled but From/To is empty — Email alerts disabled.");

            Print("REMINDER: Run this cBot on DESKTOP (not Cloud) for Telegram/Email to reach your phone.");
            Print("A Telegram message is sent on start, then when the fast EMA crosses the slow EMA on a closed bar.");

            _crossBars = GetBars(EmaTimeFrame);
            _fastEma = Indicators.ExponentialMovingAverage(_crossBars.ClosePrices, FastEmaPeriod);
            _slowEma = Indicators.ExponentialMovingAverage(_crossBars.ClosePrices, SlowEmaPeriod);
            _crossBars.BarClosed += OnCrossBarClosed;

            if (IncludeAdx || IncludeAdxDirection)
            {
                _adxBars = GetBars(AdxTimeFrame);
                _dms = Indicators.DirectionalMovementSystem(_adxBars, AdxPeriod);
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

            double prevFast = _fastEma.Result[index - 1];
            double prevSlow = _slowEma.Result[index - 1];
            double fast = _fastEma.Result[index];
            double slow = _slowEma.Result[index];
            if (!IsFinite(prevFast) || !IsFinite(prevSlow) || !IsFinite(fast) || !IsFinite(slow))
                return false;

            // LONG = fast EMA crossed above the slow EMA. SHORT = crossed below.
            if (prevFast <= prevSlow && fast > slow)
            {
                side = "LONG";
                return true;
            }

            if (prevFast >= prevSlow && fast < slow)
            {
                side = "SHORT";
                return true;
            }

            return false;
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

            if (IncludeAdx || IncludeAdxDirection)
                AppendAdx(sb, ref adxTag, ref directionTag);
            if (IncludeBbwp)
                AppendBbwp(sb, ref bbwpTag);
            if (IncludeVolumeTrend)
                AppendVolume(sb, ref volumeTag);

            var subject = new StringBuilder();
            subject.AppendFormat(CultureInfo.InvariantCulture,
                "EMA {0} cross {1} {2}", side, SymbolName, EmaTimeFrame);
            if (!string.IsNullOrEmpty(adxTag))
                subject.Append(" | ").Append(adxTag);
            if (!string.IsNullOrEmpty(directionTag))
                subject.Append(" | ").Append(directionTag);
            if (!string.IsNullOrEmpty(bbwpTag))
                subject.Append(" | ").Append(bbwpTag);
            if (!string.IsNullOrEmpty(volumeTag))
                subject.Append(" | ").Append(volumeTag);

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
                "Sections: ADX {0} | Long/Short {1} | BBWP {2} | Volume {3}\n",
                OnOff(IncludeAdx), OnOff(IncludeAdxDirection), OnOff(IncludeBbwp), OnOff(IncludeVolumeTrend));

            if (_crossBars != null && _fastEma != null && _slowEma != null && _crossBars.Count >= 3)
            {
                int closed = _crossBars.Count - 2;
                double fast = _fastEma.Result[closed];
                double slow = _slowEma.Result[closed];
                double close = _crossBars.ClosePrices[closed];
                if (IsFinite(fast) && IsFinite(slow) && IsFinite(close))
                {
                    string position = fast > slow ? "ABOVE" : fast < slow ? "BELOW" : "EQUAL TO";
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "Now: Fast EMA is {0} Slow EMA | Fast {1} | Slow {2} | close {3}\n",
                        position, FormatPrice(fast), FormatPrice(slow), FormatPrice(close));
                }
            }

            sb.Append("Next alert when the fast EMA crosses the slow EMA on a closed bar.\n");

            if (IncludeAdx || IncludeAdxDirection)
                AppendAdx(sb, ref adxTag, ref directionTag);
            if (IncludeBbwp)
                AppendBbwp(sb, ref bbwpTag);
            if (IncludeVolumeTrend)
                AppendVolume(sb, ref volumeTag);

            var subject = new StringBuilder();
            subject.AppendFormat(CultureInfo.InvariantCulture, "EMA alert started {0} {1}", SymbolName, EmaTimeFrame);
            if (!string.IsNullOrEmpty(adxTag))
                subject.Append(" | ").Append(adxTag);
            if (!string.IsNullOrEmpty(directionTag))
                subject.Append(" | ").Append(directionTag);
            if (!string.IsNullOrEmpty(bbwpTag))
                subject.Append(" | ").Append(bbwpTag);
            if (!string.IsNullOrEmpty(volumeTag))
                subject.Append(" | ").Append(volumeTag);

            Deliver(sb.ToString().TrimEnd(), subject.ToString());
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

        private void AppendAdx(StringBuilder sb, ref string adxTag, ref string directionTag)
        {
            if (_dms == null || _adxBars == null || _adxBars.Count < AdxPeriod + 2)
            {
                sb.Append("ADX: not ready (need more bars)\n");
                return;
            }

            int closed = _adxBars.Count - 2;
            double adx = _dms.ADX[closed];
            double diPlus = _dms.DIPlus[closed];
            double diMinus = _dms.DIMinus[closed];
            if (!IsFinite(adx))
            {
                sb.Append("ADX: not ready (need more bars)\n");
                return;
            }

            if (IncludeAdx)
            {
                string regime;
                string versus;
                if (adx > AdxLevel)
                {
                    regime = "TRENDING";
                    versus = "ABOVE";
                }
                else if (adx < AdxLevel)
                {
                    regime = "RANGING";
                    versus = "BELOW";
                }
                else
                {
                    regime = "ON LEVEL";
                    versus = "ON";
                }

                adxTag = regime + " " + versus + " " + AdxLevel.ToString("0.##", CultureInfo.InvariantCulture);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "ADX({0}) {1} = {2:0.00} — {3} — {4} the {5:0.##} line\n",
                    AdxPeriod, AdxTimeFrame, adx, regime, versus, AdxLevel);
            }

            if (!IncludeAdxDirection)
                return;

            if (!IsFinite(diPlus) || !IsFinite(diMinus))
            {
                sb.Append("ADX trend: not ready\n");
                return;
            }

            string direction;
            string detail;
            if (diPlus > diMinus)
            {
                direction = "LONG";
                detail = "+DI > −DI";
            }
            else if (diMinus > diPlus)
            {
                direction = "SHORT";
                detail = "−DI > +DI";
            }
            else
            {
                direction = "FLAT";
                detail = "+DI = −DI";
            }

            directionTag = "ADX " + direction;
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "ADX trend: {0} ({1} | +DI {2:0.00} | −DI {3:0.00})\n",
                direction, detail, diPlus, diMinus);
        }

        private void AppendBbwp(StringBuilder sb, ref string bbwpTag)
        {
            if (_bb == null || _bbwpBars == null || _bbwpBars.Count < 3)
            {
                sb.Append("BBWP: not ready (need more bars)\n");
                return;
            }

            int closed = _bbwpBars.Count - 2;
            double bbwp;
            double bbwpEma;
            bool hasEma;
            if (!TryComputeBbwp(closed, out bbwp, out bbwpEma, out hasEma))
            {
                sb.Append("BBWP: not ready (need more bars)\n");
                return;
            }

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "BBWP({0}, {1:0.##}, lookback {2}) {3} = {4:0.00}%",
                BbPeriod, BbStdDev, BbwpLookback, BbwpTimeFrame, bbwp);

            if (!hasEma)
            {
                sb.Append(" | EMA not ready\n");
                return;
            }

            string relation;
            if (bbwpEma > bbwp)
            {
                relation = "BBWP EMA is ABOVE the BBWP% value";
                bbwpTag = "BBWP EMA ABOVE";
            }
            else if (bbwpEma < bbwp)
            {
                relation = "BBWP EMA is BELOW the BBWP% value";
                bbwpTag = "BBWP EMA BELOW";
            }
            else
            {
                relation = "BBWP EMA is EQUAL to the BBWP% value";
                bbwpTag = "BBWP EMA EQUAL";
            }

            sb.AppendFormat(CultureInfo.InvariantCulture,
                " | EMA({0}) = {1:0.00}\n{2}\n",
                BbwpEmaPeriod, bbwpEma, relation);
        }

        private void AppendVolume(StringBuilder sb, ref string volumeTag)
        {
            if (_volumeEma == null || _volumeBars == null || _volumeBars.Count < VolumeEmaPeriod + 2)
            {
                sb.Append("Volume trend: not ready (need more bars)\n");
                return;
            }

            int closed = _volumeBars.Count - 2;
            if (closed < VolumeEmaPeriod)
            {
                sb.Append("Volume trend: not ready (need more bars)\n");
                return;
            }

            double volume = _volumeBars.TickVolumes[closed];
            double ema = _volumeEma.Result[closed];
            if (!IsFinite(volume) || !IsFinite(ema))
            {
                sb.Append("Volume trend: not ready (need more bars)\n");
                return;
            }

            string relation;
            if (volume > ema)
            {
                relation = "ABOVE";
                volumeTag = "Volume ABOVE EMA";
            }
            else if (volume < ema)
            {
                relation = "BELOW";
                volumeTag = "Volume BELOW EMA";
            }
            else
            {
                relation = "EQUAL TO";
                volumeTag = "Volume ON EMA";
            }

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Volume trend {0}: {1} the EMA({2}) | volume {3} | EMA {4}\n",
                VolumeTimeFrame,
                relation,
                VolumeEmaPeriod,
                volume.ToString("0.##", CultureInfo.InvariantCulture),
                ema.ToString("0.##", CultureInfo.InvariantCulture));
        }

        private bool TryComputeBbwp(int closedIndex, out double bbwp, out double bbwpEma, out bool hasEma)
        {
            bbwp = 0;
            bbwpEma = 0;
            hasEma = false;

            if (closedIndex < BbPeriod)
                return false;

            var width = new double[closedIndex + 1];
            for (int i = 0; i <= closedIndex; i++)
                width[i] = BandWidth(i);

            int start = -1;
            for (int i = BbwpLookback; i <= closedIndex; i++)
            {
                if (IsCompleteWindow(width, i))
                {
                    start = i;
                    break;
                }
            }

            if (start < 0 || !IsCompleteWindow(width, closedIndex))
                return false;

            int length = closedIndex - start + 1;
            var series = new double[length];
            for (int i = start; i <= closedIndex; i++)
                series[i - start] = PercentRank(width, i);

            bbwp = series[length - 1];
            if (!IsFinite(bbwp))
                return false;

            if (length < BbwpEmaPeriod)
                return true;

            bbwpEma = ExponentialAverage(series, BbwpEmaPeriod);
            hasEma = IsFinite(bbwpEma);
            return true;
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

        private bool IsCompleteWindow(double[] width, int index)
        {
            if (index < BbwpLookback || !IsFinite(width[index]))
                return false;

            for (int j = index - BbwpLookback; j < index; j++)
            {
                if (!IsFinite(width[j]))
                    return false;
            }

            return true;
        }

        private double PercentRank(double[] width, int index)
        {
            double current = width[index];
            int rank = 0;
            int from = index - BbwpLookback;
            for (int j = from; j < index; j++)
            {
                if (width[j] <= current)
                    rank++;
            }

            return 100.0 * rank / BbwpLookback;
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
                string encoded = Uri.EscapeDataString(text);
                string url = string.Format(
                    "https://api.telegram.org/bot{0}/sendMessage?chat_id={1}&text={2}",
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
    }
}
