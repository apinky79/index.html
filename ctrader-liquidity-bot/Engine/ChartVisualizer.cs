using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.Robots.LiquiditySweep.Models;

namespace cAlgo.Robots.LiquiditySweep.Engine
{
    public sealed class ChartVisualizer
    {
        private const string Prefix = "LQ_";
        private readonly HashSet<string> _trackedObjects = new HashSet<string>();

        public void DrawZonesAndSetups(
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
                return;

            int startIndex = Math.Max(0, chartBars.Count - lookbackBars);
            DateTime startTime = chartBars.OpenTimes[startIndex];
            DateTime endTime = chartBars.LastBar.OpenTime;

            foreach (var zone in zones.OrderByDescending(z => z.StrengthScore).Take(maxZones))
            {
                DrawZone(chart, symbol, zone, startTime, endTime, showLabels);
            }

            if (showSweepMarkers && entryBars != null)
            {
                foreach (var setup in setups.Where(s => s.Phase == SweepPhase.Swept || s.Phase == SweepPhase.Confirmed))
                {
                    DrawSweepMarker(chart, entryBars, symbol, setup);
                }
            }

            if (showEntryMarkers)
            {
                foreach (var entry in recentEntries)
                    DrawEntryMarker(chart, symbol, entry);
            }

            DrawTimeframeBadge(chart, chartBars, zoneTimeFrame);
        }

        private void DrawZone(
            Chart chart,
            Symbol symbol,
            LiquidityZone zone,
            DateTime startTime,
            DateTime endTime,
            bool showLabels)
        {
            string id = ZoneId(zone);
            bool isBsl = zone.Side == LiquiditySide.BuySide;
            Color fill = isBsl ? Color.FromArgb(45, 231, 76, 60) : Color.FromArgb(45, 52, 152, 219);
            Color line = isBsl ? Color.FromArgb(200, 231, 76, 60) : Color.FromArgb(200, 52, 152, 219);
            Color labelColor = isBsl ? Color.FromArgb(255, 192, 57, 43) : Color.FromArgb(255, 41, 128, 185);

            TrackDraw($"{Prefix}rect_{id}");
            chart.DrawRectangle(
                $"{Prefix}rect_{id}",
                startTime,
                zone.ZoneTop,
                endTime,
                zone.ZoneBottom,
                fill,
                1,
                LineStyle.Solid);

            TrackDraw($"{Prefix}lvl_{id}");
            chart.DrawTrendLine(
                $"{Prefix}lvl_{id}",
                startTime,
                zone.LevelPrice,
                endTime,
                zone.LevelPrice,
                line,
                2,
                LineStyle.Solid);

            if (showLabels)
            {
                string sideTag = isBsl ? "BSL" : "SSL";
                string text = $"{sideTag} {ShortSource(zone.Source)} @ {symbol.FormatPrice(zone.LevelPrice)}  [{zone.StrengthScore}]";
                TrackDraw($"{Prefix}lbl_{id}");
                chart.DrawText(
                    $"{Prefix}lbl_{id}",
                    text,
                    endTime,
                    zone.LevelPrice,
                    labelColor);
            }
        }

        private void DrawSweepMarker(Chart chart, Bars entryBars, Symbol symbol, SweepSetup setup)
        {
            if (setup.SweepBarIndex < 0 || setup.SweepBarIndex >= entryBars.Count)
                return;

            DateTime time = entryBars.OpenTimes[setup.SweepBarIndex];
            double price = setup.SweepWickExtreme;
            bool isLong = setup.Direction == TradeType.Buy;
            Color color = isLong ? Color.LimeGreen : Color.OrangeRed;
            string phase = setup.Phase == SweepPhase.Confirmed ? "SWEEP OK" : "SWEEP";
            string sweepKey = $"{setup.SweepBarIndex}_{setup.Zone.LevelPrice:F5}";

            TrackDraw($"{Prefix}sweep_{sweepKey}");
            chart.DrawIcon(
                $"{Prefix}sweep_{sweepKey}",
                isLong ? ChartIconType.UpTriangle : ChartIconType.DownTriangle,
                time,
                price,
                color);

            TrackDraw($"{Prefix}sweep_txt_{sweepKey}");
            chart.DrawText(
                $"{Prefix}sweep_txt_{sweepKey}",
                phase,
                time,
                price + (isLong ? -symbol.PipSize * 8 : symbol.PipSize * 8),
                color);
        }

        private void DrawEntryMarker(Chart chart, Symbol symbol, EntryMarker entry)
        {
            bool isLong = entry.Direction == TradeType.Buy;
            Color color = isLong ? Color.DodgerBlue : Color.MediumVioletRed;

            string entryKey = entry.Time.Ticks.ToString();

            TrackDraw($"{Prefix}entry_{entryKey}");
            chart.DrawIcon(
                $"{Prefix}entry_{entryKey}",
                isLong ? ChartIconType.UpArrow : ChartIconType.DownArrow,
                entry.Time,
                entry.Price,
                color);

            TrackDraw($"{Prefix}entry_txt_{entryKey}");
            chart.DrawText(
                $"{Prefix}entry_txt_{entryKey}",
                $"ENTRY {(isLong ? "LONG" : "SHORT")}",
                entry.Time,
                entry.Price + (isLong ? symbol.PipSize * 10 : -symbol.PipSize * 10),
                color);

            if (entry.StopLoss > 0)
            {
                TrackDraw($"{Prefix}entry_sl_{entryKey}");
                chart.DrawHorizontalLine(
                    $"{Prefix}entry_sl_{entryKey}",
                    entry.StopLoss,
                    Color.FromArgb(160, 231, 76, 60),
                    1,
                    LineStyle.DotsRare);
            }

            if (entry.TakeProfit > 0)
            {
                TrackDraw($"{Prefix}entry_tp_{entryKey}");
                chart.DrawHorizontalLine(
                    $"{Prefix}entry_tp_{entryKey}",
                    entry.TakeProfit,
                    Color.FromArgb(160, 46, 204, 113),
                    1,
                    LineStyle.DotsRare);
            }
        }

        private void DrawTimeframeBadge(Chart chart, Bars chartBars, TimeFrame zoneTimeFrame)
        {
            if (chartBars.Count < 1)
                return;

            TrackDraw($"{Prefix}tf_badge");
            chart.DrawStaticText(
                $"{Prefix}tf_badge",
                $"Liquidity zones: {FormatTimeFrame(zoneTimeFrame)}",
                VerticalAlignment.Top,
                HorizontalAlignment.Left,
                Color.FromArgb(220, 44, 62, 80));
        }

        public void Clear(Chart chart)
        {
            foreach (var name in _trackedObjects.ToList())
                chart.RemoveObject(name);
            _trackedObjects.Clear();
        }

        private void TrackDraw(string name)
        {
            _trackedObjects.Add(name);
        }

        private static string ZoneId(LiquidityZone zone)
        {
            return $"{zone.Source}_{zone.LevelPrice:F5}".Replace('.', '_');
        }

        private static string ShortSource(LiquiditySource source)
        {
            return source switch
            {
                LiquiditySource.EqualHigh => "EQH",
                LiquiditySource.EqualLow => "EQL",
                LiquiditySource.PreviousDayHigh => "PDH",
                LiquiditySource.PreviousDayLow => "PDL",
                LiquiditySource.PreviousWeekHigh => "PWH",
                LiquiditySource.PreviousWeekLow => "PWL",
                LiquiditySource.SwingHigh => "SwingH",
                LiquiditySource.SwingLow => "SwingL",
                _ => source.ToString()
            };
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

    public sealed class EntryMarker
    {
        public DateTime Time { get; set; }
        public double Price { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public TradeType Direction { get; set; }
    }
}
