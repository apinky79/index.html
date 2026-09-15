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
        }

        public Symbol Symbol { get; }
        public Bars ZoneBars { get; }
        public Bars EntryBars { get; }
        public AverageTrueRange ZoneAtr { get; }
        public AverageTrueRange EntryAtr { get; }
        public LiquidityZoneEngine ZoneEngine { get; }
        public SweepConfirmationEngine SweepEngine { get; }
        public List<SweepSetup> ActiveSetups { get; }
        public IReadOnlyList<LiquidityZone> LastZones { get; private set; } = Array.Empty<LiquidityZone>();

        public void RefreshZones()
        {
            int zoneLast = ZoneBars.Count - 1;
            if (zoneLast < 10)
                return;

            LastZones = ZoneEngine.BuildZones(
                ToArray(ZoneBars.HighPrices),
                ToArray(ZoneBars.LowPrices),
                ToArray(ZoneBars.ClosePrices),
                ToArray(ZoneBars.OpenTimes),
                ToArray(ZoneAtr.Result),
                zoneLast);
        }

        public void RefreshEntrySetups()
        {
            int entryLast = EntryBars.Count - 1;
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

        public void EvaluateAtBarClose(bool isZoneBarEvent)
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
}
