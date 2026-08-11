// -------------------------------------------------------------------------------------------------
// SMC_Testing — SIDE EXPERIMENT (does not replace UltimateTrader2026 / Test G)
//
// INSTALL: Ctrl+A → Delete → paste THIS ENTIRE file → Build
// Class name must match your cBot (SMC_Testing). Rename if cTrader requires otherwise.
//
// Features:
//   H4 bias → m15 sweep → CHoCH → order-block retest
//   News pause (entries blocked around events)
//   Optimisable SL % (0.6–1.0) and TP RR (1.8–2.8) — set ranges in Optimizer
//   Optional Level-2 depth imbalance filter (broker must publish DOM)
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    public enum SmcTradeDirectionMode
    {
        LongAndShort,
        LongOnly,
        ShortOnly
    }

    public enum SmcSlMode
    {
        OrderBlock,   // SL beyond OB (structure)
        Percent       // SL = % of entry price (optimisable like G)
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SMC_Testing : Robot
    {
        // ---- Trade options ----------------------------------------------------------------------

        [Parameter("Bot Trade ID", DefaultValue = "SMC_BTC_SIDE", Group = "Trade Options")]
        public string BotTradeId { get; set; }

        [Parameter("Order Direction", DefaultValue = SmcTradeDirectionMode.LongAndShort, Group = "Trade Options")]
        public SmcTradeDirectionMode OrderDirection { get; set; }

        // ---- Timeframes / structure -------------------------------------------------------------

        [Parameter("Bias Time Frame (HTF)", DefaultValue = "Hour4", Group = "Timeframes")]
        public TimeFrame BiasTimeFrame { get; set; }

        [Parameter("Structure Pivot Strength", DefaultValue = 3, MinValue = 2, MaxValue = 8, Group = "Structure")]
        public int PivotStrength { get; set; }

        [Parameter("Require HTF Bias Align", DefaultValue = true, Group = "SMC Filters")]
        public bool RequireHtfBias { get; set; }

        [Parameter("Require Liquidity Sweep", DefaultValue = true, Group = "SMC Filters")]
        public bool RequireLiquiditySweep { get; set; }

        [Parameter("Sweep Wick Ratio Min", DefaultValue = 0.35, MinValue = 0.1, MaxValue = 0.9, Group = "SMC Filters")]
        public double SweepWickRatioMin { get; set; }

        [Parameter("Max Bars After CHoCH for OB Entry", DefaultValue = 24, MinValue = 4, MaxValue = 100, Group = "SMC Filters")]
        public int MaxBarsAfterChoCh { get; set; }

        // ---- News pause -------------------------------------------------------------------------

        [Parameter("Enable News Pause", DefaultValue = true, Group = "News Pause")]
        public bool EnableNewsPause { get; set; }

        [Parameter("Pause Before Minutes", DefaultValue = 90, MinValue = 0, Group = "News Pause")]
        public int PauseBeforeMinutes { get; set; }

        [Parameter("Resume After Minutes", DefaultValue = 45, MinValue = 0, Group = "News Pause")]
        public int ResumeAfterMinutes { get; set; }

        [Parameter("Pause On NFP", DefaultValue = true, Group = "News Pause Events")]
        public bool PauseOnNfp { get; set; }

        [Parameter("Pause On FOMC", DefaultValue = true, Group = "News Pause Events")]
        public bool PauseOnFomc { get; set; }

        [Parameter("Pause On CPI", DefaultValue = true, Group = "News Pause Events")]
        public bool PauseOnCpi { get; set; }

        [Parameter("Pause On Core PCE", DefaultValue = true, Group = "News Pause Events")]
        public bool PauseOnCorePce { get; set; }

        [Parameter("Extra Events UTC (yyyy-MM-dd HH:mm|Title;...)", DefaultValue = "", Group = "News Pause")]
        public string ExtraNewsEventsUtc { get; set; }

        // ---- SL / TP (optimise these in cTrader Optimizer) --------------------------------------

        [Parameter("SL Mode", DefaultValue = SmcSlMode.Percent, Group = "Stoploss")]
        public SmcSlMode SlMode { get; set; }

        // Optimise: 0.6 → 1.0 step 0.1 (match G)
        [Parameter("SL Percent", DefaultValue = 0.7, MinValue = 0.6, MaxValue = 1.0, Group = "Stoploss")]
        public double SlPercent { get; set; }

        [Parameter("SL Buffer (pips, OB mode)", DefaultValue = 20, MinValue = 0, Group = "Stoploss")]
        public double SlBufferPips { get; set; }

        // Optimise: 1.8 → 2.8 step 0.2 (match G)
        [Parameter("TP Risk Multiplier", DefaultValue = 2.0, MinValue = 1.8, MaxValue = 2.8, Group = "Take Profit")]
        public double TpRiskMultiplier { get; set; }

        // ---- Risk -------------------------------------------------------------------------------

        [Parameter("Trade Risk (USD)", DefaultValue = 400, MinValue = 10, Group = "Risk")]
        public double TradeRiskUsd { get; set; }

        [Parameter("Max Open Positions", DefaultValue = 1, MinValue = 1, MaxValue = 3, Group = "Risk")]
        public int MaxOpenPositions { get; set; }

        [Parameter("One Trade Per Setup", DefaultValue = true, Group = "Risk")]
        public bool OneTradePerSetup { get; set; }

        // ---- Week DD ----------------------------------------------------------------------------

        [Parameter("Enable Week DD Brake", DefaultValue = true, Group = "Week DD Brake")]
        public bool EnableWeekDdBrake { get; set; }

        [Parameter("Week DD Brake %", DefaultValue = 3.5, MinValue = 0.5, MaxValue = 20, Group = "Week DD Brake")]
        public double WeekDdBrakePct { get; set; }

        // ---- Level-2 (DOM) ----------------------------------------------------------------------
        // Many crypto/cTrader feeds have EMPTY depth. If empty, filter is skipped (not a hard block).

        [Parameter("Use Level-2 Imbalance Filter", DefaultValue = false, Group = "Level-2 DOM")]
        public bool UseLevel2ImbalanceFilter { get; set; }

        [Parameter("DOM Levels to Sum", DefaultValue = 5, MinValue = 1, MaxValue = 20, Group = "Level-2 DOM")]
        public int DomLevelsToSum { get; set; }

        [Parameter("Min Bid/Ask Imbalance Ratio", DefaultValue = 1.2, MinValue = 1.0, MaxValue = 5.0, Group = "Level-2 DOM")]
        public double MinDomImbalanceRatio { get; set; }

        // ---- Internals --------------------------------------------------------------------------

        private Bars _htf;
        private int _htfBias;

        private double _ltfLastSwingHigh;
        private double _ltfLastSwingLow;
        private int _ltfLastSwingHighIndex = -1;
        private int _ltfLastSwingLowIndex = -1;

        private bool _sweepHighDone;
        private bool _sweepLowDone;
        private int _choChBarIndex = -1;
        private int _choChDir;
        private double _obHigh;
        private double _obLow;
        private bool _obArmed;
        private bool _setupTraded;

        private DateTime _weekStartUtc;
        private double _weekStartEquity;

        private MarketDepth _depth;
        private readonly List<NewsEvent> _newsEvents = new List<NewsEvent>();

        private struct NewsEvent
        {
            public DateTime Utc;
            public string Title;
        }

        protected override void OnStart()
        {
            _htf = MarketData.GetBars(BiasTimeFrame);
            ResetWeekIfNeeded(force: true);
            BuildNewsCalendar();

            if (UseLevel2ImbalanceFilter)
            {
                _depth = MarketData.GetMarketDepth(SymbolName);
                Print("Level-2 DOM subscribed. BidLevels={0} AskLevels={1} (0/0 = broker not publishing depth)",
                    _depth.BidEntries.Count, _depth.AskEntries.Count);
            }

            Print("SMC_Testing started. Chart={0} Bias={1} SLMode={2} NewsPause={3}",
                Bars.TimeFrame, BiasTimeFrame, SlMode, EnableNewsPause);
            Print("SIDE EXPERIMENT — do not replace UltimateTrader2026 / Test G until A/B wins.");
        }

        protected override void OnBar()
        {
            ResetWeekIfNeeded(force: false);

            if (Bars.Count < PivotStrength * 4 + 10 || _htf.Count < PivotStrength * 4 + 10)
                return;

            UpdateHtfBias();
            UpdateLtfSwings();

            int i = Bars.Count - 2;
            if (i < PivotStrength + 2)
                return;

            DetectSweep(i);
            DetectChoCh(i);
            TryEnterOnOrderBlock(i);
        }

        // =========================================================================================
        // News pause
        // =========================================================================================

        private void BuildNewsCalendar()
        {
            _newsEvents.Clear();

            // Built-in: first Friday NFP ~13:30 US/Eastern ≈ 17:30/18:30 UTC — we use 17:30 UTC as a stable compromise
            if (PauseOnNfp)
            {
                for (int year = Server.Time.Year - 1; year <= Server.Time.Year + 1; year++)
                {
                    for (int month = 1; month <= 12; month++)
                    {
                        var firstFriday = FirstWeekday(year, month, DayOfWeek.Friday);
                        _newsEvents.Add(new NewsEvent { Utc = firstFriday.AddHours(17).AddMinutes(30), Title = "NFP" });
                    }
                }
            }

            // Approximate known dates — extend via ExtraNewsEventsUtc for precision
            if (PauseOnFomc)
            {
                AddBuiltin("2025-01-29 19:00|FOMC");
                AddBuiltin("2025-03-19 18:00|FOMC");
                AddBuiltin("2025-05-07 18:00|FOMC");
                AddBuiltin("2025-06-18 18:00|FOMC");
                AddBuiltin("2025-07-30 18:00|FOMC");
                AddBuiltin("2025-09-17 18:00|FOMC");
                AddBuiltin("2025-10-29 18:00|FOMC");
                AddBuiltin("2025-12-10 19:00|FOMC");
                AddBuiltin("2026-01-28 19:00|FOMC");
                AddBuiltin("2026-03-18 18:00|FOMC");
                AddBuiltin("2026-04-29 18:00|FOMC");
                AddBuiltin("2026-06-17 18:00|FOMC");
                AddBuiltin("2026-07-29 18:00|FOMC");
            }

            if (PauseOnCpi)
            {
                // Mid-month CPI ~12:30/13:30 UTC — placeholders; override with Extra for exact
                for (int year = 2025; year <= 2026; year++)
                for (int month = 1; month <= 12; month++)
                    _newsEvents.Add(new NewsEvent
                    {
                        Utc = new DateTime(year, month, 12, 12, 30, 0, DateTimeKind.Utc),
                        Title = "CPI"
                    });
            }

            if (PauseOnCorePce)
            {
                AddBuiltin("2025-01-31 13:30|Core PCE");
                AddBuiltin("2025-02-28 13:30|Core PCE");
                AddBuiltin("2025-03-28 12:30|Core PCE");
                AddBuiltin("2025-04-30 12:30|Core PCE");
                AddBuiltin("2025-05-30 12:30|Core PCE");
                AddBuiltin("2025-06-27 12:30|Core PCE");
                AddBuiltin("2025-07-31 12:30|Core PCE");
                AddBuiltin("2025-08-29 12:30|Core PCE");
                AddBuiltin("2025-09-26 12:30|Core PCE");
                AddBuiltin("2025-10-31 12:30|Core PCE");
                AddBuiltin("2025-11-26 13:30|Core PCE");
                AddBuiltin("2025-12-19 13:30|Core PCE");
                AddBuiltin("2026-01-30 13:30|Core PCE");
                AddBuiltin("2026-02-27 13:30|Core PCE");
                AddBuiltin("2026-03-27 12:30|Core PCE");
                AddBuiltin("2026-04-30 12:30|Core PCE");
                AddBuiltin("2026-05-29 12:30|Core PCE");
                AddBuiltin("2026-06-26 12:30|Core PCE");
                AddBuiltin("2026-07-31 12:30|Core PCE");
            }

            ParseExtraEvents(ExtraNewsEventsUtc);
            Print("News calendar loaded: {0} event(s). Extra={1}", _newsEvents.Count,
                string.IsNullOrWhiteSpace(ExtraNewsEventsUtc) ? "(none)" : "yes");
        }

        private void AddBuiltin(string line)
        {
            ParseExtraEvents(line);
        }

        private void ParseExtraEvents(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;

            foreach (var part in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = part.Split('|');
                if (bits.Length < 1)
                    continue;
                DateTime dt;
                if (!DateTime.TryParseExact(bits[0].Trim(), "yyyy-MM-dd HH:mm",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
                    continue;
                var title = bits.Length > 1 ? bits[1].Trim() : "Extra";
                _newsEvents.Add(new NewsEvent { Utc = dt, Title = title });
            }
        }

        private static DateTime FirstWeekday(int year, int month, DayOfWeek dow)
        {
            var d = new DateTime(year, month, 1);
            while (d.DayOfWeek != dow)
                d = d.AddDays(1);
            return d;
        }

        private bool IsNewsPauseActive(out string reason)
        {
            reason = null;
            if (!EnableNewsPause || _newsEvents.Count == 0)
                return false;

            var now = Server.Time.ToUniversalTime();
            foreach (var ev in _newsEvents)
            {
                var start = ev.Utc.AddMinutes(-PauseBeforeMinutes);
                var end = ev.Utc.AddMinutes(ResumeAfterMinutes);
                if (now >= start && now <= end)
                {
                    reason = ev.Title + " @ " + ev.Utc.ToString("u");
                    return true;
                }
            }
            return false;
        }

        // =========================================================================================
        // Level-2 DOM imbalance
        // =========================================================================================

        private bool PassesDomFilter(bool forLong, out string detail)
        {
            detail = "DOM off";
            if (!UseLevel2ImbalanceFilter || _depth == null)
                return true;

            double bidVol = 0, askVol = 0;
            int n = 0;
            foreach (var e in _depth.BidEntries)
            {
                bidVol += e.VolumeInUnits;
                if (++n >= DomLevelsToSum) break;
            }
            n = 0;
            foreach (var e in _depth.AskEntries)
            {
                askVol += e.VolumeInUnits;
                if (++n >= DomLevelsToSum) break;
            }

            // Broker not publishing depth → do not block entries
            if (bidVol <= 0 && askVol <= 0)
            {
                detail = "DOM empty — skipped";
                return true;
            }

            if (forLong)
            {
                bool ok = askVol <= 0 || bidVol >= askVol * MinDomImbalanceRatio;
                detail = string.Format("bid={0:F2} ask={1:F2} need bid>=ask*{2}", bidVol, askVol, MinDomImbalanceRatio);
                return ok;
            }
            else
            {
                bool ok = bidVol <= 0 || askVol >= bidVol * MinDomImbalanceRatio;
                detail = string.Format("bid={0:F2} ask={1:F2} need ask>=bid*{2}", bidVol, askVol, MinDomImbalanceRatio);
                return ok;
            }
        }

        // =========================================================================================
        // Structure / SMC core
        // =========================================================================================

        private void UpdateHtfBias()
        {
            int last = _htf.Count - 2;
            if (last < PivotStrength * 3)
                return;

            double sh = double.NaN, sl = double.NaN;
            for (int i = PivotStrength; i <= last - PivotStrength; i++)
            {
                if (IsPivotHigh(_htf, i, PivotStrength)) sh = _htf.HighPrices[i];
                if (IsPivotLow(_htf, i, PivotStrength)) sl = _htf.LowPrices[i];
            }

            for (int i = Math.Max(PivotStrength * 2, last - 40); i <= last; i++)
            {
                if (!double.IsNaN(sh) && _htf.ClosePrices[i] > sh) _htfBias = 1;
                if (!double.IsNaN(sl) && _htf.ClosePrices[i] < sl) _htfBias = -1;
                if (IsPivotHigh(_htf, i, PivotStrength)) sh = _htf.HighPrices[i];
                if (IsPivotLow(_htf, i, PivotStrength)) sl = _htf.LowPrices[i];
            }
        }

        private void UpdateLtfSwings()
        {
            int last = Bars.Count - 2;
            for (int i = Math.Max(PivotStrength, last - 80); i <= last - PivotStrength; i++)
            {
                if (IsPivotHigh(Bars, i, PivotStrength))
                {
                    _ltfLastSwingHigh = Bars.HighPrices[i];
                    _ltfLastSwingHighIndex = i;
                }
                if (IsPivotLow(Bars, i, PivotStrength))
                {
                    _ltfLastSwingLow = Bars.LowPrices[i];
                    _ltfLastSwingLowIndex = i;
                }
            }
        }

        private static bool IsPivotHigh(Bars bars, int i, int strength)
        {
            double h = bars.HighPrices[i];
            for (int k = 1; k <= strength; k++)
                if (bars.HighPrices[i - k] >= h || bars.HighPrices[i + k] > h) return false;
            return true;
        }

        private static bool IsPivotLow(Bars bars, int i, int strength)
        {
            double l = bars.LowPrices[i];
            for (int k = 1; k <= strength; k++)
                if (bars.LowPrices[i - k] <= l || bars.LowPrices[i + k] < l) return false;
            return true;
        }

        private void DetectSweep(int i)
        {
            double open = Bars.OpenPrices[i];
            double close = Bars.ClosePrices[i];
            double high = Bars.HighPrices[i];
            double low = Bars.LowPrices[i];
            double range = high - low;
            if (range <= Symbol.TickSize) return;

            if (_ltfLastSwingHighIndex >= 0 && high > _ltfLastSwingHigh && close < _ltfLastSwingHigh)
            {
                double upperWick = high - Math.Max(open, close);
                if (upperWick / range >= SweepWickRatioMin)
                {
                    _sweepHighDone = true;
                    _sweepLowDone = false;
                    _obArmed = false;
                    _setupTraded = false;
                    _choChBarIndex = -1;
                    Print("{0} Sweep HIGH @ {1:F2}", Bars.OpenTimes[i], high);
                }
            }

            if (_ltfLastSwingLowIndex >= 0 && low < _ltfLastSwingLow && close > _ltfLastSwingLow)
            {
                double lowerWick = Math.Min(open, close) - low;
                if (lowerWick / range >= SweepWickRatioMin)
                {
                    _sweepLowDone = true;
                    _sweepHighDone = false;
                    _obArmed = false;
                    _setupTraded = false;
                    _choChBarIndex = -1;
                    Print("{0} Sweep LOW @ {1:F2}", Bars.OpenTimes[i], low);
                }
            }
        }

        private void DetectChoCh(int i)
        {
            if (RequireLiquiditySweep && !_sweepHighDone && !_sweepLowDone)
                return;

            if (_sweepLowDone && _ltfLastSwingHighIndex >= 0 && Bars.ClosePrices[i] > _ltfLastSwingHigh)
            {
                if (!(RequireHtfBias && _htfBias < 0))
                {
                    _choChDir = 1;
                    _choChBarIndex = i;
                    MarkOrderBlock(i, bullish: true);
                    _sweepLowDone = false;
                    Print("{0} Bullish CHoCH OB [{1:F2}..{2:F2}]", Bars.OpenTimes[i], _obLow, _obHigh);
                    return;
                }
            }

            if (_sweepHighDone && _ltfLastSwingLowIndex >= 0 && Bars.ClosePrices[i] < _ltfLastSwingLow)
            {
                if (RequireHtfBias && _htfBias > 0)
                    return;

                _choChDir = -1;
                _choChBarIndex = i;
                MarkOrderBlock(i, bullish: false);
                _sweepHighDone = false;
                Print("{0} Bearish CHoCH OB [{1:F2}..{2:F2}]", Bars.OpenTimes[i], _obLow, _obHigh);
            }
        }

        private void MarkOrderBlock(int choChIndex, bool bullish)
        {
            for (int j = choChIndex - 1; j >= Math.Max(0, choChIndex - 15); j--)
            {
                bool bearishCandle = Bars.ClosePrices[j] < Bars.OpenPrices[j];
                bool bullishCandle = Bars.ClosePrices[j] > Bars.OpenPrices[j];
                if (bullish && bearishCandle)
                {
                    _obHigh = Bars.HighPrices[j];
                    _obLow = Bars.LowPrices[j];
                    _obArmed = true;
                    _setupTraded = false;
                    return;
                }
                if (!bullish && bullishCandle)
                {
                    _obHigh = Bars.HighPrices[j];
                    _obLow = Bars.LowPrices[j];
                    _obArmed = true;
                    _setupTraded = false;
                    return;
                }
            }
            int p = Math.Max(0, choChIndex - 1);
            _obHigh = Bars.HighPrices[p];
            _obLow = Bars.LowPrices[p];
            _obArmed = true;
            _setupTraded = false;
        }

        private void TryEnterOnOrderBlock(int i)
        {
            if (!_obArmed || _choChBarIndex < 0) return;
            if (OneTradePerSetup && _setupTraded) return;
            if (i - _choChBarIndex > MaxBarsAfterChoCh) { _obArmed = false; return; }
            if (CountOurPositions() >= MaxOpenPositions) return;
            if (EnableWeekDdBrake && IsWeekDdBrakeActive()) return;

            string newsReason;
            if (IsNewsPauseActive(out newsReason))
            {
                Print("Entry blocked by news pause: {0}", newsReason);
                return;
            }

            double high = Bars.HighPrices[i];
            double low = Bars.LowPrices[i];
            double close = Bars.ClosePrices[i];

            bool longOk = OrderDirection != SmcTradeDirectionMode.ShortOnly;
            bool shortOk = OrderDirection != SmcTradeDirectionMode.LongOnly;

            if (_choChDir > 0 && longOk)
            {
                bool touched = low <= _obHigh && low >= _obLow - Symbol.PipSize * SlBufferPips;
                bool reclaim = close >= (_obLow + _obHigh) * 0.5;
                if (touched && reclaim)
                {
                    string domDetail;
                    if (!PassesDomFilter(forLong: true, out domDetail))
                    {
                        Print("LONG blocked by DOM filter ({0})", domDetail);
                        return;
                    }
                    EnterLong();
                    _setupTraded = true;
                    _obArmed = false;
                }
            }

            if (_choChDir < 0 && shortOk)
            {
                bool touched = high >= _obLow && high <= _obHigh + Symbol.PipSize * SlBufferPips;
                bool reject = close <= (_obLow + _obHigh) * 0.5;
                if (touched && reject)
                {
                    string domDetail;
                    if (!PassesDomFilter(forLong: false, out domDetail))
                    {
                        Print("SHORT blocked by DOM filter ({0})", domDetail);
                        return;
                    }
                    EnterShort();
                    _setupTraded = true;
                    _obArmed = false;
                }
            }
        }

        private void EnterLong()
        {
            double entry = Symbol.Ask;
            double slPrice = SlMode == SmcSlMode.Percent
                ? entry * (1.0 - SlPercent / 100.0)
                : _obLow - Symbol.PipSize * SlBufferPips;

            double riskPerUnit = entry - slPrice;
            if (riskPerUnit <= 0) return;

            double volume = VolumeForRisk(riskPerUnit);
            if (volume <= 0) return;

            double tpPrice = entry + riskPerUnit * TpRiskMultiplier;
            var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, volume, BotTradeId, slPrice, tpPrice);
            if (result.IsSuccessful)
                Print("LONG entry={0:F2} SL={1:F2} TP={2:F2} vol={3}", entry, slPrice, tpPrice, volume);
            else
                Print("LONG failed: {0}", result.Error);
        }

        private void EnterShort()
        {
            double entry = Symbol.Bid;
            double slPrice = SlMode == SmcSlMode.Percent
                ? entry * (1.0 + SlPercent / 100.0)
                : _obHigh + Symbol.PipSize * SlBufferPips;

            double riskPerUnit = slPrice - entry;
            if (riskPerUnit <= 0) return;

            double volume = VolumeForRisk(riskPerUnit);
            if (volume <= 0) return;

            double tpPrice = entry - riskPerUnit * TpRiskMultiplier;
            var result = ExecuteMarketOrder(TradeType.Sell, SymbolName, volume, BotTradeId, slPrice, tpPrice);
            if (result.IsSuccessful)
                Print("SHORT entry={0:F2} SL={1:F2} TP={2:F2} vol={3}", entry, slPrice, tpPrice, volume);
            else
                Print("SHORT failed: {0}", result.Error);
        }

        private double VolumeForRisk(double riskPerUnit)
        {
            double raw = TradeRiskUsd / riskPerUnit;
            double step = Symbol.VolumeInUnitsStep;
            double min = Symbol.VolumeInUnitsMin;
            double max = Symbol.VolumeInUnitsMax;
            double vol = Math.Floor(raw / step) * step;
            if (vol < min) return 0;
            if (vol > max) vol = max;
            return Symbol.NormalizeVolumeInUnits(vol, RoundingMode.Down);
        }

        private int CountOurPositions()
        {
            int n = 0;
            foreach (var p in Positions)
                if (p.SymbolName == SymbolName && p.Label == BotTradeId) n++;
            return n;
        }

        private void ResetWeekIfNeeded(bool force)
        {
            var now = Server.Time;
            int diff = ((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var monday = now.Date.AddDays(-diff);
            if (force || monday != _weekStartUtc)
            {
                _weekStartUtc = monday;
                _weekStartEquity = Account.Equity;
                if (EnableWeekDdBrake)
                    Print("Week DD brake: week start {0:u} equity={1:F2}", _weekStartUtc, _weekStartEquity);
            }
        }

        private bool IsWeekDdBrakeActive()
        {
            if (!EnableWeekDdBrake || _weekStartEquity <= 0) return false;
            double ddPct = (_weekStartEquity - Account.Equity) / _weekStartEquity * 100.0;
            return ddPct >= WeekDdBrakePct;
        }
    }
}
