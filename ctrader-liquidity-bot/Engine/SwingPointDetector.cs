using System;
using System.Collections.Generic;

namespace cAlgo.Robots.LiquiditySweep.Engine
{
    /// <summary>
    /// Non-repainting swing detection: a pivot is confirmed only after N bars on each side.
    /// This matches the industry-standard approach used in SMC frameworks (FractalTrader, LuxAlgo, JOAT).
    /// </summary>
    public static class SwingPointDetector
    {
        public sealed class SwingPoint
        {
            public int BarIndex { get; set; }
            public double Price { get; set; }
            public bool IsHigh { get; set; }
            public DateTime OpenTime { get; set; }
        }

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
}
