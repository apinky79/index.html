using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.Robots.LiquiditySweep.Models;

namespace cAlgo.Robots.LiquiditySweep.Engine
{
    /// <summary>
    /// Identifies liquidity pools using:
    /// 1) Confirmed swing highs/lows (resting stops beyond structure)
    /// 2) Equal highs/lows clustered within ATR-relative tolerance (strongest pools)
    /// 3) Previous day/week high-low session levels (validated on crypto per walk-forward SMC systems)
    /// </summary>
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
                return Array.Empty<LiquidityZone>();

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

            AddEqualLevelZones(swings.Where(s => s.IsHigh).ToList(), tolerance, padding, zones, isHigh: true);
            AddEqualLevelZones(swings.Where(s => !s.IsHigh).ToList(), tolerance, padding, zones, isHigh: false);

            if (_useSessionLevels)
                AddSessionLevels(highs, lows, openTimes, lastClosedBarIndex, padding, zones);

            return zones
                .Where(z => z.FormedBarIndex <= lastClosedBarIndex)
                .GroupBy(z => $"{z.Source}:{Math.Round(z.LevelPrice, 5)}")
                .Select(g => g.OrderByDescending(z => z.StrengthScore).First())
                .OrderByDescending(z => z.StrengthScore)
                .ThenByDescending(z => z.FormedBarIndex)
                .Take(_maxActiveZones)
                .ToList();
        }

        private static LiquidityZone CreateSwingZone(
            SwingPointDetector.SwingPoint swing,
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
            List<SwingPointDetector.SwingPoint> swings,
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
                var last = swings[cluster.Last()];

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

            var dayGroups = new Dictionary<DateTime, (double High, double Low, int LastBar)>();
            var weekGroups = new Dictionary<(int Year, int Week), (double High, double Low, int LastBar)>();

            for (int i = 0; i <= lastClosedBarIndex; i++)
            {
                var day = openTimes[i].Date;
                if (!dayGroups.TryGetValue(day, out var dayVal))
                    dayVal = (highs[i], lows[i], i);
                else
                {
                    dayVal.High = Math.Max(dayVal.High, highs[i]);
                    dayVal.Low = Math.Min(dayVal.Low, lows[i]);
                    dayVal.LastBar = i;
                }
                dayGroups[day] = dayVal;

                var weekKey = GetIsoWeekKey(openTimes[i]);
                if (!weekGroups.TryGetValue(weekKey, out var weekVal))
                    weekVal = (highs[i], lows[i], i);
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
            var priorWeeks = weekGroups.Keys.Where(w => w.CompareTo(currentWeek) < 0).OrderByDescending(w => w).Take(1).ToList();
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
            int baseScore = source switch
            {
                LiquiditySource.EqualHigh or LiquiditySource.EqualLow => 75,
                LiquiditySource.PreviousWeekHigh or LiquiditySource.PreviousWeekLow => 70,
                LiquiditySource.PreviousDayHigh or LiquiditySource.PreviousDayLow => 65,
                LiquiditySource.SwingHigh or LiquiditySource.SwingLow => 50,
                _ => 40
            };

            return Math.Min(100, baseScore + (touchCount - 1) * 8);
        }

        private static (int Year, int Week) GetIsoWeekKey(DateTime date)
        {
            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            int week = cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return (date.Year, week);
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
}
