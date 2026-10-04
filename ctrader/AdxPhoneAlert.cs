//───────────────────────────────────────────────────────────────────────────────
// cBot: ADX Phone Alert (side project)
// Author: Adam Pink
// Purpose: Periodically report ADX (and optional DI+/DI−) on a chosen timeframe,
//          plus whether price is above a selected EMA, and whether the EMA of
//          BBWP is above the BBWP% line.
// Phone delivery: Telegram (recommended) and/or Email. Journal always logged.
// NOTE: Outbound alerts need a DESKTOP/local instance — cTrader Cloud blocks
//       HTTP and email. Attach this cBot to any chart; it monitors the TF you pick.
//
// BBWP (Bollinger Band Width Percentile), same idea as TradingView ta.percentrank:
//   band width = (upper − lower) / middle
//   BBWP%      = 100 × (how many of the previous Lookback widths are <= current) / Lookback
//   The EMA is an exponential moving average of that BBWP% series.
//   "EMA is ABOVE the BBWP% line" means that EMA value is greater than the current BBWP%.
//───────────────────────────────────────────────────────────────────────────────
using System;
using System.Globalization;
using System.Text;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    public enum AdxAlertIntervalUnit
    {
        Minutes,
        Hours,
        Bars
    }

    /// <summary>
    /// Always = report every interval.
    /// Above / Below = only send when ADX is above/below Filter Level.
    /// </summary>
    public enum AdxThresholdFilterMode
    {
        Always,
        Above,
        Below
    }

    // AccessRights.None — Http.Get still works on desktop for Telegram (.NET 6+).
    // Cloud blocks outbound HTTP/email anyway.
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class AdxPhoneAlert : Robot
    {
        // ===========================
        // Chart / ADX
        // ===========================
        [Parameter("ADX TimeFrame", Group = "ADX", DefaultValue = "Hour")]
        public TimeFrame AdxTimeFrame { get; set; }

        [Parameter("ADX Period", Group = "ADX", DefaultValue = 14, MinValue = 1)]
        public int AdxPeriod { get; set; }

        [Parameter("Include DI+ / DI−", Group = "ADX", DefaultValue = true)]
        public bool IncludeDi { get; set; }

        [Parameter("Trending Threshold", Group = "ADX", DefaultValue = 25.0)]
        public double TrendingThreshold { get; set; }

        [Parameter("Ranging Threshold", Group = "ADX", DefaultValue = 20.0)]
        public double RangingThreshold { get; set; }

        // ===========================
        // Only alert when ADX is above/below a level (optional)
        // ===========================
        [Parameter("Threshold Filter", Group = "ADX Filter", DefaultValue = AdxThresholdFilterMode.Always)]
        public AdxThresholdFilterMode ThresholdFilter { get; set; }

        [Parameter("Filter Level", Group = "ADX Filter", DefaultValue = 25.0, MinValue = 0.0, MaxValue = 100.0)]
        public double FilterLevel { get; set; }

        [Parameter("Log Skipped Alerts", Group = "ADX Filter", DefaultValue = false)]
        public bool LogSkippedAlerts { get; set; }

        // ===========================
        // Volume Trend EMA — is the closed price above this EMA?
        // ===========================
        [Parameter("Include EMA", Group = "Volume Trend EMA", DefaultValue = true)]
        public bool IncludeEma { get; set; }

        [Parameter("EMA TimeFrame", Group = "Volume Trend EMA", DefaultValue = "Hour")]
        public TimeFrame EmaTimeFrame { get; set; }

        [Parameter("EMA Period", Group = "Volume Trend EMA", DefaultValue = 50, MinValue = 1)]
        public int EmaPeriod { get; set; }

        // ===========================
        // BBWP — is the EMA of BBWP above the BBWP% line?
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
        // Frequency
        // ===========================
        [Parameter("Interval Unit", Group = "Frequency", DefaultValue = AdxAlertIntervalUnit.Minutes)]
        public AdxAlertIntervalUnit IntervalUnit { get; set; }

        [Parameter("Interval Value", Group = "Frequency", DefaultValue = 60, MinValue = 1)]
        public int IntervalValue { get; set; }

        [Parameter("Send Alert On Start", Group = "Frequency", DefaultValue = true)]
        public bool AlertOnStart { get; set; }

        // ===========================
        // Telegram (phone message)
        // ===========================
        [Parameter("Enable Telegram", Group = "Telegram", DefaultValue = true)]
        public bool EnableTelegram { get; set; }

        // Create a bot via @BotFather, then get chat id via @userinfobot or getUpdates.
        // Leave these blank in source and paste them into the cBot parameters.
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

        // ===========================
        // Local notify
        // ===========================
        private DirectionalMovementSystem _dms;
        private Bars _adxBars;
        private ExponentialMovingAverage _ema;
        private Bars _emaBars;
        private BollingerBands _bb;
        private Bars _bbwpBars;
        private DateTime _nextAlertUtc = DateTime.MinValue;
        private int _lastAlertedBarIndex = -1;

        protected override void OnStart()
        {
            Print("*** ADX Phone Alert BUILD 2026-10-04-v5 ***");
            Print("Symbol={0} | ADX TF={1} | Period={2} | Interval={3} {4}",
                SymbolName, AdxTimeFrame, AdxPeriod, IntervalValue, IntervalUnit);
            if (ThresholdFilter == AdxThresholdFilterMode.Always)
                Print("ADX filter: Always (every interval).");
            else
                Print("ADX filter: only when ADX is {0} {1:0.##}", ThresholdFilter, FilterLevel);

            if (IncludeEma)
                Print("Volume Trend EMA: period {0} on {1} (close vs EMA).", EmaPeriod, EmaTimeFrame);
            else
                Print("Volume Trend EMA: off.");

            if (IncludeBbwp)
            {
                Print("BBWP: BB({0}, {1:0.##}, {2}) lookback {3} | EMA({4}) on {5}.",
                    BbPeriod, BbStdDev, BbBasisType, BbwpLookback, BbwpEmaPeriod, BbwpTimeFrame);
            }
            else
            {
                Print("BBWP: off.");
            }

            if (EnableTelegram && (string.IsNullOrWhiteSpace(TelegramBotToken) || string.IsNullOrWhiteSpace(TelegramChatId)))
                Print("WARNING: Telegram enabled but Bot Token / Chat ID is empty — Telegram alerts disabled.");

            if (EnableEmail && (string.IsNullOrWhiteSpace(EmailFrom) || string.IsNullOrWhiteSpace(EmailTo)))
                Print("WARNING: Email enabled but From/To is empty — Email alerts disabled.");

            Print("REMINDER: Run this cBot on DESKTOP (not Cloud) for Telegram/Email to reach your phone.");

            _adxBars = AdxTimeFrame == Bars.TimeFrame
                ? Bars
                : MarketData.GetBars(AdxTimeFrame);

            _dms = Indicators.DirectionalMovementSystem(_adxBars, AdxPeriod);

            if (IncludeEma)
            {
                _emaBars = GetBars(EmaTimeFrame);
                _ema = Indicators.ExponentialMovingAverage(_emaBars.ClosePrices, EmaPeriod);
            }

            if (IncludeBbwp)
            {
                _bbwpBars = GetBars(BbwpTimeFrame);
                _bb = Indicators.BollingerBands(_bbwpBars.ClosePrices, BbPeriod, BbStdDev, BbBasisType);
            }

            if (IntervalUnit == AdxAlertIntervalUnit.Bars)
            {
                // Bar-mode checks on each tick of the monitored TF via OnBar of chart;
                // we also poll with a short timer so a higher TF still gets checked.
                Timer.Start(15);
            }
            else
            {
                ScheduleNextAlert(forceImmediate: AlertOnStart);
                StartIntervalTimer();
            }

            if (AlertOnStart)
                SendAdxAlert("startup");
        }

        protected override void OnTimer()
        {
            if (IntervalUnit == AdxAlertIntervalUnit.Bars)
            {
                TrySendBarIntervalAlert();
                return;
            }

            var now = Server.Time.Kind == DateTimeKind.Utc
                ? Server.Time
                : DateTime.SpecifyKind(Server.Time, DateTimeKind.Utc);

            if (now >= _nextAlertUtc)
            {
                SendAdxAlert("schedule");
                ScheduleNextAlert(forceImmediate: false);
            }

            StartIntervalTimer();
        }

        protected override void OnBar()
        {
            if (IntervalUnit == AdxAlertIntervalUnit.Bars)
                TrySendBarIntervalAlert();
        }

        protected override void OnStop()
        {
            Timer.Stop();
            Print("ADX Phone Alert stopped.");
        }

        private Bars GetBars(TimeFrame timeFrame)
        {
            if (timeFrame == Bars.TimeFrame)
                return Bars;
            if (_adxBars != null && timeFrame == AdxTimeFrame)
                return _adxBars;
            return MarketData.GetBars(timeFrame);
        }

        private void StartIntervalTimer()
        {
            // Wake often enough to hit the schedule; keep wake short and mobile-safe.
            int wakeSeconds = 30;
            if (IntervalUnit == AdxAlertIntervalUnit.Minutes && IntervalValue < 5)
                wakeSeconds = 10;

            Timer.Start(wakeSeconds);
        }

        private void ScheduleNextAlert(bool forceImmediate)
        {
            var now = Server.Time.Kind == DateTimeKind.Utc
                ? Server.Time
                : DateTime.SpecifyKind(Server.Time, DateTimeKind.Utc);

            if (forceImmediate)
            {
                _nextAlertUtc = now;
                return;
            }

            if (IntervalUnit == AdxAlertIntervalUnit.Hours)
                _nextAlertUtc = now.AddHours(IntervalValue);
            else
                _nextAlertUtc = now.AddMinutes(IntervalValue);

            Print("Next ADX alert at {0:dd/MM/yyyy HH:mm:ss} UTC", _nextAlertUtc);
        }

        private void TrySendBarIntervalAlert()
        {
            if (_adxBars == null || _adxBars.Count < AdxPeriod + 2)
                return;

            int closedIndex = _adxBars.Count - 2; // last closed bar on ADX TF
            if (closedIndex < 0)
                return;

            // Fire once every N closed ADX-TF bars
            if (_lastAlertedBarIndex < 0)
            {
                _lastAlertedBarIndex = closedIndex;
                return;
            }

            int barsSince = closedIndex - _lastAlertedBarIndex;
            if (barsSince < IntervalValue)
                return;

            _lastAlertedBarIndex = closedIndex;
            SendAdxAlert("bar");
        }

        private void SendAdxAlert(string reason)
        {
            if (_dms == null || _adxBars == null || _adxBars.Count < AdxPeriod + 2)
            {
                Print("ADX not ready yet (need more bars).");
                return;
            }

            int i = _adxBars.Count - 2; // closed bar
            double adx = _dms.ADX[i];
            double diPlus = _dms.DIPlus[i];
            double diMinus = _dms.DIMinus[i];

            if (!PassesThresholdFilter(adx))
            {
                if (LogSkippedAlerts)
                {
                    Print("Skipped alert ({0}): ADX={1:0.00} does not meet filter {2} {3:0.##}",
                        reason, adx, ThresholdFilter, FilterLevel);
                }
                return;
            }

            string regime;
            if (adx >= TrendingThreshold)
                regime = "TRENDING";
            else if (adx <= RangingThreshold)
                regime = "RANGING";
            else
                regime = "NEUTRAL";

            string bias = diPlus > diMinus ? "DI+ > DI− (bullish bias)"
                : diMinus > diPlus ? "DI− > DI+ (bearish bias)"
                : "DI+ ≈ DI−";

            string emaSide = null;
            string bbwpSide = null;

            var barTime = _adxBars.OpenTimes[i];
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "ADX Alert | {0} | TF {1}\n", SymbolName, AdxTimeFrame);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Bar: {0:dd/MM/yyyy HH:mm} UTC\n", barTime);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "ADX({0}) = {1:0.00}  [{2}]\n", AdxPeriod, adx, regime);

            if (ThresholdFilter != AdxThresholdFilterMode.Always)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "Filter: {0} {1:0.##} (passed)\n", ThresholdFilter, FilterLevel);
            }

            if (IncludeDi)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "+DI = {0:0.00} | −DI = {1:0.00}\n", diPlus, diMinus);
                sb.Append(bias).Append('\n');
            }

            AppendEma(sb, ref emaSide);
            AppendBbwp(sb, ref bbwpSide);

            sb.AppendFormat("Reason: {0}", reason);

            string message = sb.ToString();
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
                    string subject = string.Format(CultureInfo.InvariantCulture,
                        "ADX {0} {1}: {2:0.00} [{3}]",
                        SymbolName, AdxTimeFrame, adx, regime);
                    if (!string.IsNullOrEmpty(emaSide))
                        subject += " | Price " + emaSide + " EMA";
                    if (!string.IsNullOrEmpty(bbwpSide))
                        subject += " | EMA " + bbwpSide + " BBWP";

                    Notifications.SendEmail(EmailFrom, EmailTo, subject, message);
                    Print("Email alert sent to {0}", EmailTo);
                }
                catch (Exception ex)
                {
                    Print("Email alert failed: {0}", ex.Message);
                }
            }
        }

        private void AppendEma(StringBuilder sb, ref string emaSide)
        {
            if (!IncludeEma)
                return;

            if (_ema == null || _emaBars == null || _emaBars.Count < EmaPeriod + 2)
            {
                sb.Append("Volume Trend EMA: not ready (need more bars)\n");
                return;
            }

            int closed = _emaBars.Count - 2;
            if (closed < EmaPeriod)
            {
                sb.Append("Volume Trend EMA: not ready (need more bars)\n");
                return;
            }

            double ema = _ema.Result[closed];
            double close = _emaBars.ClosePrices[closed];
            if (!IsFinite(ema) || !IsFinite(close))
            {
                sb.Append("Volume Trend EMA: not ready (need more bars)\n");
                return;
            }

            string relation;
            if (close > ema)
            {
                relation = "ABOVE";
                emaSide = "ABOVE";
            }
            else if (close < ema)
            {
                relation = "BELOW";
                emaSide = "BELOW";
            }
            else
            {
                relation = "EQUAL TO";
                emaSide = "ON";
            }

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Volume Trend EMA({0}) {1}: close is {2} the EMA | close {3} | EMA {4}\n",
                EmaPeriod,
                EmaTimeFrame,
                relation,
                FormatPrice(close),
                FormatPrice(ema));
        }

        private void AppendBbwp(StringBuilder sb, ref string bbwpSide)
        {
            if (!IncludeBbwp)
                return;

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
                relation = "EMA is ABOVE the BBWP% line";
                bbwpSide = "ABOVE";
            }
            else if (bbwpEma < bbwp)
            {
                relation = "EMA is BELOW the BBWP% line";
                bbwpSide = "BELOW";
            }
            else
            {
                relation = "EMA is ON the BBWP% line";
                bbwpSide = "ON";
            }

            sb.AppendFormat(CultureInfo.InvariantCulture,
                " | EMA({0}) = {1:0.00}\n{2}\n",
                BbwpEmaPeriod, bbwpEma, relation);
        }

        /// <summary>
        /// BBWP at the closed bar, plus the EMA of the BBWP series through that bar.
        /// Returns false when the percentile itself is not available yet.
        /// hasEma is false when BBWP exists but the EMA is still warming up.
        /// </summary>
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
            // First window is the BbPeriod bars ending at this index.
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

        /// <summary>
        /// TradingView ta.percentrank: share of the previous Lookback values that are &lt;= current.
        /// </summary>
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

        /// <summary>
        /// EMA seeded with an SMA of the first period, then the standard 2/(n+1) smoother.
        /// </summary>
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

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private bool PassesThresholdFilter(double adx)
        {
            if (ThresholdFilter == AdxThresholdFilterMode.Always)
                return true;
            if (ThresholdFilter == AdxThresholdFilterMode.Above)
                return adx > FilterLevel;
            if (ThresholdFilter == AdxThresholdFilterMode.Below)
                return adx < FilterLevel;
            return true;
        }

        private void SendTelegram(string text)
        {
            try
            {
                // URL-encode manually (no System.Web / no LINQ)
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
