using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.Robots.LiquiditySweep.Models;

namespace cAlgo.Robots.LiquiditySweep.Engine
{
    public sealed class SymbolTradingContext
    {
        public SymbolTradingContext(
            Symbol symbol,
            Bars zoneBars,
            Bars entryBars,
            AverageTrueRange atr,
            LiquidityZoneEngine zoneEngine,
            SweepConfirmationEngine sweepEngine)
        {
            Symbol = symbol;
            ZoneBars = zoneBars;
            EntryBars = entryBars;
            Atr = atr;
            ZoneEngine = zoneEngine;
            SweepEngine = sweepEngine;
            ActiveSetups = new List<SweepSetup>();
        }

        public Symbol Symbol { get; }
        public Bars ZoneBars { get; }
        public Bars EntryBars { get; }
        public AverageTrueRange Atr { get; }
        public LiquidityZoneEngine ZoneEngine { get; }
        public SweepConfirmationEngine SweepEngine { get; }
        public List<SweepSetup> ActiveSetups { get; }
        public IReadOnlyList<LiquidityZone> LastZones { get; private set; } = Array.Empty<LiquidityZone>();

        /// <summary>
        /// Evaluate using the most recently closed bar.
        /// Called from Bars.BarClosed handlers where the forming bar is omitted from the collection.
        /// </summary>
        public void EvaluateAtBarClose()
        {
            int zoneLast = ZoneBars.Count - 1;
            if (zoneLast < 10)
                return;

            LastZones = ZoneEngine.BuildZones(
                ToArray(ZoneBars.HighPrices),
                ToArray(ZoneBars.LowPrices),
                ToArray(ZoneBars.ClosePrices),
                ToArray(ZoneBars.OpenTimes),
                ToArray(Atr.Result),
                zoneLast);

            int entryLast = EntryBars.Count - 1;
            if (entryLast < 5)
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
}
