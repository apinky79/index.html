using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.Robots.LiquiditySweep.Engine;
using cAlgo.Robots.LiquiditySweep.Models;

namespace cAlgo.Robots
{
    /// <summary>
    /// Liquidity Sweep cBot for cTrader.
    /// Identifies liquidity on a higher timeframe, waits for sweep + price-action confirmation on the entry timeframe,
    /// then manages risk and profit using USD/percent sizing and R-multiple targets.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class LiquiditySweepBot : Robot
    {
        // --- Symbols ---
        [Parameter("Trade Chart Symbol Only", DefaultValue = true, Group = "Symbols")]
        public bool TradeChartSymbolOnly { get; set; }

        [Parameter("Extra Symbols (comma-separated)", DefaultValue = "ETHUSD", Group = "Symbols")]
        public string ExtraSymbols { get; set; }

        [Parameter("Max Open Positions Per Symbol", DefaultValue = 1, MinValue = 1, Group = "Symbols")]
        public int MaxPositionsPerSymbol { get; set; }

        // --- Timeframes ---
        [Parameter("Liquidity Zone Timeframe", DefaultValue = "Hour4", Group = "Timeframes")]
        public TimeFrame ZoneTimeFrame { get; set; }

        [Parameter("Entry Timeframe", DefaultValue = "Minute15", Group = "Timeframes")]
        public TimeFrame EntryTimeFrame { get; set; }

        // --- Liquidity detection ---
        [Parameter("Pivot Bars (swing confirmation)", DefaultValue = 5, MinValue = 2, Group = "Liquidity")]
        public int PivotBars { get; set; }

        [Parameter("Equal Level Tolerance (x ATR)", DefaultValue = 0.15, MinValue = 0.05, Group = "Liquidity")]
        public double EqualLevelToleranceAtr { get; set; }

        [Parameter("Min Equal Touches", DefaultValue = 2, MinValue = 2, Group = "Liquidity")]
        public int MinEqualTouches { get; set; }

        [Parameter("Zone Padding (x ATR)", DefaultValue = 0.10, MinValue = 0.01, Group = "Liquidity")]
        public double ZonePaddingAtr { get; set; }

        [Parameter("Use Session Levels (PDH/L, PWH/L)", DefaultValue = true, Group = "Liquidity")]
        public bool UseSessionLevels { get; set; }

        [Parameter("Min Zone Strength (0-100)", DefaultValue = 60, MinValue = 0, MaxValue = 100, Group = "Liquidity")]
        public int MinZoneStrength { get; set; }

        [Parameter("Max Active Zones", DefaultValue = 12, MinValue = 5, Group = "Liquidity")]
        public int MaxActiveZones { get; set; }

        // --- Sweep confirmation ---
        [Parameter("Confirmation Bar Delay", DefaultValue = 1, MinValue = 0, Group = "Confirmation")]
        public int ConfirmationBarDelay { get; set; }

        [Parameter("Max Sweep Age (entry bars)", DefaultValue = 12, MinValue = 3, Group = "Confirmation")]
        public int MaxSweepAgeBars { get; set; }

        [Parameter("Require Structure Shift (MSS)", DefaultValue = true, Group = "Confirmation")]
        public bool RequireStructureShift { get; set; }

        [Parameter("Structure Pivot Bars", DefaultValue = 3, MinValue = 2, Group = "Confirmation")]
        public int StructurePivotBars { get; set; }

        [Parameter("Min Wick/Body Ratio", DefaultValue = 0.8, MinValue = 0.1, Group = "Confirmation")]
        public double MinWickBodyRatio { get; set; }

        // --- Risk ---
        [Parameter("Risk Mode", DefaultValue = RiskMode.PercentEquity, Group = "Risk")]
        public RiskMode RiskModeSetting { get; set; }

        [Parameter("Fixed USD Risk", DefaultValue = 100, MinValue = 1, Group = "Risk")]
        public double FixedUsdRisk { get; set; }

        [Parameter("Risk Percent", DefaultValue = 0.8, MinValue = 0.01, Group = "Risk")]
        public double RiskPercent { get; set; }

        [Parameter("Max Spread (pips, 0=off)", DefaultValue = 0, MinValue = 0, Group = "Risk")]
        public double MaxSpreadPips { get; set; }

        [Parameter("Stop Buffer (x ATR)", DefaultValue = 0.05, MinValue = 0, Group = "Risk")]
        public double StopBufferAtr { get; set; }

        [Parameter("ATR Period", DefaultValue = 14, MinValue = 5, Group = "Risk")]
        public int AtrPeriod { get; set; }

        // --- Profit ---
        [Parameter("Reward:Risk Ratio", DefaultValue = 2.0, MinValue = 0.5, Group = "Profit")]
        public double RewardRiskRatio { get; set; }

        [Parameter("Move SL to Breakeven at 1R", DefaultValue = true, Group = "Profit")]
        public bool MoveToBreakevenAt1R { get; set; }

        [Parameter("Partial Close at 1R", DefaultValue = true, Group = "Profit")]
        public bool PartialCloseAt1R { get; set; }

        [Parameter("Partial Close %", DefaultValue = 50, MinValue = 10, MaxValue = 90, Group = "Profit")]
        public double PartialClosePercent { get; set; }

        [Parameter("Use Trailing Stop", DefaultValue = true, Group = "Profit")]
        public bool UseTrailingStop { get; set; }

        [Parameter("Trailing Stop %", DefaultValue = 0.35, MinValue = 0.1, Group = "Profit")]
        public double TrailingStopPercent { get; set; }

        [Parameter("Activate Trail After R", DefaultValue = 1.5, MinValue = 0.5, Group = "Profit")]
        public double ActivateTrailAfterR { get; set; }

        [Parameter("Draw Zones On Chart", DefaultValue = true, Group = "Visual")]
        public bool DrawZonesOnChart { get; set; }

        private readonly Dictionary<string, SymbolTradingContext> _contexts = new Dictionary<string, SymbolTradingContext>();
        private RiskManager _riskManager;
        private ProfitManager _profitManager;

        protected override void OnStart()
        {
            _riskManager = new RiskManager(
                RiskModeSetting,
                FixedUsdRisk,
                RiskPercent,
                MaxSpreadPips,
                StopBufferAtr);

            _profitManager = new ProfitManager(
                RewardRiskRatio,
                MoveToBreakevenAt1R,
                PartialCloseAt1R,
                PartialClosePercent,
                UseTrailingStop,
                TrailingStopPercent,
                ActivateTrailAfterR);

            foreach (var symbol in ResolveSymbols())
            {
                RegisterSymbol(symbol);
            }

            if (_contexts.Count == 0)
            {
                Print("No tradable symbols resolved. Attach bot to BTCUSD chart or set Extra Symbols.");
                Stop();
            }
        }

        protected override void OnTick()
        {
            foreach (var ctx in _contexts.Values)
                _profitManager.ManageOpenPositions(this, ctx.Symbol);
        }

        private void RegisterSymbol(Symbol symbol)
        {
            var zoneBars = MarketData.GetBars(ZoneTimeFrame, symbol.Name);
            var entryBars = MarketData.GetBars(EntryTimeFrame, symbol.Name);
            var atr = Indicators.AverageTrueRange(entryBars, AtrPeriod, MovingAverageType.Simple);

            var zoneEngine = new LiquidityZoneEngine(
                PivotBars,
                EqualLevelToleranceAtr,
                MinEqualTouches,
                ZonePaddingAtr,
                UseSessionLevels,
                MaxActiveZones);

            var sweepEngine = new SweepConfirmationEngine(
                ConfirmationBarDelay,
                MaxSweepAgeBars,
                RequireStructureShift,
                StructurePivotBars,
                MinWickBodyRatio,
                MinZoneStrength);

            var ctx = new SymbolTradingContext(symbol, zoneBars, entryBars, atr, zoneEngine, sweepEngine);
            _contexts[symbol.Name] = ctx;

            zoneBars.BarClosed += args =>
            {
                if (args.Bars.SymbolName != symbol.Name)
                    return;
                ProcessSymbol(ctx, isZoneBarEvent: true);
            };

            entryBars.BarClosed += args =>
            {
                if (args.Bars.SymbolName != symbol.Name)
                    return;
                ProcessSymbol(ctx, isZoneBarEvent: false);
            };

            Print($"Registered {symbol.Name} | Zone TF: {ZoneTimeFrame} | Entry TF: {EntryTimeFrame}");
        }

        private void ProcessSymbol(SymbolTradingContext ctx, bool isZoneBarEvent)
        {
            ctx.EvaluateAtBarClose();

            if (DrawZonesOnChart && isZoneBarEvent)
                DrawZones(ctx);

            if (!isZoneBarEvent)
            {
                _profitManager.ManageOpenPositions(this, ctx.Symbol);
                TryExecuteEntries(ctx);
            }
        }

        private void TryExecuteEntries(SymbolTradingContext ctx)
        {
            if (!_riskManager.IsSpreadAcceptable(ctx.Symbol))
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
            double entry = setup.Direction == TradeType.Buy ? symbol.Ask : symbol.Bid;
            int atrIndex = Math.Max(0, ctx.EntryBars.Count - 1);
            double atr = ctx.Atr.Result[atrIndex];

            double stopLoss = _riskManager.BuildStopLoss(setup.Direction, setup.SweepWickExtreme, atr, symbol);
            double takeProfit = symbol.NormalizePrice(
                _profitManager.CalculateTakeProfit(setup.Direction, entry, stopLoss));

            double riskAmount = _riskManager.CalculateRiskAmount(Account);
            double volume = _riskManager.CalculateVolumeInUnits(symbol, entry, stopLoss, riskAmount);

            if (volume <= 0)
            {
                Print($"{symbol.Name}: volume too small for risk settings.");
                return;
            }

            var result = ExecuteMarketOrder(setup.Direction, symbol.Name, volume, "LQ_Sweep", stopLoss, takeProfit);
            if (result.IsSuccessful)
            {
                _profitManager.RegisterPosition(result.Position, stopLoss);
                Print($"{symbol.Name} {setup.Direction} | Zone: {setup.Zone.Label} @ {setup.Zone.LevelPrice:F2} | SL {stopLoss:F2} | TP {takeProfit:F2} | R:R {RewardRiskRatio}");
            }
            else
            {
                Print($"{symbol.Name} order failed: {result.Error}");
            }
        }

        private void DrawZones(SymbolTradingContext ctx)
        {
            Chart.RemoveAllObjects();

            foreach (var zone in ctx.LastZones.Take(8))
            {
                var color = zone.Side == LiquiditySide.BuySide ? Color.FromArgb(60, 220, 80, 80) : Color.FromArgb(60, 80, 160, 255);
                Chart.DrawRectangle(
                    $"zone_{zone.Source}_{zone.LevelPrice}",
                    ctx.ZoneBars.OpenTimes[Math.Max(0, ctx.ZoneBars.Count - 120)],
                    zone.ZoneTop,
                    ctx.ZoneBars.LastBar.OpenTime,
                    zone.ZoneBottom,
                    color,
                    1,
                    LineStyle.Solid);
            }
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
                    Print($"Symbol not found: {name}");
                    continue;
                }
                yield return symbol;
            }
        }
    }
}
