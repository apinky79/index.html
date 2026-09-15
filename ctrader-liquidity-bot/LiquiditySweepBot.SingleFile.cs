using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    internal static class SymbolHelper
    {
        public static double NormalizePrice(Symbol symbol, double price)
        {
            if (symbol.TickSize > 0)
                price = Math.Round(price / symbol.TickSize) * symbol.TickSize;
            return Math.Round(price, symbol.Digits);
        }
    }

    // --- Enums ---

    public enum LiquiditySide
    {
        BuySide,
        SellSide
    }

    public enum LiquiditySource
    {
        SwingHigh,
        SwingLow,
        EqualHigh,
        EqualLow,
        PreviousDayHigh,
        PreviousDayLow,
        PreviousWeekHigh,
        PreviousWeekLow
    }

    public enum SweepPhase
    {
        None,
        Swept,
        Confirmed,
        Invalidated
    }

    public enum RiskMode
    {
        FixedUsd,
        PercentEquity,
        PercentBalance
    }

    public enum StopLossType
    {
        SweepWick,
        Pips,
        Percent,
        ATR
    }

    public enum TakeProfitType
    {
        None,
        Pips,
        Percent,
        ATR,
        RiskMultiplier
    }

    public enum SLToBEType
    {
        None,
        Pips,
        Percent,
        ATR,
        RiskMultiplier
    }

    public enum TrailingSLType
    {
        None,
        Pips,
        Percent,
        ATR,
        RiskMultiplier
    }

    // --- Models ---

    public sealed class LiquidityZone
    {
        public double LevelPrice { get; set; }
        public double ZoneTop { get; set; }
        public double ZoneBottom { get; set; }
        public LiquiditySide Side { get; set; }
        public LiquiditySource Source { get; set; }
        public int TouchCount { get; set; }
        public int StrengthScore { get; set; }
        public DateTime FormedAt { get; set; }
        public int FormedBarIndex { get; set; }
        public bool IsActive { get; set; }
        public string Label { get; set; }

        public LiquidityZone()
        {
            IsActive = true;
        }
    }

    public sealed class SweepSetup
    {
        public LiquidityZone Zone { get; set; }
        public TradeType Direction { get; set; }
        public SweepPhase Phase { get; set; }
        public int SweepBarIndex { get; set; }
        public double SweepWickExtreme { get; set; }
        public int ConfirmationBarsRemaining { get; set; }
        public bool StructureShiftConfirmed { get; set; }
        public DateTime DetectedAt { get; set; }
    }

    public sealed class ManagedTradeState
    {
        public long PositionId { get; set; }
        public double EntryPrice { get; set; }
        public double InitialStopLoss { get; set; }
        public double RiskDistance { get; set; }
        public bool MovedToBreakeven { get; set; }
        public bool PartialTaken { get; set; }
        public double HighestPrice { get; set; }
        public double LowestPrice { get; set; }
    }

    public sealed class SwingPoint
    {
        public int BarIndex { get; set; }
        public double Price { get; set; }
        public bool IsHigh { get; set; }
        public DateTime OpenTime { get; set; }
    }

    public sealed class EntryMarker
    {
        public DateTime Time { get; set; }
        public double Price { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public TradeType Direction { get; set; }
    }

    public sealed class DayGroup
    {
        public double High { get; set; }
        public double Low { get; set; }
        public int LastBar { get; set; }
    }

    public sealed class WeekKey
    {
        public int Year { get; set; }
        public int Week { get; set; }

        public WeekKey(int year, int week)
        {
            Year = year;
            Week = week;
        }

        public int CompareTo(WeekKey other)
        {
            if (other == null)
                return 1;
            int yearCompare = Year.CompareTo(other.Year);
            if (yearCompare != 0)
                return yearCompare;
            return Week.CompareTo(other.Week);
        }

        public override bool Equals(object obj)
        {
            var other = obj as WeekKey;
            if (other == null)
                return false;
            return Year == other.Year && Week == other.Week;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Year * 397) ^ Week;
            }
        }
    }

    public sealed class WeekGroup
    {
        public double High { get; set; }
        public double Low { get; set; }
        public int LastBar { get; set; }
    }

    // --- SwingPointDetector ---

    public static class SwingPointDetector
    {
        public static List<SwingPoint> DetectConfirmedSwings(
            double[] highs,
            double[] lows,
            DateTime[] openTimes,
            int pivotBars,
            int upToBarIndexInclusive)
        {
            var swings = new List<SwingPoint>();
            if (highs == null || lows == null || highs.Length != lows.Length)
                return swings;

            int lastConfirmable = upToBarIndexInclusive - pivotBars;
            for (int i = pivotBars; i <= lastConfirmable; i++)
            {
                if (IsSwingHigh(highs, i, pivotBars))
                {
                    swings.Add(new SwingPoint
                    {
                        BarIndex = i,
                        Price = highs[i],
                        IsHigh = true,
                        OpenTime = openTimes != null && i < openTimes.Length ? openTimes[i] : DateTime.MinValue
                    });
                }

                if (IsSwingLow(lows, i, pivotBars))
                {
                    swings.Add(new SwingPoint
                    {
                        BarIndex = i,
                        Price = lows[i],
                        IsHigh = false,
                        OpenTime = openTimes != null && i < openTimes.Length ? openTimes[i] : DateTime.MinValue
                    });
                }
            }

            swings.Sort((a, b) => a.BarIndex.CompareTo(b.BarIndex));
            return swings;
        }

        public static bool IsSwingHigh(double[] highs, int index, int pivotBars)
        {
            double pivot = highs[index];
            for (int j = index - pivotBars; j <= index + pivotBars; j++)
            {
                if (j == index)
                    continue;
                if (highs[j] >= pivot)
                    return false;
            }
            return true;
        }

        public static bool IsSwingLow(double[] lows, int index, int pivotBars)
        {
            double pivot = lows[index];
            for (int j = index - pivotBars; j <= index + pivotBars; j++)
            {
                if (j == index)
                    continue;
                if (lows[j] <= pivot)
                    return false;
            }
            return true;
        }
    }

    // --- LiquidityZoneEngine ---

    public sealed class LiquidityZoneEngine
    {
        private readonly double _equalLevelToleranceAtrMultiplier;
        private readonly int _minEqualTouches;
        private readonly int _pivotBars;
        private readonly double _zonePaddingAtrMultiplier;
        private readonly bool _useSessionLevels;
        private readonly int _maxActiveZones;

        public LiquidityZoneEngine(
            int pivotBars,
            double equalLevelToleranceAtrMultiplier,
            int minEqualTouches,
            double zonePaddingAtrMultiplier,
            bool useSessionLevels,
            int maxActiveZones)
        {
            _pivotBars = Math.Max(2, pivotBars);
            _equalLevelToleranceAtrMultiplier = equalLevelToleranceAtrMultiplier;
            _minEqualTouches = Math.Max(2, minEqualTouches);
            _zonePaddingAtrMultiplier = zonePaddingAtrMultiplier;
            _useSessionLevels = useSessionLevels;
            _maxActiveZones = Math.Max(5, maxActiveZones);
        }

        public IReadOnlyList<LiquidityZone> BuildZones(
            double[] highs,
            double[] lows,
            double[] closes,
            DateTime[] openTimes,
            double[] atrValues,
            int lastClosedBarIndex)
        {
            if (lastClosedBarIndex < _pivotBars * 2 + 2)
                return new List<LiquidityZone>();

            var swings = SwingPointDetector.DetectConfirmedSwings(
                highs, lows, openTimes, _pivotBars, lastClosedBarIndex);

            double atr = atrValues[lastClosedBarIndex];
            if (atr <= 0)
                atr = EstimateAtr(highs, lows, closes, lastClosedBarIndex);

            double tolerance = atr * _equalLevelToleranceAtrMultiplier;
            double padding = atr * _zonePaddingAtrMultiplier;

            var zones = new List<LiquidityZone>();

            foreach (var swing in swings.Where(s => s.IsHigh))
            {
                zones.Add(CreateSwingZone(swing, LiquiditySide.BuySide, LiquiditySource.SwingHigh, padding, 1));
            }

            foreach (var swing in swings.Where(s => !s.IsHigh))
            {
                zones.Add(CreateSwingZone(swing, LiquiditySide.SellSide, LiquiditySource.SwingLow, padding, 1));
            }

            AddEqualLevelZones(swings.Where(s => s.IsHigh).ToList(), tolerance, padding, zones, true);
            AddEqualLevelZones(swings.Where(s => !s.IsHigh).ToList(), tolerance, padding, zones, false);

            if (_useSessionLevels)
                AddSessionLevels(highs, lows, openTimes, lastClosedBarIndex, padding, zones);

            return zones
                .Where(z => z.FormedBarIndex <= lastClosedBarIndex)
                .GroupBy(z => z.Source + ":" + Math.Round(z.LevelPrice, 5))
                .Select(g => g.OrderByDescending(z => z.StrengthScore).First())
                .OrderByDescending(z => z.StrengthScore)
                .ThenByDescending(z => z.FormedBarIndex)
                .Take(_maxActiveZones)
                .ToList();
        }

        private static LiquidityZone CreateSwingZone(
            SwingPoint swing,
            LiquiditySide side,
            LiquiditySource source,
            double padding,
            int touchCount)
        {
            return new LiquidityZone
            {
                LevelPrice = swing.Price,
                ZoneTop = swing.Price + padding,
                ZoneBottom = swing.Price - padding,
                Side = side,
                Source = source,
                TouchCount = touchCount,
                StrengthScore = ScoreStrength(source, touchCount),
                FormedAt = swing.OpenTime,
                FormedBarIndex = swing.BarIndex,
                Label = source.ToString()
            };
        }

        private void AddEqualLevelZones(
            List<SwingPoint> swings,
            double tolerance,
            double padding,
            List<LiquidityZone> zones,
            bool isHigh)
        {
            if (swings.Count < _minEqualTouches)
                return;

            var assigned = new bool[swings.Count];
            for (int i = 0; i < swings.Count; i++)
            {
                if (assigned[i])
                    continue;

                var cluster = new List<int> { i };
                for (int j = i + 1; j < swings.Count; j++)
                {
                    if (assigned[j])
                        continue;

                    if (Math.Abs(swings[j].Price - swings[i].Price) <= tolerance)
                        cluster.Add(j);
                }

                if (cluster.Count < _minEqualTouches)
                    continue;

                foreach (var idx in cluster)
                    assigned[idx] = true;

                double avg = cluster.Average(idx => swings[idx].Price);
                var last = swings[cluster[cluster.Count - 1]];

                zones.Add(new LiquidityZone
                {
                    LevelPrice = avg,
                    ZoneTop = avg + padding,
                    ZoneBottom = avg - padding,
                    Side = isHigh ? LiquiditySide.BuySide : LiquiditySide.SellSide,
                    Source = isHigh ? LiquiditySource.EqualHigh : LiquiditySource.EqualLow,
                    TouchCount = cluster.Count,
                    StrengthScore = ScoreStrength(isHigh ? LiquiditySource.EqualHigh : LiquiditySource.EqualLow, cluster.Count),
                    FormedAt = last.OpenTime,
                    FormedBarIndex = last.BarIndex,
                    Label = isHigh ? "Equal Highs" : "Equal Lows"
                });
            }
        }

        private static void AddSessionLevels(
            double[] highs,
            double[] lows,
            DateTime[] openTimes,
            int lastClosedBarIndex,
            double padding,
            List<LiquidityZone> zones)
        {
            if (openTimes == null || openTimes.Length == 0)
                return;

            var dayGroups = new Dictionary<DateTime, DayGroup>();
            var weekGroups = new Dictionary<WeekKey, WeekGroup>();

            for (int i = 0; i <= lastClosedBarIndex; i++)
            {
                var day = openTimes[i].Date;
                DayGroup dayVal;
                if (!dayGroups.TryGetValue(day, out dayVal))
                {
                    dayVal = new DayGroup { High = highs[i], Low = lows[i], LastBar = i };
                }
                else
                {
                    dayVal.High = Math.Max(dayVal.High, highs[i]);
                    dayVal.Low = Math.Min(dayVal.Low, lows[i]);
                    dayVal.LastBar = i;
                }
                dayGroups[day] = dayVal;

                var weekKey = GetIsoWeekKey(openTimes[i]);
                WeekGroup weekVal;
                if (!weekGroups.TryGetValue(weekKey, out weekVal))
                {
                    weekVal = new WeekGroup { High = highs[i], Low = lows[i], LastBar = i };
                }
                else
                {
                    weekVal.High = Math.Max(weekVal.High, highs[i]);
                    weekVal.Low = Math.Min(weekVal.Low, lows[i]);
                    weekVal.LastBar = i;
                }
                weekGroups[weekKey] = weekVal;
            }

            var currentDay = openTimes[lastClosedBarIndex].Date;
            var priorDays = dayGroups.Keys.Where(d => d < currentDay).OrderByDescending(d => d).Take(1).ToList();
            foreach (var day in priorDays)
            {
                var d = dayGroups[day];
                zones.Add(SessionZone(d.High, LiquiditySide.BuySide, LiquiditySource.PreviousDayHigh, padding, d.LastBar, openTimes[d.LastBar]));
                zones.Add(SessionZone(d.Low, LiquiditySide.SellSide, LiquiditySource.PreviousDayLow, padding, d.LastBar, openTimes[d.LastBar]));
            }

            var currentWeek = GetIsoWeekKey(openTimes[lastClosedBarIndex]);
            var priorWeeks = weekGroups.Keys.Where(w => w.CompareTo(currentWeek) < 0).OrderByDescending(w => w.Year).ThenByDescending(w => w.Week).Take(1).ToList();
            foreach (var week in priorWeeks)
            {
                var w = weekGroups[week];
                zones.Add(SessionZone(w.High, LiquiditySide.BuySide, LiquiditySource.PreviousWeekHigh, padding, w.LastBar, openTimes[w.LastBar]));
                zones.Add(SessionZone(w.Low, LiquiditySide.SellSide, LiquiditySource.PreviousWeekLow, padding, w.LastBar, openTimes[w.LastBar]));
            }
        }

        private static LiquidityZone SessionZone(
            double level,
            LiquiditySide side,
            LiquiditySource source,
            double padding,
            int barIndex,
            DateTime openTime)
        {
            return new LiquidityZone
            {
                LevelPrice = level,
                ZoneTop = level + padding,
                ZoneBottom = level - padding,
                Side = side,
                Source = source,
                TouchCount = 1,
                StrengthScore = ScoreStrength(source, 1),
                FormedBarIndex = barIndex,
                FormedAt = openTime,
                Label = source.ToString()
            };
        }

        private static int ScoreStrength(LiquiditySource source, int touchCount)
        {
            int baseScore;
            switch (source)
            {
                case LiquiditySource.EqualHigh:
                case LiquiditySource.EqualLow:
                    baseScore = 75;
                    break;
                case LiquiditySource.PreviousWeekHigh:
                case LiquiditySource.PreviousWeekLow:
                    baseScore = 70;
                    break;
                case LiquiditySource.PreviousDayHigh:
                case LiquiditySource.PreviousDayLow:
                    baseScore = 65;
                    break;
                case LiquiditySource.SwingHigh:
                case LiquiditySource.SwingLow:
                    baseScore = 50;
                    break;
                default:
                    baseScore = 40;
                    break;
            }

            return Math.Min(100, baseScore + (touchCount - 1) * 8);
        }

        private static WeekKey GetIsoWeekKey(DateTime date)
        {
            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            int week = cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return new WeekKey(date.Year, week);
        }

        private static double EstimateAtr(double[] highs, double[] lows, double[] closes, int index)
        {
            int start = Math.Max(1, index - 14);
            double sum = 0;
            int count = 0;
            for (int i = start; i <= index; i++)
            {
                double tr = Math.Max(highs[i] - lows[i],
                    Math.Max(Math.Abs(highs[i] - closes[i - 1]), Math.Abs(lows[i] - closes[i - 1])));
                sum += tr;
                count++;
            }
            return count > 0 ? sum / count : highs[index] - lows[index];
        }
    }

    // --- SweepConfirmationEngine ---

    public sealed class SweepConfirmationEngine
    {
        private readonly int _confirmationBarDelay;
        private readonly int _maxSweepAgeBars;
        private readonly bool _requireStructureShift;
        private readonly int _structurePivotBars;
        private readonly double _minWickToBodyRatio;
        private readonly int _minZoneStrength;

        public SweepConfirmationEngine(
            int confirmationBarDelay,
            int maxSweepAgeBars,
            bool requireStructureShift,
            int structurePivotBars,
            double minWickToBodyRatio,
            int minZoneStrength)
        {
            _confirmationBarDelay = Math.Max(0, confirmationBarDelay);
            _maxSweepAgeBars = Math.Max(3, maxSweepAgeBars);
            _requireStructureShift = requireStructureShift;
            _structurePivotBars = Math.Max(2, structurePivotBars);
            _minWickToBodyRatio = minWickToBodyRatio;
            _minZoneStrength = minZoneStrength;
        }

        public List<SweepSetup> UpdateSetups(
            IReadOnlyList<LiquidityZone> zones,
            double[] opens,
            double[] highs,
            double[] lows,
            double[] closes,
            int lastClosedBarIndex,
            List<SweepSetup> existingSetups)
        {
            var setups = existingSetups ?? new List<SweepSetup>();
            PurgeExpired(setups, lastClosedBarIndex);

            if (lastClosedBarIndex < 1)
                return setups;

            int bar = lastClosedBarIndex;
            double open = opens[bar];
            double high = highs[bar];
            double low = lows[bar];
            double close = closes[bar];
            double body = Math.Abs(close - open);

            foreach (var zone in zones.Where(z => z.IsActive && z.StrengthScore >= _minZoneStrength))
            {
                if (setups.Any(s => s.Zone.LevelPrice == zone.LevelPrice && s.Zone.Source == zone.Source && s.Phase != SweepPhase.Invalidated))
                    continue;

                if (zone.Side == LiquiditySide.SellSide)
                {
                    TryDetectBullishSweep(zone, open, high, low, close, body, bar, setups, highs, lows, closes);
                }
                else
                {
                    TryDetectBearishSweep(zone, open, high, low, close, body, bar, setups, highs, lows, closes);
                }
            }

            AdvanceConfirmation(setups, opens, highs, lows, closes, lastClosedBarIndex);
            return setups;
        }

        public IReadOnlyList<SweepSetup> GetReadyEntries(IEnumerable<SweepSetup> setups)
        {
            return setups
                .Where(s => s.Phase == SweepPhase.Confirmed && s.ConfirmationBarsRemaining <= 0)
                .Where(s => !_requireStructureShift || s.StructureShiftConfirmed)
                .ToList();
        }

        private void TryDetectBullishSweep(
            LiquidityZone zone,
            double open,
            double high,
            double low,
            double close,
            double body,
            int bar,
            List<SweepSetup> setups,
            double[] highs,
            double[] lows,
            double[] closes)
        {
            if (low >= zone.LevelPrice)
                return;

            bool closedBackInside = close > zone.LevelPrice;
            if (!closedBackInside)
                return;

            double lowerWick = Math.Min(open, close) - low;
            if (body > 0 && lowerWick / body < _minWickToBodyRatio)
                return;

            setups.Add(new SweepSetup
            {
                Zone = zone,
                Direction = TradeType.Buy,
                Phase = SweepPhase.Swept,
                SweepBarIndex = bar,
                SweepWickExtreme = low,
                ConfirmationBarsRemaining = _confirmationBarDelay,
                StructureShiftConfirmed = !_requireStructureShift || HasBullishStructureShift(highs, lows, closes, bar),
                DetectedAt = DateTime.UtcNow
            });
        }

        private void TryDetectBearishSweep(
            LiquidityZone zone,
            double open,
            double high,
            double low,
            double close,
            double body,
            int bar,
            List<SweepSetup> setups,
            double[] highs,
            double[] lows,
            double[] closes)
        {
            if (high <= zone.LevelPrice)
                return;

            bool closedBackInside = close < zone.LevelPrice;
            if (!closedBackInside)
                return;

            double upperWick = high - Math.Max(open, close);
            if (body > 0 && upperWick / body < _minWickToBodyRatio)
                return;

            setups.Add(new SweepSetup
            {
                Zone = zone,
                Direction = TradeType.Sell,
                Phase = SweepPhase.Swept,
                SweepBarIndex = bar,
                SweepWickExtreme = high,
                ConfirmationBarsRemaining = _confirmationBarDelay,
                StructureShiftConfirmed = !_requireStructureShift || HasBearishStructureShift(highs, lows, closes, bar),
                DetectedAt = DateTime.UtcNow
            });
        }

        private void AdvanceConfirmation(
            List<SweepSetup> setups,
            double[] opens,
            double[] highs,
            double[] lows,
            double[] closes,
            int bar)
        {
            foreach (var setup in setups.Where(s => s.Phase == SweepPhase.Swept || s.Phase == SweepPhase.Confirmed))
            {
                if (bar <= setup.SweepBarIndex)
                    continue;

                if (setup.Direction == TradeType.Buy)
                {
                    if (closes[bar] < setup.Zone.LevelPrice)
                    {
                        setup.Phase = SweepPhase.Invalidated;
                        continue;
                    }
                }
                else if (closes[bar] > setup.Zone.LevelPrice)
                {
                    setup.Phase = SweepPhase.Invalidated;
                    continue;
                }

                if (_requireStructureShift && !setup.StructureShiftConfirmed)
                {
                    setup.StructureShiftConfirmed = setup.Direction == TradeType.Buy
                        ? HasBullishStructureShift(highs, lows, closes, bar)
                        : HasBearishStructureShift(highs, lows, closes, bar);
                }

                if (setup.Phase == SweepPhase.Swept)
                {
                    setup.Phase = SweepPhase.Confirmed;
                }

                if (setup.ConfirmationBarsRemaining > 0)
                    setup.ConfirmationBarsRemaining--;
            }
        }

        private bool HasBullishStructureShift(double[] highs, double[] lows, double[] closes, int bar)
        {
            var swings = SwingPointDetector.DetectConfirmedSwings(highs, lows, null, _structurePivotBars, bar);
            var lastHigh = swings.LastOrDefault(s => s.IsHigh && s.BarIndex < bar);
            if (lastHigh == null)
                return false;
            return closes[bar] > lastHigh.Price;
        }

        private bool HasBearishStructureShift(double[] highs, double[] lows, double[] closes, int bar)
        {
            var swings = SwingPointDetector.DetectConfirmedSwings(highs, lows, null, _structurePivotBars, bar);
            var lastLow = swings.LastOrDefault(s => !s.IsHigh && s.BarIndex < bar);
            if (lastLow == null)
                return false;
            return closes[bar] < lastLow.Price;
        }

        private void PurgeExpired(List<SweepSetup> setups, int bar)
        {
            setups.RemoveAll(s =>
                s.Phase == SweepPhase.Invalidated ||
                (s.Phase != SweepPhase.None && bar - s.SweepBarIndex > _maxSweepAgeBars));
        }
    }

    // --- RiskManager ---

    public sealed class RiskManager
    {
        private readonly RiskMode _mode;
        private readonly double _fixedUsdRisk;
        private readonly double _riskPercent;
        private readonly double _maxSpreadPips;
        private readonly double _stopBufferAtrMultiplier;

        public RiskManager(
            RiskMode mode,
            double fixedUsdRisk,
            double riskPercent,
            double maxSpreadPips,
            double stopBufferAtrMultiplier)
        {
            _mode = mode;
            _fixedUsdRisk = Math.Max(1, fixedUsdRisk);
            _riskPercent = Math.Max(0.01, riskPercent);
            _maxSpreadPips = maxSpreadPips;
            _stopBufferAtrMultiplier = stopBufferAtrMultiplier;
        }

        public bool IsSpreadAcceptable(Symbol symbol)
        {
            if (_maxSpreadPips <= 0)
                return true;
            return symbol.Spread / symbol.PipSize <= _maxSpreadPips;
        }

        public double CalculateRiskAmount(IAccount account)
        {
            switch (_mode)
            {
                case RiskMode.FixedUsd:
                    return _fixedUsdRisk;
                case RiskMode.PercentEquity:
                    return account.Equity * _riskPercent / 100.0;
                case RiskMode.PercentBalance:
                    return account.Balance * _riskPercent / 100.0;
                default:
                    return _fixedUsdRisk;
            }
        }

        public double BuildStopLoss(TradeType direction, double sweepWickExtreme, double atr, Symbol symbol)
        {
            double buffer = atr * _stopBufferAtrMultiplier;
            double stop = direction == TradeType.Buy
                ? sweepWickExtreme - buffer
                : sweepWickExtreme + buffer;

            return SymbolHelper.NormalizePrice(symbol, stop);
        }

        public double CalculateVolumeInUnits(Symbol symbol, double entryPrice, double stopLoss, double riskAmount)
        {
            double stopDistance = Math.Abs(entryPrice - stopLoss);
            if (stopDistance <= 0)
                return symbol.VolumeInUnitsMin;

            double amountRiskedPerUnit = stopDistance * symbol.TickValue / symbol.TickSize;
            if (amountRiskedPerUnit <= 0)
                return symbol.VolumeInUnitsMin;

            double rawVolume = riskAmount / amountRiskedPerUnit;
            rawVolume = symbol.NormalizeVolumeInUnits(rawVolume, RoundingMode.Down);

            if (rawVolume < symbol.VolumeInUnitsMin)
                return 0;

            if (rawVolume > symbol.VolumeInUnitsMax)
                rawVolume = symbol.VolumeInUnitsMax;

            return rawVolume;
        }
    }

    // --- ProfitManager ---

    public sealed class ProfitManager
    {
        private readonly double _rewardRiskRatio;
        private readonly bool _moveToBreakevenAt1R;
        private readonly bool _partialCloseAt1R;
        private readonly double _partialClosePercent;
        private readonly bool _useTrailingStop;
        private readonly double _trailingStopPercent;
        private readonly double _activateTrailAfterR;

        private readonly Dictionary<long, ManagedTradeState> _states = new Dictionary<long, ManagedTradeState>();

        public ProfitManager(
            double rewardRiskRatio,
            bool moveToBreakevenAt1R,
            bool partialCloseAt1R,
            double partialClosePercent,
            bool useTrailingStop,
            double trailingStopPercent,
            double activateTrailAfterR)
        {
            _rewardRiskRatio = Math.Max(0.5, rewardRiskRatio);
            _moveToBreakevenAt1R = moveToBreakevenAt1R;
            _partialCloseAt1R = partialCloseAt1R;
            _partialClosePercent = Math.Min(90, Math.Max(10, partialClosePercent));
            _useTrailingStop = useTrailingStop;
            _trailingStopPercent = Math.Max(0.1, trailingStopPercent);
            _activateTrailAfterR = Math.Max(0.5, activateTrailAfterR);
        }

        public void RegisterPosition(Position position, double initialStopLoss)
        {
            double riskDistance = Math.Abs(position.EntryPrice - initialStopLoss);
            if (riskDistance <= 0)
                return;

            _states[position.Id] = new ManagedTradeState
            {
                PositionId = position.Id,
                EntryPrice = position.EntryPrice,
                InitialStopLoss = initialStopLoss,
                RiskDistance = riskDistance,
                HighestPrice = position.EntryPrice,
                LowestPrice = position.EntryPrice
            };
        }

        public void ManageOpenPositions(Robot robot, Symbol symbol)
        {
            foreach (var position in robot.Positions.Where(p => p.SymbolName == symbol.Name))
            {
                ManagedTradeState state;
                if (!_states.TryGetValue(position.Id, out state))
                    continue;

                double price = position.TradeType == TradeType.Buy ? symbol.Bid : symbol.Ask;
                state.HighestPrice = Math.Max(state.HighestPrice, price);
                state.LowestPrice = Math.Min(state.LowestPrice, price);

                double profitR = GetProfitInR(position, state, price);
                double? newStop = null;

                if (_moveToBreakevenAt1R && !state.MovedToBreakeven && profitR >= 1.0)
                {
                    newStop = state.EntryPrice;
                    state.MovedToBreakeven = true;
                }

                if (_partialCloseAt1R && !state.PartialTaken && profitR >= 1.0)
                {
                    double closeVolume = symbol.NormalizeVolumeInUnits(
                        position.VolumeInUnits * (_partialClosePercent / 100.0),
                        RoundingMode.Down);

                    if (closeVolume >= symbol.VolumeInUnitsMin && closeVolume < position.VolumeInUnits)
                    {
                        robot.ClosePosition(position, closeVolume);
                        state.PartialTaken = true;
                    }
                }

                if (_useTrailingStop && profitR >= _activateTrailAfterR)
                {
                    double trailDistance = price * (_trailingStopPercent / 100.0);
                    double trailStop = position.TradeType == TradeType.Buy
                        ? state.HighestPrice - trailDistance
                        : state.LowestPrice + trailDistance;

                    trailStop = SymbolHelper.NormalizePrice(symbol, trailStop);
                    newStop = BetterStop(position, newStop, trailStop);
                }

                if (newStop.HasValue && IsImprovement(position, newStop.Value))
                {
                    robot.ModifyPosition(position, newStop.Value, position.TakeProfit, ProtectionType.Absolute);
                }
            }

            CleanupClosed(robot);
        }

        public double CalculateTakeProfit(TradeType direction, double entryPrice, double stopLoss)
        {
            double risk = Math.Abs(entryPrice - stopLoss);
            return direction == TradeType.Buy
                ? entryPrice + risk * _rewardRiskRatio
                : entryPrice - risk * _rewardRiskRatio;
        }

        private static double GetProfitInR(Position position, ManagedTradeState state, double price)
        {
            double move = position.TradeType == TradeType.Buy
                ? price - state.EntryPrice
                : state.EntryPrice - price;
            return move / state.RiskDistance;
        }

        private static double? BetterStop(Position position, double? current, double candidate)
        {
            if (!current.HasValue)
                return candidate;

            if (position.TradeType == TradeType.Buy)
                return Math.Max(current.Value, candidate);

            return Math.Min(current.Value, candidate);
        }

        private static bool IsImprovement(Position position, double newStop)
        {
            if (!position.StopLoss.HasValue)
                return true;

            return position.TradeType == TradeType.Buy
                ? newStop > position.StopLoss.Value
                : newStop < position.StopLoss.Value;
        }

        private void CleanupClosed(Robot robot)
        {
            var openIds = new HashSet<long>();
            foreach (var position in robot.Positions)
                openIds.Add(position.Id);

            var keysToRemove = new List<long>();
            foreach (var id in _states.Keys)
            {
                if (!openIds.Contains(id))
                    keysToRemove.Add(id);
            }

            foreach (var id in keysToRemove)
                _states.Remove(id);
        }
    }

    // --- ChartVisualizer ---

    public sealed class ChartVisualizer
    {
        private const string Prefix = "LQ_";
        private readonly HashSet<string> _trackedObjects = new HashSet<string>();

        public int DrawZonesAndSetups(
            Chart chart,
            Bars chartBars,
            Symbol symbol,
            TimeFrame zoneTimeFrame,
            IReadOnlyList<LiquidityZone> zones,
            Bars entryBars,
            IEnumerable<SweepSetup> setups,
            int maxZones,
            int lookbackBars,
            bool showLabels,
            bool showSweepMarkers,
            bool showEntryMarkers,
            IReadOnlyList<EntryMarker> recentEntries)
        {
            Clear(chart);

            if (chartBars.Count < 2)
            {
                DrawStatusBadge(chart, zoneTimeFrame, 0, 0, "Waiting for bars...");
                return 0;
            }

            ResolveDrawRange(chartBars, lookbackBars, out int startIndex, out int endIndex);

            int drawn = 0;
            if (zones != null)
            {
                foreach (var zone in zones.OrderByDescending(z => z.StrengthScore).Take(maxZones))
                {
                    DrawZone(chart, chartBars, symbol, zone, startIndex, endIndex, showLabels);
                    drawn++;
                }
            }

            if (showSweepMarkers && entryBars != null)
            {
                foreach (var setup in setups.Where(s => s.Phase == SweepPhase.Swept || s.Phase == SweepPhase.Confirmed))
                {
                    DrawSweepMarker(chart, chartBars, entryBars, symbol, setup);
                }
            }

            if (showEntryMarkers)
            {
                foreach (var entry in recentEntries)
                    DrawEntryMarker(chart, symbol, entry);
            }

            int detected = zones != null ? zones.Count : 0;
            string status = detected == 0
                ? "No zones detected yet — check H4 history loaded"
                : "Red=BSL (short sweep) | Blue=SSL (long sweep)";
            DrawStatusBadge(chart, zoneTimeFrame, drawn, detected, status);
            return drawn;
        }

        public void DrawTestPattern(Chart chart, Bars chartBars, Symbol symbol)
        {
            if (chartBars.Count < 3)
                return;

            int end = chartBars.Count - 1;
            int start = Math.Max(0, end - 30);
            double mid = symbol.Bid > 0 ? symbol.Bid : chartBars.ClosePrices[end];
            double half = Math.Max(symbol.PipSize * 100, mid * 0.002);

            TrackDraw(Prefix + "test_rect");
            var testRect = chart.DrawRectangle(
                Prefix + "test_rect",
                start,
                mid + half,
                end,
                mid - half,
                Color.FromArgb(60, 255, 255, 0),
                2,
                LineStyle.Solid);
            ConfigureShape(testRect, Color.FromArgb(60, 255, 255, 0), 3000);

            TrackDraw(Prefix + "test_line");
            var testLine = chart.DrawHorizontalLine(Prefix + "test_line", mid, Color.Yellow, 2, LineStyle.Solid);
            ConfigureShape(testLine, Color.Yellow, 3000);
        }

        public void DrawHeartbeat(Chart chart, TimeFrame zoneTimeFrame, int zoneBarCount)
        {
            Clear(chart);
            DrawStatusBadge(
                chart,
                zoneTimeFrame,
                0,
                0,
                "Bot ACTIVE | zone TF bars: " + zoneBarCount + " | waiting for zones...");
        }

        private static void ResolveDrawRange(Bars chartBars, int lookbackBars, out int startIndex, out int endIndex)
        {
            endIndex = Math.Max(1, chartBars.Count - 1);
            startIndex = Math.Max(0, endIndex - lookbackBars);
            if (endIndex <= startIndex)
                endIndex = Math.Min(chartBars.Count - 1, startIndex + 1);
        }

        private void DrawZone(
            Chart chart,
            Bars chartBars,
            Symbol symbol,
            LiquidityZone zone,
            int startIndex,
            int endIndex,
            bool showLabels)
        {
            string id = ZoneId(zone);
            bool isBsl = zone.Side == LiquiditySide.BuySide;
            Color fill = isBsl ? Color.FromArgb(90, 231, 76, 60) : Color.FromArgb(90, 52, 152, 219);
            Color line = isBsl ? Color.FromArgb(255, 231, 76, 60) : Color.FromArgb(255, 52, 152, 219);
            Color labelColor = isBsl ? Color.OrangeRed : Color.DeepSkyBlue;

            int zoneStart = FindChartBarIndex(chartBars, zone.FormedAt);
            if (zoneStart < 0)
                zoneStart = startIndex;
            int drawStart = Math.Max(startIndex, zoneStart);
            if (drawStart >= endIndex)
                drawStart = Math.Max(0, endIndex - Math.Max(10, (endIndex - startIndex) / 4));

            double top = Math.Max(zone.ZoneTop, zone.ZoneBottom);
            double bottom = Math.Min(zone.ZoneTop, zone.ZoneBottom);
            double minHeight = Math.Max(symbol.PipSize * 40, zone.LevelPrice * 0.001);
            if (top - bottom < minHeight)
            {
                double mid = zone.LevelPrice;
                top = mid + minHeight / 2.0;
                bottom = mid - minHeight / 2.0;
            }

            DateTime tStart = chartBars.OpenTimes[drawStart];
            DateTime tEnd = chartBars.OpenTimes[endIndex];

            TrackDraw(Prefix + "rect_" + id);
            var rect = chart.DrawRectangle(
                Prefix + "rect_" + id,
                drawStart,
                top,
                endIndex,
                bottom,
                fill,
                2,
                LineStyle.Solid);
            ConfigureShape(rect, fill, 1000);

            TrackDraw(Prefix + "rectT_" + id);
            var rectTime = chart.DrawRectangle(
                Prefix + "rectT_" + id,
                tStart,
                top,
                tEnd,
                bottom,
                fill,
                1,
                LineStyle.Solid);
            ConfigureShape(rectTime, fill, 999);

            TrackDraw(Prefix + "lvl_" + id);
            var levelLine = chart.DrawTrendLine(
                Prefix + "lvl_" + id,
                drawStart,
                zone.LevelPrice,
                endIndex,
                zone.LevelPrice,
                line,
                2,
                LineStyle.Solid);
            ConfigureShape(levelLine, line, 1001);

            TrackDraw(Prefix + "hlvl_" + id);
            var hLine = chart.DrawHorizontalLine(
                Prefix + "hlvl_" + id,
                zone.LevelPrice,
                line,
                2,
                LineStyle.Solid);
            ConfigureShape(hLine, line, 1001);

            if (showLabels)
            {
                string sideTag = isBsl ? "BSL" : "SSL";
                string text = sideTag + " " + ShortSource(zone.Source) + " @ " + Math.Round(zone.LevelPrice, symbol.Digits) + "  [" + zone.StrengthScore + "]";
                TrackDraw(Prefix + "lbl_" + id);
                var label = chart.DrawText(
                    Prefix + "lbl_" + id,
                    text,
                    endIndex,
                    zone.LevelPrice,
                    labelColor);
                ConfigureShape(label, labelColor, 1002);
            }
        }

        private static void ConfigureShape(ChartObject obj, Color color, int zIndex)
        {
            if (obj == null)
                return;

            obj.IsInteractive = true;
            obj.IsHidden = false;
            obj.ZIndex = zIndex;

            var rectangle = obj as ChartRectangle;
            if (rectangle != null)
            {
                rectangle.IsFilled = true;
                rectangle.Color = color;
            }
        }

        private void DrawSweepMarker(Chart chart, Bars chartBars, Bars entryBars, Symbol symbol, SweepSetup setup)
        {
            if (setup.SweepBarIndex < 0 || setup.SweepBarIndex >= entryBars.Count)
                return;

            int chartIndex = FindChartBarIndex(chartBars, entryBars.OpenTimes[setup.SweepBarIndex]);
            if (chartIndex < 0)
                return;

            double price = setup.SweepWickExtreme;
            bool isLong = setup.Direction == TradeType.Buy;
            Color color = isLong ? Color.LimeGreen : Color.OrangeRed;
            string phase = setup.Phase == SweepPhase.Confirmed ? "SWEEP OK" : "SWEEP";
            string sweepKey = setup.SweepBarIndex + "_" + setup.Zone.LevelPrice.ToString("F5");

            TrackDraw(Prefix + "sweep_" + sweepKey);
            var icon = chart.DrawIcon(
                Prefix + "sweep_" + sweepKey,
                isLong ? ChartIconType.UpTriangle : ChartIconType.DownTriangle,
                chartIndex,
                price,
                color);
            ConfigureShape(icon, color, 1002);

            TrackDraw(Prefix + "sweep_txt_" + sweepKey);
            var label = chart.DrawText(
                Prefix + "sweep_txt_" + sweepKey,
                phase,
                chartIndex,
                price + (isLong ? -symbol.PipSize * 8 : symbol.PipSize * 8),
                color);
            ConfigureShape(label, color, 1002);
        }

        private static int FindChartBarIndex(Bars chartBars, DateTime time)
        {
            for (int i = chartBars.Count - 1; i >= 0; i--)
            {
                if (chartBars.OpenTimes[i] <= time)
                    return i;
            }
            return chartBars.Count > 0 ? 0 : -1;
        }

        private void DrawEntryMarker(Chart chart, Symbol symbol, EntryMarker entry)
        {
            bool isLong = entry.Direction == TradeType.Buy;
            Color color = isLong ? Color.DodgerBlue : Color.MediumVioletRed;

            string entryKey = entry.Time.Ticks.ToString();

            TrackDraw(Prefix + "entry_" + entryKey);
            var icon = chart.DrawIcon(
                Prefix + "entry_" + entryKey,
                isLong ? ChartIconType.UpArrow : ChartIconType.DownArrow,
                entry.Time,
                entry.Price,
                color);
            ConfigureShape(icon, color, 1002);

            TrackDraw(Prefix + "entry_txt_" + entryKey);
            var label = chart.DrawText(
                Prefix + "entry_txt_" + entryKey,
                "ENTRY " + (isLong ? "LONG" : "SHORT"),
                entry.Time,
                entry.Price + (isLong ? symbol.PipSize * 10 : -symbol.PipSize * 10),
                color);
            ConfigureShape(label, color, 1002);

            if (entry.StopLoss > 0)
            {
                TrackDraw(Prefix + "entry_sl_" + entryKey);
                var slLine = chart.DrawHorizontalLine(
                    Prefix + "entry_sl_" + entryKey,
                    entry.StopLoss,
                    Color.FromArgb(160, 231, 76, 60),
                    1,
                    LineStyle.Dots);
                ConfigureShape(slLine, Color.FromArgb(160, 231, 76, 60), 1002);
            }

            if (entry.TakeProfit > 0)
            {
                TrackDraw(Prefix + "entry_tp_" + entryKey);
                var tpLine = chart.DrawHorizontalLine(
                    Prefix + "entry_tp_" + entryKey,
                    entry.TakeProfit,
                    Color.FromArgb(160, 46, 204, 113),
                    1,
                    LineStyle.Dots);
                ConfigureShape(tpLine, Color.FromArgb(160, 46, 204, 113), 1002);
            }
        }

        private void DrawStatusBadge(Chart chart, TimeFrame zoneTimeFrame, int zoneCount, int detectedCount, string statusLine)
        {
            TrackDraw(Prefix + "tf_badge");
            var badge = chart.DrawStaticText(
                Prefix + "tf_badge",
                "Liquidity " + FormatTimeFrame(zoneTimeFrame) + " | drawn: " + zoneCount + " / detected: " + detectedCount,
                VerticalAlignment.Top,
                HorizontalAlignment.Left,
                Color.Gold);
            ConfigureShape(badge, Color.Gold, 2000);

            TrackDraw(Prefix + "tf_status");
            var status = chart.DrawStaticText(
                Prefix + "tf_status",
                statusLine,
                VerticalAlignment.Top,
                HorizontalAlignment.Right,
                Color.Gold);
            ConfigureShape(status, Color.Gold, 2000);

            TrackDraw(Prefix + "tf_badge2");
            var badge2 = chart.DrawStaticText(
                Prefix + "tf_badge2",
                "Liquidity Sweep Bot",
                VerticalAlignment.Top,
                HorizontalAlignment.Center,
                Color.White);
            ConfigureShape(badge2, Color.White, 2000);
        }

        public void Clear(Chart chart)
        {
            var toRemove = new List<string>();
            foreach (var obj in chart.Objects)
            {
                if (obj.Name != null && obj.Name.StartsWith(Prefix))
                    toRemove.Add(obj.Name);
            }
            foreach (var name in toRemove)
                chart.RemoveObject(name);
            _trackedObjects.Clear();
        }

        private void TrackDraw(string name)
        {
            _trackedObjects.Add(name);
        }

        private static string ZoneId(LiquidityZone zone)
        {
            return (zone.Source + "_" + zone.LevelPrice.ToString("F5")).Replace('.', '_');
        }

        private static string ShortSource(LiquiditySource source)
        {
            switch (source)
            {
                case LiquiditySource.EqualHigh:
                    return "EQH";
                case LiquiditySource.EqualLow:
                    return "EQL";
                case LiquiditySource.PreviousDayHigh:
                    return "PDH";
                case LiquiditySource.PreviousDayLow:
                    return "PDL";
                case LiquiditySource.PreviousWeekHigh:
                    return "PWH";
                case LiquiditySource.PreviousWeekLow:
                    return "PWL";
                case LiquiditySource.SwingHigh:
                    return "SwingH";
                case LiquiditySource.SwingLow:
                    return "SwingL";
                default:
                    return source.ToString();
            }
        }

        private static string FormatTimeFrame(TimeFrame tf)
        {
            if (tf == TimeFrame.Minute) return "M1";
            if (tf == TimeFrame.Minute5) return "M5";
            if (tf == TimeFrame.Minute15) return "M15";
            if (tf == TimeFrame.Minute30) return "M30";
            if (tf == TimeFrame.Hour) return "H1";
            if (tf == TimeFrame.Hour4) return "H4";
            if (tf == TimeFrame.Daily) return "D1";
            if (tf == TimeFrame.Weekly) return "W1";
            if (tf == TimeFrame.Monthly) return "MN1";
            return tf.ToString();
        }
    }

    // --- SymbolTradingContext ---

    public sealed class SymbolTradingContext
    {
        public SymbolTradingContext(
            Symbol symbol,
            Bars zoneBars,
            Bars entryBars,
            AverageTrueRange zoneAtr,
            AverageTrueRange entryAtr,
            LiquidityZoneEngine zoneEngine,
            SweepConfirmationEngine sweepEngine)
        {
            Symbol = symbol;
            ZoneBars = zoneBars;
            EntryBars = entryBars;
            ZoneAtr = zoneAtr;
            EntryAtr = entryAtr;
            ZoneEngine = zoneEngine;
            SweepEngine = sweepEngine;
            ActiveSetups = new List<SweepSetup>();
            LastZones = new List<LiquidityZone>();
        }

        public Symbol Symbol { get; private set; }
        public Bars ZoneBars { get; private set; }
        public Bars EntryBars { get; private set; }
        public AverageTrueRange ZoneAtr { get; private set; }
        public AverageTrueRange EntryAtr { get; private set; }
        public LiquidityZoneEngine ZoneEngine { get; private set; }
        public SweepConfirmationEngine SweepEngine { get; private set; }
        public List<SweepSetup> ActiveSetups { get; private set; }
        public IReadOnlyList<LiquidityZone> LastZones { get; private set; }

        public static int GetLastClosedBarIndex(Bars bars)
        {
            if (bars.Count >= 2)
                return bars.Count - 2;
            if (bars.Count == 1)
                return 0;
            return -1;
        }

        public void RefreshZones()
        {
            if (ZoneBars.Count < 5)
                return;

            int zoneLast = GetLastClosedBarIndex(ZoneBars);
            if (zoneLast < 0)
                zoneLast = ZoneBars.Count - 1;
            if (zoneLast < 5)
                return;

            var atr = ToArray(ZoneAtr.Result);
            if (atr.Length <= zoneLast)
                zoneLast = Math.Min(zoneLast, atr.Length - 1);
            if (zoneLast < 5)
                return;

            LastZones = ZoneEngine.BuildZones(
                ToArray(ZoneBars.HighPrices),
                ToArray(ZoneBars.LowPrices),
                ToArray(ZoneBars.ClosePrices),
                ToArray(ZoneBars.OpenTimes),
                atr,
                zoneLast);
        }

        public void RefreshEntrySetups()
        {
            int entryLast = GetLastClosedBarIndex(EntryBars);
            if (entryLast < 5 || LastZones.Count == 0)
                return;

            ActiveSetups = SweepEngine.UpdateSetups(
                LastZones,
                ToArray(EntryBars.OpenPrices),
                ToArray(EntryBars.HighPrices),
                ToArray(EntryBars.LowPrices),
                ToArray(EntryBars.ClosePrices),
                entryLast,
                ActiveSetups);
        }

        public void EvaluateAtBarOpen(bool isZoneBarEvent)
        {
            if (isZoneBarEvent)
                RefreshZones();
            RefreshEntrySetups();
        }

        private static double[] ToArray(DataSeries series)
        {
            var data = new double[series.Count];
            for (int i = 0; i < series.Count; i++)
                data[i] = series[i];
            return data;
        }

        private static DateTime[] ToArray(TimeSeries times)
        {
            var data = new DateTime[times.Count];
            for (int i = 0; i < times.Count; i++)
                data[i] = times[i];
            return data;
        }
    }

    // --- Algo trader preset (HTF liquidity / LTF execution model) ---

    /// <summary>
    /// Research-backed defaults used when "Use Chart Timeframe" toggles are OFF.
    /// Model: H4 (or D1) liquidity map + M15 sweep confirmation — standard ICT/SMC algo split
    /// used by systematic crypto and FX liquidity bots (4:1 to 16:1 HTF:LTF ratio).
    /// </summary>
    internal sealed class AlgoEffectiveConfig
    {
        public TimeFrame ZoneTimeFrame;
        public TimeFrame EntryTimeFrame;
        public int PivotBars;
        public double EqualLevelToleranceAtr;
        public int MinEqualTouches;
        public double ZonePaddingAtr;
        public bool UseSessionLevels;
        public int MinZoneStrength;
        public int MaxActiveZones;
        public int ConfirmationBarDelay;
        public int MaxSweepAgeBars;
        public bool RequireStructureShift;
        public int StructurePivotBars;
        public double MinWickBodyRatio;
        public double StopBufferAtr;
        public double RiskPercent;
        public double MaxSpreadPips;
        public bool PresetActive;
    }

    internal static class AlgoTraderPresets
    {
        public static AlgoEffectiveConfig Resolve(
            bool applyPreset,
            bool isOptimizing,
            bool useChartForZones,
            bool useChartForEntry,
            TimeFrame chartTimeFrame,
            TimeFrame zoneTimeFrameParam,
            TimeFrame entryTimeFrameParam,
            int pivotBars,
            double equalLevelToleranceAtr,
            int minEqualTouches,
            double zonePaddingAtr,
            bool useSessionLevels,
            int minZoneStrength,
            int maxActiveZones,
            int confirmationBarDelay,
            int maxSweepAgeBars,
            bool requireStructureShift,
            int structurePivotBars,
            double minWickBodyRatio,
            double stopBufferAtr,
            double riskPercent,
            double maxSpreadPips)
        {
            var config = new AlgoEffectiveConfig
            {
                ZoneTimeFrame = useChartForZones ? chartTimeFrame : zoneTimeFrameParam,
                EntryTimeFrame = useChartForEntry ? chartTimeFrame : entryTimeFrameParam,
                PivotBars = pivotBars,
                EqualLevelToleranceAtr = equalLevelToleranceAtr,
                MinEqualTouches = minEqualTouches,
                ZonePaddingAtr = zonePaddingAtr,
                UseSessionLevels = useSessionLevels,
                MinZoneStrength = minZoneStrength,
                MaxActiveZones = maxActiveZones,
                ConfirmationBarDelay = confirmationBarDelay,
                MaxSweepAgeBars = maxSweepAgeBars,
                RequireStructureShift = requireStructureShift,
                StructurePivotBars = structurePivotBars,
                MinWickBodyRatio = minWickBodyRatio,
                StopBufferAtr = stopBufferAtr,
                RiskPercent = riskPercent,
                MaxSpreadPips = maxSpreadPips,
                PresetActive = false
            };

            bool algoTimeframeMode = !useChartForZones || !useChartForEntry;

            // During optimisation each pass sets parameters — never override them with the live preset.
            if (isOptimizing)
            {
                if (applyPreset && algoTimeframeMode)
                {
                    if (!useChartForZones)
                        config.ZoneTimeFrame = TimeFrame.Hour4;
                    if (!useChartForEntry)
                        config.EntryTimeFrame = TimeFrame.Minute15;
                }
                return config;
            }

            if (!applyPreset || !algoTimeframeMode)
                return config;

            config.PresetActive = true;

            if (!useChartForZones)
                config.ZoneTimeFrame = TimeFrame.Hour4;

            if (!useChartForEntry)
                config.EntryTimeFrame = TimeFrame.Minute15;

            config.PivotBars = 5;
            config.EqualLevelToleranceAtr = 0.15;
            config.MinEqualTouches = 2;
            config.ZonePaddingAtr = 0.10;
            config.UseSessionLevels = true;
            config.MinZoneStrength = 65;
            config.MaxActiveZones = 10;
            config.ConfirmationBarDelay = 1;
            config.MaxSweepAgeBars = 10;
            config.RequireStructureShift = true;
            config.StructurePivotBars = 3;
            config.MinWickBodyRatio = 0.8;
            config.StopBufferAtr = 0.05;
            config.RiskPercent = 0.5;
            config.MaxSpreadPips = 30;

            return config;
        }

        public static string FormatTimeFrame(TimeFrame tf)
        {
            if (tf == TimeFrame.Minute) return "M1";
            if (tf == TimeFrame.Minute5) return "M5";
            if (tf == TimeFrame.Minute15) return "M15";
            if (tf == TimeFrame.Minute30) return "M30";
            if (tf == TimeFrame.Hour) return "H1";
            if (tf == TimeFrame.Hour4) return "H4";
            if (tf == TimeFrame.Daily) return "D1";
            if (tf == TimeFrame.Weekly) return "W1";
            return tf.ToString();
        }

        public static void PrintBanner(Robot robot, AlgoEffectiveConfig config, bool useChartForZones, bool useChartForEntry)
        {
            robot.Print("=== ALGO TRADER PRESET ACTIVE ===");
            robot.Print("HTF/LTF model: " + FormatTimeFrame(config.ZoneTimeFrame) + " liquidity -> "
                + FormatTimeFrame(config.EntryTimeFrame) + " sweep entries (attach chart to "
                + FormatTimeFrame(config.EntryTimeFrame) + " or higher for best visuals)");
            robot.Print("Use Chart TF Zones: " + useChartForZones + " | Use Chart TF Entry: " + useChartForEntry);
            robot.Print("Pivot: " + config.PivotBars + " | Min strength: " + config.MinZoneStrength
                + " | MSS: " + config.RequireStructureShift + " | Confirm delay: " + config.ConfirmationBarDelay + " bar(s)");
            robot.Print("SL: SweepWick + " + config.StopBufferAtr + " ATR | TP: 2R | Risk: "
                + config.RiskPercent + "% equity (or set Trade Risk USD)");
            robot.Print("Session levels PDH/L: " + config.UseSessionLevels + " | Max spread: "
                + config.MaxSpreadPips + " pips | Max zones: " + config.MaxActiveZones);
        }
    }

    // --- Main Robot ---

    /// <summary>
    /// Liquidity Sweep cBot for cTrader.
    /// Identifies liquidity on a configurable timeframe, waits for sweep + price-action confirmation,
    /// then manages risk and profit using USD/percent sizing and R-multiple targets.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class LiquiditySweepBot : Robot
    {
        // --- Trade Options ---
        [Parameter("Bot Trade ID", Group = "Trade Options", DefaultValue = "LQ_Sweep")]
        public string BotTradeId { get; set; }

        // --- Symbols ---
        [Parameter("Trade Chart Symbol Only", DefaultValue = true, Group = "Symbols")]
        public bool TradeChartSymbolOnly { get; set; }

        [Parameter("Extra Symbols (comma-separated)", DefaultValue = "ETHUSD", Group = "Symbols")]
        public string ExtraSymbols { get; set; }

        [Parameter("Max Open Positions Per Symbol", DefaultValue = 1, MinValue = 1, Group = "Symbols")]
        public int MaxPositionsPerSymbol { get; set; }

        // --- Timeframes ---
        [Parameter("Apply Algo Preset (when TF unchecked)", DefaultValue = true, Group = "Timeframes")]
        public bool ApplyAlgoTraderPreset { get; set; }

        [Parameter("Use Chart Timeframe for Zones", DefaultValue = false, Group = "Timeframes")]
        public bool UseChartTimeframeForZones { get; set; }

        [Parameter("Liquidity Zone Timeframe", DefaultValue = "Hour4", Group = "Timeframes")]
        public TimeFrame ZoneTimeFrame { get; set; }

        [Parameter("Use Chart Timeframe for Entry", DefaultValue = false, Group = "Timeframes")]
        public bool UseChartTimeframeForEntry { get; set; }

        [Parameter("Entry Timeframe", DefaultValue = "Minute15", Group = "Timeframes")]
        public TimeFrame EntryTimeFrame { get; set; }

        // --- Liquidity detection (Min/Max/Step = optimisation ranges in cTrader) ---
        [Parameter("Pivot Bars (swing confirmation)", DefaultValue = 5, MinValue = 3, MaxValue = 8, Step = 1, Group = "Liquidity")]
        public int PivotBars { get; set; }

        [Parameter("Equal Level Tolerance (x ATR)", DefaultValue = 0.15, MinValue = 0.10, MaxValue = 0.25, Step = 0.05, Group = "Liquidity")]
        public double EqualLevelToleranceAtr { get; set; }

        [Parameter("Min Equal Touches", DefaultValue = 2, MinValue = 2, MaxValue = 2, Step = 1, Group = "Liquidity")]
        public int MinEqualTouches { get; set; }

        [Parameter("Zone Padding (x ATR)", DefaultValue = 0.10, MinValue = 0.05, MaxValue = 0.20, Step = 0.05, Group = "Liquidity")]
        public double ZonePaddingAtr { get; set; }

        [Parameter("Use Session Levels (PDH/L, PWH/L)", DefaultValue = true, Group = "Liquidity")]
        public bool UseSessionLevels { get; set; }

        [Parameter("Min Zone Strength (0-100)", DefaultValue = 65, MinValue = 50, MaxValue = 80, Step = 5, Group = "Liquidity")]
        public int MinZoneStrength { get; set; }

        [Parameter("Max Active Zones", DefaultValue = 10, MinValue = 8, MaxValue = 12, Step = 1, Group = "Liquidity")]
        public int MaxActiveZones { get; set; }

        // --- Sweep confirmation ---
        [Parameter("Confirmation Bar Delay", DefaultValue = 1, MinValue = 0, MaxValue = 3, Step = 1, Group = "Confirmation")]
        public int ConfirmationBarDelay { get; set; }

        [Parameter("Max Sweep Age (entry bars)", DefaultValue = 10, MinValue = 6, MaxValue = 16, Step = 2, Group = "Confirmation")]
        public int MaxSweepAgeBars { get; set; }

        [Parameter("Require Structure Shift (MSS)", DefaultValue = true, Group = "Confirmation")]
        public bool RequireStructureShift { get; set; }

        [Parameter("Structure Pivot Bars", DefaultValue = 3, MinValue = 2, MaxValue = 5, Step = 1, Group = "Confirmation")]
        public int StructurePivotBars { get; set; }

        [Parameter("Min Wick/Body Ratio", DefaultValue = 0.8, MinValue = 0.5, MaxValue = 1.2, Step = 0.1, Group = "Confirmation")]
        public double MinWickBodyRatio { get; set; }

        // --- Stoploss (UltimateTrader-style) ---
        [Parameter("SL Type", Group = "Stoploss", DefaultValue = StopLossType.SweepWick)]
        public StopLossType SLType { get; set; }

        [Parameter("SL Value", Group = "Stoploss", DefaultValue = 0.0)]
        public double SLValue { get; set; }

        [Parameter("Stop Buffer (x ATR, SweepWick only)", Group = "Stoploss", DefaultValue = 0.05, MinValue = 0.02, MaxValue = 0.10, Step = 0.01)]
        public double StopBufferAtr { get; set; }

        [Parameter("SL to BE Type", Group = "Stoploss", DefaultValue = SLToBEType.None)]
        public SLToBEType SLtoBE { get; set; }

        [Parameter("SL to BE Value", Group = "Stoploss", DefaultValue = 0.0)]
        public double SLtoBEValue { get; set; }

        [Parameter("Trailing SL Type", Group = "Stoploss", DefaultValue = TrailingSLType.None)]
        public TrailingSLType TrailingSL { get; set; }

        [Parameter("Trailing SL Value", Group = "Stoploss", DefaultValue = 0.0)]
        public double TrailingSLValue { get; set; }

        // --- Take Profit (UltimateTrader-style) ---
        [Parameter("TP Type", Group = "Take Profit", DefaultValue = TakeProfitType.RiskMultiplier)]
        public TakeProfitType TPType { get; set; }

        [Parameter("TP Value", Group = "Take Profit", DefaultValue = 2.0, MinValue = 1.5, MaxValue = 3.0, Step = 0.25)]
        public double TPValue { get; set; }

        // --- Risk Management ---
        [Parameter("Trade Risk (USD)", Group = "Risk Management", DefaultValue = 0.0)]
        public double TradeRisk { get; set; }

        [Parameter("Risk Mode (if Trade Risk = 0)", DefaultValue = RiskMode.PercentEquity, Group = "Risk Management")]
        public RiskMode RiskModeSetting { get; set; }

        [Parameter("Fixed USD Risk", DefaultValue = 100, MinValue = 1, Group = "Risk Management")]
        public double FixedUsdRisk { get; set; }

        [Parameter("Risk Percent", DefaultValue = 0.5, MinValue = 0.25, MaxValue = 1.0, Step = 0.25, Group = "Risk Management")]
        public double RiskPercent { get; set; }

        [Parameter("Max Spread (pips, 0=off)", DefaultValue = 30, MinValue = 15, MaxValue = 50, Step = 5, Group = "Risk Management")]
        public double MaxSpreadPips { get; set; }

        [Parameter("ATR Period", DefaultValue = 14, MinValue = 10, MaxValue = 20, Step = 2, Group = "Risk Management")]
        public int AtrPeriod { get; set; }

        // --- Visual ---
        [Parameter("Draw Zones On Chart", DefaultValue = true, Group = "Visual")]
        public bool DrawZonesOnChart { get; set; }

        [Parameter("Show Zone Labels", DefaultValue = true, Group = "Visual")]
        public bool ShowZoneLabels { get; set; }

        [Parameter("Show Sweep Markers", DefaultValue = true, Group = "Visual")]
        public bool ShowSweepMarkers { get; set; }

        [Parameter("Show Entry Markers", DefaultValue = true, Group = "Visual")]
        public bool ShowEntryMarkers { get; set; }

        [Parameter("Max Zones Drawn", DefaultValue = 10, MinValue = 1, MaxValue = 20, Group = "Visual")]
        public int MaxZonesDrawn { get; set; }

        [Parameter("Zone Lookback Bars", DefaultValue = 200, MinValue = 50, Group = "Visual")]
        public int ZoneLookbackBars { get; set; }

        [Parameter("Debug Zone Drawing (log)", DefaultValue = false, Group = "Visual")]
        public bool DebugZoneDrawing { get; set; }

        [Parameter("Keep Drawings After Stop", DefaultValue = true, Group = "Visual")]
        public bool KeepDrawingsAfterStop { get; set; }

        private readonly Dictionary<string, SymbolTradingContext> _contexts = new Dictionary<string, SymbolTradingContext>();
        private readonly List<EntryMarker> _entryMarkers = new List<EntryMarker>();
        private readonly ChartVisualizer _visualizer = new ChartVisualizer();
        private RiskManager _riskManager;
        private AverageTrueRange _chartAtr;
        private TimeFrame _resolvedZoneTimeFrame;
        private TimeFrame _resolvedEntryTimeFrame;
        private AlgoEffectiveConfig _effectiveConfig;
        private SymbolTradingContext _chartContext;
        private int _lastProcessedBarIndex = -1;
        private bool _initialDrawDone;
        private int _timerRefreshCount;

        protected override void OnStart()
        {
            _effectiveConfig = AlgoTraderPresets.Resolve(
                ApplyAlgoTraderPreset,
                IsOptimizing,
                UseChartTimeframeForZones,
                UseChartTimeframeForEntry,
                TimeFrame,
                ZoneTimeFrame,
                EntryTimeFrame,
                PivotBars,
                EqualLevelToleranceAtr,
                MinEqualTouches,
                ZonePaddingAtr,
                UseSessionLevels,
                MinZoneStrength,
                MaxActiveZones,
                ConfirmationBarDelay,
                MaxSweepAgeBars,
                RequireStructureShift,
                StructurePivotBars,
                MinWickBodyRatio,
                StopBufferAtr,
                RiskPercent,
                MaxSpreadPips);

            _resolvedZoneTimeFrame = _effectiveConfig.ZoneTimeFrame;
            _resolvedEntryTimeFrame = _effectiveConfig.EntryTimeFrame;

            _chartAtr = Indicators.AverageTrueRange(Bars, AtrPeriod, MovingAverageType.Simple);

            double riskPercent = _effectiveConfig.PresetActive ? _effectiveConfig.RiskPercent : RiskPercent;
            double maxSpread = _effectiveConfig.PresetActive ? _effectiveConfig.MaxSpreadPips : MaxSpreadPips;
            double stopBuffer = _effectiveConfig.PresetActive ? _effectiveConfig.StopBufferAtr : StopBufferAtr;

            _riskManager = new RiskManager(
                RiskModeSetting,
                FixedUsdRisk,
                riskPercent,
                maxSpread,
                stopBuffer);

            foreach (var symbol in ResolveSymbols())
            {
                RegisterSymbol(symbol);
            }

            if (_contexts.Count == 0)
            {
                Print("No tradable symbols resolved. Attach bot to BTCUSD chart or set Extra Symbols.");
                Stop();
                return;
            }

            if (!_contexts.TryGetValue(Symbol.Name, out _chartContext))
            {
                if (!_contexts.TryGetValue(SymbolName, out _chartContext))
                {
                    foreach (var kv in _contexts)
                    {
                        _chartContext = kv.Value;
                        break;
                    }
                }
            }

            Print("=== LiquiditySweepBot STARTED ===");
            Print("Liquidity TF: " + AlgoTraderPresets.FormatTimeFrame(_resolvedZoneTimeFrame)
                + " | Entry TF: " + AlgoTraderPresets.FormatTimeFrame(_resolvedEntryTimeFrame)
                + " | Chart TF: " + AlgoTraderPresets.FormatTimeFrame(TimeFrame) + " | Symbol: " + Symbol.Name);
            Print("DrawZonesOnChart: " + DrawZonesOnChart + " | Backtesting: " + IsBacktesting + " | Optimizing: " + IsOptimizing);

            if (IsOptimizing)
                Print("Optimisation mode — sweeping parameter values (live algo preset overrides disabled).");

            if (_effectiveConfig.PresetActive)
                AlgoTraderPresets.PrintBanner(this, _effectiveConfig, UseChartTimeframeForZones, UseChartTimeframeForEntry);
            else if (!UseChartTimeframeForZones || !UseChartTimeframeForEntry)
                Print("Algo preset OFF — using your manual parameter values. Enable 'Apply Algo Preset' for recommended HTF/LTF settings.");
            else
                Print("Chart-timeframe mode — zones and entries follow the chart TF. Uncheck TF boxes + enable preset for algo defaults (H4/M15).");

            if (_chartContext == null)
            {
                Print("ERROR: No chart context — bot cannot draw zones.");
            }
            else
            {
                Print("Zone bars loaded: " + _chartContext.ZoneBars.Count + " | Chart bars: " + Bars.Count);
                _visualizer.DrawHeartbeat(Chart, _resolvedZoneTimeFrame, _chartContext.ZoneBars.Count);
                _chartContext.RefreshZones();
                _chartContext.RefreshEntrySetups();
                RefreshChartVisuals(_chartContext);
            }

            Bars.BarOpened += OnChartBarOpened;
            Timer.Start(1);
        }

        protected override void OnStop()
        {
            Timer.Stop();
            Bars.BarOpened -= OnChartBarOpened;
            if (!KeepDrawingsAfterStop)
                _visualizer.Clear(Chart);
        }

        private void OnChartBarOpened(BarOpenedEventArgs args)
        {
            if (_chartContext == null || args.Bars != Bars)
                return;

            _chartContext.RefreshZones();
            _chartContext.RefreshEntrySetups();
            RefreshChartVisuals(_chartContext);
        }

        protected override void OnBar()
        {
            if (_chartContext == null)
                return;

            _chartContext.RefreshZones();
            _chartContext.RefreshEntrySetups();
            RefreshChartVisuals(_chartContext);
        }

        protected override void OnTimer()
        {
            _timerRefreshCount++;
            if (_chartContext != null)
            {
                _chartContext.RefreshZones();
                _chartContext.RefreshEntrySetups();
                RefreshChartVisuals(_chartContext);
            }

            if (_timerRefreshCount < 20)
                Timer.Start(1);
            else
                Timer.Stop();
        }

        protected override void OnTick()
        {
            ApplyStopLossToBreakEven();
            ApplyTrailingStopLoss();

            if (_chartContext == null)
                return;

            if (!_initialDrawDone)
            {
                _initialDrawDone = true;
                _chartContext.RefreshZones();
                _chartContext.RefreshEntrySetups();
                RefreshChartVisuals(_chartContext);
            }

            if (IsBacktesting)
            {
                _chartContext.RefreshZones();
                _chartContext.RefreshEntrySetups();
                RefreshChartVisuals(_chartContext);
            }

            if (Bars.Count - 1 > _lastProcessedBarIndex)
            {
                _lastProcessedBarIndex = Bars.Count - 1;

                if (!IsBacktesting)
                {
                    _chartContext.RefreshZones();
                    _chartContext.RefreshEntrySetups();
                    RefreshChartVisuals(_chartContext);
                }

                if (_resolvedEntryTimeFrame == TimeFrame || UseChartTimeframeForEntry)
                    TryExecuteEntries(_chartContext);
            }
        }

        private void RegisterSymbol(Symbol symbol)
        {
            var zoneBars = MarketData.GetBars(_resolvedZoneTimeFrame, symbol.Name);
            var entryBars = MarketData.GetBars(_resolvedEntryTimeFrame, symbol.Name);
            var zoneAtr = Indicators.AverageTrueRange(zoneBars, AtrPeriod, MovingAverageType.Simple);
            var entryAtr = Indicators.AverageTrueRange(entryBars, AtrPeriod, MovingAverageType.Simple);

            var cfg = _effectiveConfig;

            var zoneEngine = new LiquidityZoneEngine(
                cfg.PivotBars,
                cfg.EqualLevelToleranceAtr,
                cfg.MinEqualTouches,
                cfg.ZonePaddingAtr,
                cfg.UseSessionLevels,
                cfg.MaxActiveZones);

            var sweepEngine = new SweepConfirmationEngine(
                cfg.ConfirmationBarDelay,
                cfg.MaxSweepAgeBars,
                cfg.RequireStructureShift,
                cfg.StructurePivotBars,
                cfg.MinWickBodyRatio,
                cfg.MinZoneStrength);

            var ctx = new SymbolTradingContext(symbol, zoneBars, entryBars, zoneAtr, entryAtr, zoneEngine, sweepEngine);
            _contexts[symbol.Name] = ctx;

            zoneBars.BarOpened += args =>
            {
                if (args.Bars.SymbolName != symbol.Name)
                    return;
                ProcessSymbol(ctx, true);
            };

            entryBars.BarOpened += args =>
            {
                if (args.Bars.SymbolName != symbol.Name)
                    return;
                ProcessSymbol(ctx, false);
            };

            Print("Registered " + symbol.Name + " | Zone TF: " + _resolvedZoneTimeFrame + " | Entry TF: " + _resolvedEntryTimeFrame);
        }

        private void ProcessSymbol(SymbolTradingContext ctx, bool isZoneBarEvent)
        {
            ctx.EvaluateAtBarOpen(isZoneBarEvent);

            if (ctx == _chartContext)
                RefreshChartVisuals(ctx);

            if (!isZoneBarEvent && ctx != _chartContext)
                TryExecuteEntries(ctx);
        }

        private void RefreshChartVisuals(SymbolTradingContext ctx)
        {
            if (!DrawZonesOnChart)
            {
                _visualizer.Clear(Chart);
                return;
            }

            ctx.RefreshZones();

            int drawn = _visualizer.DrawZonesAndSetups(
                Chart,
                Bars,
                ctx.Symbol,
                _resolvedZoneTimeFrame,
                ctx.LastZones,
                ctx.EntryBars,
                ctx.ActiveSetups,
                MaxZonesDrawn,
                ZoneLookbackBars,
                ShowZoneLabels,
                ShowSweepMarkers,
                ShowEntryMarkers,
                _entryMarkers);

            if (drawn == 0)
                _visualizer.DrawTestPattern(Chart, Bars, ctx.Symbol);

            if (DebugZoneDrawing)
            {
                string sample = ctx.LastZones.Count > 0
                    ? " | sample @" + Math.Round(ctx.LastZones[0].LevelPrice, ctx.Symbol.Digits)
                    : string.Empty;
                Print("Chart draw: " + drawn + " drawn | " + ctx.LastZones.Count + " detected"
                    + " | zone TF bars " + ctx.ZoneBars.Count + " | chart bars " + Bars.Count + sample);
            }
        }

        private void TryExecuteEntries(SymbolTradingContext ctx)
        {
            if (!_riskManager.IsSpreadAcceptable(ctx.Symbol))
                return;

            if (Positions.Find(BotTradeId, ctx.Symbol.Name) != null)
                return;

            int openForSymbol = Positions.Count(p => p.SymbolName == ctx.Symbol.Name);
            if (openForSymbol >= MaxPositionsPerSymbol)
                return;

            var ready = ctx.SweepEngine.GetReadyEntries(ctx.ActiveSetups);
            foreach (var setup in ready)
            {
                if (openForSymbol >= MaxPositionsPerSymbol)
                    break;

                ExecuteSetup(ctx, setup);
                setup.Phase = SweepPhase.Invalidated;
                openForSymbol++;
            }
        }

        private void ExecuteSetup(SymbolTradingContext ctx, SweepSetup setup)
        {
            var symbol = ctx.Symbol;
            if (Positions.Find(BotTradeId, symbol.Name) != null)
                return;

            int atrIndex = SymbolTradingContext.GetLastClosedBarIndex(ctx.EntryBars);
            if (atrIndex < 0)
                atrIndex = 0;
            double atr = ctx.EntryAtr.Result[atrIndex];

            double? stopLossPips = CalculateStopLossPips(setup, symbol, atr);
            if (!stopLossPips.HasValue || stopLossPips.Value <= 0)
            {
                Print(symbol.Name + ": invalid stop loss — trade skipped.");
                return;
            }

            double volumeInUnits = CalculateVolumeInUnits(symbol, stopLossPips.Value);
            if (volumeInUnits <= 0)
            {
                Print(symbol.Name + ": volume too small for risk settings.");
                return;
            }

            double? takeProfitPips = CalculateTakeProfitPips(setup.Direction, symbol, atr, stopLossPips);

            var result = ExecuteMarketOrder(setup.Direction, symbol.Name, volumeInUnits, BotTradeId, stopLossPips, takeProfitPips);
            if (result.IsSuccessful)
            {
                double entry = setup.Direction == TradeType.Buy ? symbol.Ask : symbol.Bid;
                double slPrice = setup.Direction == TradeType.Buy
                    ? entry - stopLossPips.Value * symbol.PipSize
                    : entry + stopLossPips.Value * symbol.PipSize;
                double? tpPrice = null;
                if (takeProfitPips.HasValue)
                {
                    tpPrice = setup.Direction == TradeType.Buy
                        ? entry + takeProfitPips.Value * symbol.PipSize
                        : entry - takeProfitPips.Value * symbol.PipSize;
                }

                if (ShowEntryMarkers && symbol.Name == SymbolName)
                {
                    _entryMarkers.Add(new EntryMarker
                    {
                        Time = ctx.EntryBars.OpenTimes[atrIndex],
                        Price = entry,
                        StopLoss = slPrice,
                        TakeProfit = tpPrice ?? 0,
                        Direction = setup.Direction
                    });

                    if (_entryMarkers.Count > 30)
                        _entryMarkers.RemoveAt(0);

                    RefreshChartVisuals(ctx);
                }

                Print(symbol.Name + " " + setup.Direction + " | Zone: " + setup.Zone.Label + " @ " + setup.Zone.LevelPrice.ToString("F2")
                    + " | SL: " + stopLossPips.Value.ToString("F1") + " pips | TP: " + (takeProfitPips.HasValue ? takeProfitPips.Value.ToString("F1") + " pips" : "none")
                    + " | Vol: " + volumeInUnits);
            }
            else
            {
                Print(symbol.Name + " order failed: " + result.Error);
            }
        }

        private double? CalculateStopLossPips(SweepSetup setup, Symbol symbol, double atr)
        {
            if (SLType == StopLossType.SweepWick)
            {
                double buffer = atr * _effectiveConfig.StopBufferAtr;
                double stopPrice = setup.Direction == TradeType.Buy
                    ? setup.SweepWickExtreme - buffer
                    : setup.SweepWickExtreme + buffer;
                double entry = setup.Direction == TradeType.Buy ? symbol.Ask : symbol.Bid;
                stopPrice = SymbolHelper.NormalizePrice(symbol, stopPrice);
                return Math.Abs(entry - stopPrice) / symbol.PipSize;
            }

            if (SLValue <= 0)
                return null;

            switch (SLType)
            {
                case StopLossType.Pips:
                    return SLValue;
                case StopLossType.Percent:
                    {
                        double referencePrice = setup.Direction == TradeType.Buy ? symbol.Ask : symbol.Bid;
                        return (referencePrice * (SLValue / 100.0)) / symbol.PipSize;
                    }
                case StopLossType.ATR:
                    return (_chartAtr.Result.Last(1) * SLValue) / symbol.PipSize;
                default:
                    return null;
            }
        }

        private double? CalculateTakeProfitPips(TradeType tradeType, Symbol symbol, double atr, double? stopLossPips)
        {
            if (TPType == TakeProfitType.None || TPValue <= 0)
                return null;

            switch (TPType)
            {
                case TakeProfitType.Pips:
                    return TPValue;
                case TakeProfitType.Percent:
                    {
                        double referencePrice = tradeType == TradeType.Buy ? symbol.Ask : symbol.Bid;
                        return (referencePrice * (TPValue / 100.0)) / symbol.PipSize;
                    }
                case TakeProfitType.ATR:
                    return (_chartAtr.Result.Last(1) * TPValue) / symbol.PipSize;
                case TakeProfitType.RiskMultiplier:
                    if (stopLossPips.HasValue)
                        return stopLossPips.Value * TPValue;
                    return null;
                default:
                    return null;
            }
        }

        private double CalculateVolumeInUnits(Symbol symbol, double stopLossPips)
        {
            if (TradeRisk > 0 && stopLossPips > 0)
            {
                double riskPerPip = symbol.PipValue;
                double volumeInLots = TradeRisk / ((stopLossPips * riskPerPip) * symbol.LotSize);
                double volumeInUnits = symbol.QuantityToVolumeInUnits(volumeInLots);
                if (volumeInUnits < symbol.VolumeInUnitsMin)
                    volumeInUnits = symbol.VolumeInUnitsMin;
                return symbol.NormalizeVolumeInUnits(volumeInUnits);
            }

            double riskAmount = _riskManager.CalculateRiskAmount(Account);
            double amountRiskedPerUnit = stopLossPips * symbol.PipSize * symbol.TickValue / symbol.TickSize;
            if (amountRiskedPerUnit <= 0)
                return symbol.VolumeInUnitsMin;

            double rawVolume = riskAmount / amountRiskedPerUnit;
            rawVolume = symbol.NormalizeVolumeInUnits(rawVolume, RoundingMode.Down);
            if (rawVolume < symbol.VolumeInUnitsMin)
                return 0;
            if (rawVolume > symbol.VolumeInUnitsMax)
                rawVolume = symbol.VolumeInUnitsMax;
            return rawVolume;
        }

        private void ApplyStopLossToBreakEven()
        {
            var position = Positions.Find(BotTradeId, Symbol.Name);
            if (position == null || SLtoBE == SLToBEType.None || SLtoBEValue <= 0)
                return;

            if (position.StopLoss.HasValue && position.StopLoss.Value == position.EntryPrice)
                return;

            double thresholdPips;
            switch (SLtoBE)
            {
                case SLToBEType.Pips:
                    thresholdPips = SLtoBEValue;
                    break;
                case SLToBEType.Percent:
                    thresholdPips = (position.EntryPrice * SLtoBEValue / 100.0) / Symbol.PipSize;
                    break;
                case SLToBEType.ATR:
                    thresholdPips = (_chartAtr.Result.Last(1) * SLtoBEValue) / Symbol.PipSize;
                    break;
                case SLToBEType.RiskMultiplier:
                    if (!position.StopLoss.HasValue)
                        return;
                    thresholdPips = Math.Abs((position.EntryPrice - position.StopLoss.Value) / Symbol.PipSize) * SLtoBEValue;
                    break;
                default:
                    return;
            }

            if (position.Pips >= thresholdPips)
                position.ModifyStopLossPrice(position.EntryPrice);
        }

        private void ApplyTrailingStopLoss()
        {
            var position = Positions.Find(BotTradeId, Symbol.Name);
            if (position == null || TrailingSL == TrailingSLType.None || TrailingSLValue <= 0)
                return;

            double trailingPips;
            switch (TrailingSL)
            {
                case TrailingSLType.Pips:
                    trailingPips = TrailingSLValue;
                    break;
                case TrailingSLType.Percent:
                    trailingPips = (position.EntryPrice * TrailingSLValue / 100.0) / Symbol.PipSize;
                    break;
                case TrailingSLType.ATR:
                    trailingPips = (_chartAtr.Result.Last(1) * TrailingSLValue) / Symbol.PipSize;
                    break;
                case TrailingSLType.RiskMultiplier:
                    if (!position.StopLoss.HasValue)
                        return;
                    trailingPips = Math.Abs((position.EntryPrice - position.StopLoss.Value) / Symbol.PipSize) * TrailingSLValue;
                    break;
                default:
                    return;
            }

            if (trailingPips <= 0)
                return;

            double newStopPrice = position.TradeType == TradeType.Buy
                ? Symbol.Bid - trailingPips * Symbol.PipSize
                : Symbol.Ask + trailingPips * Symbol.PipSize;

            newStopPrice = SymbolHelper.NormalizePrice(Symbol, newStopPrice);

            if (position.TradeType == TradeType.Buy)
            {
                if (!position.StopLoss.HasValue || newStopPrice > position.StopLoss.Value)
                    position.ModifyStopLossPrice(newStopPrice);
            }
            else
            {
                if (!position.StopLoss.HasValue || newStopPrice < position.StopLoss.Value)
                    position.ModifyStopLossPrice(newStopPrice);
            }
        }

        /// <summary>
        /// Custom fitness for Optimisation → Criteria → Custom.
        /// Rewards profit factor and net profit, penalises drawdown; ignores passes with too few trades.
        /// </summary>
        protected override double GetFitness(GetFitnessArgs args)
        {
            const int minTrades = 15;
            if (args.Trades < minTrades)
                return 0;

            double pf = args.ProfitFactor;
            if (pf <= 0 || double.IsNaN(pf) || double.IsInfinity(pf))
                return 0;

            double dd = args.MaxEquityDrawdownPercentages;
            if (dd < 0.5)
                dd = 0.5;

            return pf * args.NetProfit / dd;
        }

        private IEnumerable<Symbol> ResolveSymbols()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (TradeChartSymbolOnly)
            {
                names.Add(SymbolName);
            }
            else
            {
                names.Add(SymbolName);
                if (!string.IsNullOrWhiteSpace(ExtraSymbols))
                {
                    foreach (var part in ExtraSymbols.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        names.Add(part.Trim());
                }
            }

            foreach (var name in names)
            {
                Symbol symbol = Symbols.GetSymbol(name);
                if (symbol == null)
                {
                    Print("Symbol not found: " + name);
                    continue;
                }
                yield return symbol;
            }
        }
    }
}
