// Atlas BTC Omni Bot — all researched timeframes & triggers (see TIMEFRAME_MATRIX.md)
// Attach to M1–Weekly (cTrader). Entry trigger = Auto picks best trigger for that TF (see TIMEFRAME_MATRIX.md).

using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    public enum OmniEntryTrigger
    {
        Auto,
        Donchian20,
        Donchian55,
        AdxDonchian20,
        AdxDonchian55,
        AdxDonchian20Strict,
        AdxRisingBreak20,
        EmaCross12_26,
        EmaCross21_55,
        EmaCross50_200,
        TripleEmaPullback,
        MacdCross,
        MacdHistTurn,
        RsiTrendPullback,
        Rsi50Cross,
        BollingerBreakout,
        BollingerMeanRevert,
        KeltnerBreakout,
        StochCross,
        Cci100Cross,
        DiCross,
        RocMomentum12,
        IchimokuTkCross,
        VolumeSpikeBreak,
        SupertrendFlip,
        WilliamsRReversal,
        ParabolicSarFlip,
        AroonCross25,
        MfiReversal,
        SmaCross50_200,
        EmaCross9_21,
        Donchian10
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None, AddIndicators = true)]
    public class AtlasBtcOmniBot : Robot
    {
        private const string Label = "Atlas-BTC-Omni";

        [Parameter("Entry trigger", Group = "Signal", DefaultValue = OmniEntryTrigger.Auto)]
        public OmniEntryTrigger EntryTrigger { get; set; }

        [Parameter("Higher-TF EMA55 filter", Group = "Signal", DefaultValue = true)]
        public bool UseHigherTfFilter { get; set; }

        [Parameter("Filter timeframe (manual override)", Group = "Signal", DefaultValue = TimeFrame.Hour4)]
        public TimeFrame FilterTimeFrame { get; set; }

        [Parameter("Min ADX (ADX triggers)", Group = "Signal", DefaultValue = 22, MinValue = 5)]
        public double MinAdx { get; set; }

        [Parameter("Donchian period (custom)", Group = "Signal", DefaultValue = 20, MinValue = 5)]
        public int DonchianPeriod { get; set; }

        [Parameter("ATR period", Group = "Exit", DefaultValue = 14, MinValue = 5)]
        public int AtrPeriod { get; set; }

        [Parameter("Stop = ATR ×", Group = "Exit", DefaultValue = 2.5, MinValue = 0.5)]
        public double StopAtrMultiple { get; set; }

        [Parameter("Take-profit R", Group = "Exit", DefaultValue = 2.5, MinValue = 1.0)]
        public double RewardRiskMultiple { get; set; }

        [Parameter("Risk % equity", Group = "Risk", DefaultValue = 1.0, MinValue = 0.1)]
        public double RiskPercent { get; set; }

        [Parameter("Max positions", Group = "Risk", DefaultValue = 1, MinValue = 1)]
        public int MaxPositions { get; set; }

        [Parameter("Allow long", Group = "Risk", DefaultValue = true)]
        public bool AllowLong { get; set; }

        [Parameter("Allow short", Group = "Risk", DefaultValue = true)]
        public bool AllowShort { get; set; }

        [Parameter("Log signals", Group = "Debug", DefaultValue = true)]
        public bool LogSignals { get; set; }

        private OmniEntryTrigger _activeTrigger;
        private AverageTrueRange _atr;
        private DirectionalMovementSystem _dms;
        private MacdCrossOver _macd;
        private RelativeStrengthIndex _rsi;
        private BollingerBands _bb;
        private StochasticOscillator _stoch;
        private CommodityChannelIndex _cci;
        private IchimokuKinkoHyo _ichimoku;
        private ParabolicSAR _psar;
        private WilliamsPctR _williamsR;
        private AroonOscillator _aroon;
        private MoneyFlowIndex _mfi;
        private SimpleMovingAverage _sma50, _sma200;
        private ExponentialMovingAverage _ema9;

        private Bars _filterBars;
        private ExponentialMovingAverage _htfEma55;
        private ExponentialMovingAverage _ema8, _ema12, _ema21, _ema26, _ema50, _ema55, _ema200;
        private ExponentialMovingAverage _keltnerMid;

        protected override void OnStart()
        {
            _activeTrigger = EntryTrigger == OmniEntryTrigger.Auto ? RecommendTrigger(TimeFrame) : EntryTrigger;
            ApplyAutoRiskReward(TimeFrame);

            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Exponential);
            _dms = Indicators.DirectionalMovementSystem(14);
            _macd = Indicators.MacdCrossOver(12, 26, 9);
            _rsi = Indicators.RelativeStrengthIndex(Bars.ClosePrices, 14);
            _bb = Indicators.BollingerBands(Bars.ClosePrices, 20, 2, MovingAverageType.Simple);
            _stoch = Indicators.StochasticOscillator(14, 3, 3, MovingAverageType.Simple);
            _cci = Indicators.CommodityChannelIndex(20);
            _ichimoku = Indicators.IchimokuKinkoHyo(9, 26, 52);
            _psar = Indicators.ParabolicSAR(0.02, 0.2);
            _williamsR = Indicators.WilliamsPctR(14);
            _aroon = Indicators.AroonOscillator(25);
            _mfi = Indicators.MoneyFlowIndex(14);
            _sma50 = Indicators.SimpleMovingAverage(Bars.ClosePrices, 50);
            _sma200 = Indicators.SimpleMovingAverage(Bars.ClosePrices, 200);
            _ema9 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 9);
            _ema8 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 8);
            _ema12 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 12);
            _ema21 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 21);
            _ema26 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 26);
            _ema50 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 50);
            _ema55 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 55);
            _ema200 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 200);
            _keltnerMid = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 20);

            if (UseHigherTfFilter)
            {
                var htf = ResolveFilterTimeFrame();
                _filterBars = MarketData.GetBars(htf);
                _htfEma55 = Indicators.ExponentialMovingAverage(_filterBars.ClosePrices, 55);
            }

            if (LogSignals)
            {
                Print("Atlas Omni on {0} {1} | Trigger={2} | HTF filter={3} | SL={4:F1}×ATR TP={5:F1}R",
                    SymbolName, TimeFrame, _activeTrigger, UseHigherTfFilter, StopAtrMultiple, RewardRiskMultiple);
            }
        }

        protected override void OnBarClosed()
        {
            if (Bars.Count < 260)
                return;
            if (Positions.Count(p => p.Label == Label && p.SymbolName == SymbolName) >= MaxPositions)
                return;

            bool wantLong, wantShort;
            EvaluateSignal(out wantLong, out wantShort);

            if (UseHigherTfFilter && _filterBars != null)
            {
                bool htfUp = _filterBars.ClosePrices.Last(1) > _htfEma55.Result.Last(1);
                bool htfDown = _filterBars.ClosePrices.Last(1) < _htfEma55.Result.Last(1);
                wantLong &= htfUp;
                wantShort &= htfDown;
            }

            double atr = _atr.Result.Last(1);
            if (atr <= 0)
                return;

            if (AllowLong && wantLong)
                Open(TradeType.Buy, atr);
            else if (AllowShort && wantShort)
                Open(TradeType.Sell, atr);
        }

        private void EvaluateSignal(out bool wantLong, out bool wantShort)
        {
            wantLong = wantShort = false;
            int i = 1;

            switch (_activeTrigger)
            {
                case OmniEntryTrigger.Donchian20:
                case OmniEntryTrigger.Donchian55:
                    DonchianSignal(i, _activeTrigger == OmniEntryTrigger.Donchian55 ? 55 : 20, false, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.AdxDonchian20:
                    DonchianSignal(i, 20, true, 22, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.AdxDonchian55:
                    DonchianSignal(i, 55, true, 22, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.AdxDonchian20Strict:
                    DonchianSignal(i, 20, true, 25, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.AdxRisingBreak20:
                    AdxRisingDonchian(i, 20, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.EmaCross12_26:
                    EmaCrossSignal(i, 12, 26, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.EmaCross21_55:
                    EmaCrossSignal(i, 21, 55, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.EmaCross50_200:
                    EmaCrossSignal(i, 50, 200, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.TripleEmaPullback:
                    TripleEmaSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.MacdCross:
                    MacdCrossSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.MacdHistTurn:
                    MacdHistSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.RsiTrendPullback:
                    RsiPullbackSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.Rsi50Cross:
                    Rsi50Signal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.BollingerBreakout:
                    BollingerBreakSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.BollingerMeanRevert:
                    BollingerRevertSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.KeltnerBreakout:
                    KeltnerBreakSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.StochCross:
                    StochSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.Cci100Cross:
                    CciSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.DiCross:
                    DiCrossSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.RocMomentum12:
                    RocSignal(i, 12, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.IchimokuTkCross:
                    IchimokuSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.VolumeSpikeBreak:
                    VolumeBreakSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.SupertrendFlip:
                    SupertrendSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.WilliamsRReversal:
                    WilliamsSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.ParabolicSarFlip:
                    PsarSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.AroonCross25:
                    AroonSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.MfiReversal:
                    MfiSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.SmaCross50_200:
                    SmaCrossSignal(i, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.EmaCross9_21:
                    EmaCrossSignal(i, 9, 21, out wantLong, out wantShort);
                    break;
                case OmniEntryTrigger.Donchian10:
                    DonchianSignal(i, 10, false, out wantLong, out wantShort);
                    break;
            }
        }

        // --- Signal builders (use Last(1) = last closed bar) ---

        private void DonchianSignal(int i, int period, bool useAdx, out bool lng, out bool shrt)
        {
            DonchianSignal(i, period, useAdx, MinAdx, out lng, out shrt);
        }

        private void DonchianSignal(int i, int period, bool useAdx, double adxMin, out bool lng, out bool shrt)
        {
            double priorHigh = HighestHigh(period, i + 1);
            double priorLow = LowestLow(period, i + 1);
            double c1 = Bars.ClosePrices.Last(i);
            double c2 = Bars.ClosePrices.Last(i + 1);
            lng = c2 <= priorHigh && c1 > priorHigh;
            shrt = c2 >= priorLow && c1 < priorLow;
            if (useAdx)
            {
                double adx = _dms.ADX.Last(i);
                bool diUp = _dms.DIPlus.Last(i) > _dms.DIMinus.Last(i);
                bool diDn = _dms.DIMinus.Last(i) > _dms.DIPlus.Last(i);
                lng &= adx >= adxMin && diUp;
                shrt &= adx >= adxMin && diDn;
            }
        }

        private void AdxRisingDonchian(int i, int period, out bool lng, out bool shrt)
        {
            DonchianSignal(i, period, true, 20, out lng, out shrt);
            bool rising = _dms.ADX.Last(i) > _dms.ADX.Last(i + 3);
            lng &= rising;
            shrt &= rising;
        }

        private void EmaCrossSignal(int i, int fast, int slow, out bool lng, out bool shrt)
        {
            var ef = EmaByPeriod(fast);
            var es = EmaByPeriod(slow);
            lng = ef.Result.Last(i + 1) <= es.Result.Last(i + 1) && ef.Result.Last(i) > es.Result.Last(i);
            shrt = ef.Result.Last(i + 1) >= es.Result.Last(i + 1) && ef.Result.Last(i) < es.Result.Last(i);
        }

        private ExponentialMovingAverage EmaByPeriod(int period)
        {
            switch (period)
            {
                case 8: return _ema8;
                case 9: return _ema9;
                case 12: return _ema12;
                case 21: return _ema21;
                case 26: return _ema26;
                case 50: return _ema50;
                case 55: return _ema55;
                case 200: return _ema200;
                default: return Indicators.ExponentialMovingAverage(Bars.ClosePrices, period);
            }
        }

        private void TripleEmaSignal(int i, out bool lng, out bool shrt)
        {
            bool up = _ema8.Result.Last(i) > _ema21.Result.Last(i) && _ema21.Result.Last(i) > _ema55.Result.Last(i);
            bool dn = _ema8.Result.Last(i) < _ema21.Result.Last(i) && _ema21.Result.Last(i) < _ema55.Result.Last(i);
            lng = up && Bars.ClosePrices.Last(i + 1) <= _ema21.Result.Last(i + 1) && Bars.ClosePrices.Last(i) > _ema21.Result.Last(i);
            shrt = dn && Bars.ClosePrices.Last(i + 1) >= _ema21.Result.Last(i + 1) && Bars.ClosePrices.Last(i) < _ema21.Result.Last(i);
        }

        private void MacdCrossSignal(int i, out bool lng, out bool shrt)
        {
            lng = _macd.MACD.Last(i + 1) <= _macd.Signal.Last(i + 1) && _macd.MACD.Last(i) > _macd.Signal.Last(i);
            shrt = _macd.MACD.Last(i + 1) >= _macd.Signal.Last(i + 1) && _macd.MACD.Last(i) < _macd.Signal.Last(i);
        }

        private void MacdHistSignal(int i, out bool lng, out bool shrt)
        {
            double h0 = _macd.Histogram.Last(i);
            double h1 = _macd.Histogram.Last(i + 1);
            lng = h1 <= 0 && h0 > 0;
            shrt = h1 >= 0 && h0 < 0;
        }

        private void RsiPullbackSignal(int i, out bool lng, out bool shrt)
        {
            double r0 = _rsi.Result.Last(i);
            double r1 = _rsi.Result.Last(i + 1);
            lng = Bars.ClosePrices.Last(i) > _ema200.Result.Last(i) && r1 < 40 && r0 >= 40;
            shrt = Bars.ClosePrices.Last(i) < _ema200.Result.Last(i) && r1 > 60 && r0 <= 60;
        }

        private void Rsi50Signal(int i, out bool lng, out bool shrt)
        {
            lng = _rsi.Result.Last(i + 1) <= 50 && _rsi.Result.Last(i) > 50;
            shrt = _rsi.Result.Last(i + 1) >= 50 && _rsi.Result.Last(i) < 50;
        }

        private void BollingerBreakSignal(int i, out bool lng, out bool shrt)
        {
            lng = Bars.ClosePrices.Last(i + 1) <= _bb.Top.Last(i + 1) && Bars.ClosePrices.Last(i) > _bb.Top.Last(i);
            shrt = Bars.ClosePrices.Last(i + 1) >= _bb.Bottom.Last(i + 1) && Bars.ClosePrices.Last(i) < _bb.Bottom.Last(i);
        }

        private void BollingerRevertSignal(int i, out bool lng, out bool shrt)
        {
            lng = Bars.ClosePrices.Last(i) < _bb.Bottom.Last(i) && Bars.ClosePrices.Last(i) > Bars.ClosePrices.Last(i + 1);
            shrt = Bars.ClosePrices.Last(i) > _bb.Top.Last(i) && Bars.ClosePrices.Last(i) < Bars.ClosePrices.Last(i + 1);
        }

        private void KeltnerBreakSignal(int i, out bool lng, out bool shrt)
        {
            double band = 2.0 * _atr.Result.Last(i);
            double up = _keltnerMid.Result.Last(i) + band;
            double lo = _keltnerMid.Result.Last(i) - band;
            lng = Bars.ClosePrices.Last(i + 1) <= up && Bars.ClosePrices.Last(i) > up;
            shrt = Bars.ClosePrices.Last(i + 1) >= lo && Bars.ClosePrices.Last(i) < lo;
        }

        private void StochSignal(int i, out bool lng, out bool shrt)
        {
            lng = _stoch.PercentK.Last(i + 1) <= _stoch.PercentD.Last(i + 1) && _stoch.PercentK.Last(i) > _stoch.PercentD.Last(i)
                  && _stoch.PercentK.Last(i) < 80;
            shrt = _stoch.PercentK.Last(i + 1) >= _stoch.PercentD.Last(i + 1) && _stoch.PercentK.Last(i) < _stoch.PercentD.Last(i)
                   && _stoch.PercentK.Last(i) > 20;
        }

        private void CciSignal(int i, out bool lng, out bool shrt)
        {
            lng = _cci.Result.Last(i + 1) <= 100 && _cci.Result.Last(i) > 100;
            shrt = _cci.Result.Last(i + 1) >= -100 && _cci.Result.Last(i) < -100;
        }

        private void DiCrossSignal(int i, out bool lng, out bool shrt)
        {
            lng = _dms.DIPlus.Last(i + 1) <= _dms.DIMinus.Last(i + 1) && _dms.DIPlus.Last(i) > _dms.DIMinus.Last(i);
            shrt = _dms.DIPlus.Last(i + 1) >= _dms.DIMinus.Last(i + 1) && _dms.DIPlus.Last(i) < _dms.DIMinus.Last(i);
        }

        private void RocSignal(int i, int period, out bool lng, out bool shrt)
        {
            double roc0 = (Bars.ClosePrices.Last(i) / Bars.ClosePrices.Last(i + period) - 1) * 100;
            double roc1 = (Bars.ClosePrices.Last(i + 1) / Bars.ClosePrices.Last(i + 1 + period) - 1) * 100;
            lng = roc1 <= 0 && roc0 > 0;
            shrt = roc1 >= 0 && roc0 < 0;
        }

        private void IchimokuSignal(int i, out bool lng, out bool shrt)
        {
            lng = _ichimoku.TenkanSen.Last(i + 1) <= _ichimoku.KijunSen.Last(i + 1)
                  && _ichimoku.TenkanSen.Last(i) > _ichimoku.KijunSen.Last(i);
            shrt = _ichimoku.TenkanSen.Last(i + 1) >= _ichimoku.KijunSen.Last(i + 1)
                   && _ichimoku.TenkanSen.Last(i) < _ichimoku.KijunSen.Last(i);
        }

        private void VolumeBreakSignal(int i, out bool lng, out bool shrt)
        {
            double volMa = 0;
            for (int k = i; k < i + 20; k++)
                volMa += Bars.TickVolumes.Last(k);
            volMa /= 20;
            bool spike = Bars.TickVolumes.Last(i) > 1.8 * volMa;
            double hi = HighestHigh(20, i + 1);
            double lo = LowestLow(20, i + 1);
            double c = Bars.ClosePrices.Last(i);
            lng = spike && c > hi;
            shrt = spike && c < lo;
        }

        private void SupertrendSignal(int i, out bool lng, out bool shrt)
        {
            // Proxy: price vs Keltner mid ± 3 ATR band flip
            double band = 3.0 * _atr.Result.Last(i);
            double up = _keltnerMid.Result.Last(i) + band;
            double lo = _keltnerMid.Result.Last(i) - band;
            lng = Bars.ClosePrices.Last(i + 1) <= up && Bars.ClosePrices.Last(i) > up;
            shrt = Bars.ClosePrices.Last(i + 1) >= lo && Bars.ClosePrices.Last(i) < lo;
        }

        private void WilliamsSignal(int i, out bool lng, out bool shrt)
        {
            lng = _williamsR.Result.Last(i + 1) <= -80 && _williamsR.Result.Last(i) > -80;
            shrt = _williamsR.Result.Last(i + 1) >= -20 && _williamsR.Result.Last(i) < -20;
        }

        private void PsarSignal(int i, out bool lng, out bool shrt)
        {
            bool bull = Bars.ClosePrices.Last(i) > _psar.Result.Last(i);
            bool bullPrev = Bars.ClosePrices.Last(i + 1) > _psar.Result.Last(i + 1);
            lng = !bullPrev && bull;
            shrt = bullPrev && !bull;
        }

        private void AroonSignal(int i, out bool lng, out bool shrt)
        {
            double up = _aroon.Up.Last(i);
            double down = _aroon.Down.Last(i);
            double up1 = _aroon.Up.Last(i + 1);
            double down1 = _aroon.Down.Last(i + 1);
            lng = up1 <= down1 && up > down && up > 50;
            shrt = down1 <= up1 && down > up && down > 50;
        }

        private void MfiSignal(int i, out bool lng, out bool shrt)
        {
            lng = _mfi.Result.Last(i + 1) <= 20 && _mfi.Result.Last(i) > 20;
            shrt = _mfi.Result.Last(i + 1) >= 80 && _mfi.Result.Last(i) < 80;
        }

        private void SmaCrossSignal(int i, out bool lng, out bool shrt)
        {
            lng = _sma50.Result.Last(i + 1) <= _sma200.Result.Last(i + 1) && _sma50.Result.Last(i) > _sma200.Result.Last(i);
            shrt = _sma50.Result.Last(i + 1) >= _sma200.Result.Last(i + 1) && _sma50.Result.Last(i) < _sma200.Result.Last(i);
        }

        private void Open(TradeType side, double atr)
        {
            double entry = Bars.ClosePrices.Last(1);
            double stopDist = StopAtrMultiple * atr;
            double sl = side == TradeType.Buy ? entry - stopDist : entry + stopDist;
            double tp = side == TradeType.Buy ? entry + RewardRiskMultiple * stopDist : entry - RewardRiskMultiple * stopDist;

            long vol = VolumeForRisk(stopDist);
            if (vol < Symbol.VolumeInUnitsMin)
                return;

            var res = ExecuteMarketOrder(side, SymbolName, vol, Label);
            if (!res.IsSuccessful)
                return;
            ModifyPosition(res.Position, sl, tp, ProtectionType.Absolute);
            if (LogSignals)
                Print("{0} {1} trigger={2}", side, SymbolName, _activeTrigger);
        }

        private long VolumeForRisk(double stopDistancePrice)
        {
            double riskCash = Account.Equity * (RiskPercent / 100.0);
            if (Symbol.TickSize <= 0 || Symbol.TickValue <= 0 || stopDistancePrice <= 0)
                return 0;
            double ticks = stopDistancePrice / Symbol.TickSize;
            return Symbol.NormalizeVolumeInUnits(riskCash / (ticks * Symbol.TickValue), RoundingMode.Down);
        }

        private double HighestHigh(int period, int startOffset)
        {
            double max = double.MinValue;
            for (int k = startOffset; k < startOffset + period; k++)
                max = Math.Max(max, Bars.HighPrices.Last(k));
            return max;
        }

        private double LowestLow(int period, int startOffset)
        {
            double min = double.MaxValue;
            for (int k = startOffset; k < startOffset + period; k++)
                min = Math.Min(min, Bars.LowPrices.Last(k));
            return min;
        }

        private TimeFrame ResolveFilterTimeFrame()
        {
            if (TimeFrame == TimeFrame.Minute) return TimeFrame.Minute5;
            if (TimeFrame == TimeFrame.Minute3) return TimeFrame.Minute15;
            if (TimeFrame == TimeFrame.Minute5) return TimeFrame.Minute15;
            if (TimeFrame == TimeFrame.Minute15) return TimeFrame.Hour;
            if (TimeFrame == TimeFrame.Minute30) return TimeFrame.Hour4;
            if (TimeFrame == TimeFrame.Hour) return TimeFrame.Hour4;
            if (TimeFrame == TimeFrame.Hour2) return TimeFrame.Hour4;
            if (TimeFrame == TimeFrame.Hour3) return TimeFrame.Hour4;
            if (TimeFrame == TimeFrame.Hour4) return TimeFrame.Daily;
            if (TimeFrame == TimeFrame.Daily) return TimeFrame.Weekly;
            return FilterTimeFrame;
        }

        private static OmniEntryTrigger RecommendTrigger(TimeFrame tf)
        {
            if (tf == TimeFrame.Minute) return OmniEntryTrigger.EmaCross9_21;
            if (tf == TimeFrame.Minute3) return OmniEntryTrigger.Donchian10;
            if (tf == TimeFrame.Minute5) return OmniEntryTrigger.AdxDonchian20;
            if (tf == TimeFrame.Minute15) return OmniEntryTrigger.EmaCross50_200;
            if (tf == TimeFrame.Minute30) return OmniEntryTrigger.VolumeSpikeBreak;
            if (tf == TimeFrame.Hour) return OmniEntryTrigger.Cci100Cross;
            if (tf == TimeFrame.Hour2) return OmniEntryTrigger.MacdCross;
            if (tf == TimeFrame.Hour3) return OmniEntryTrigger.AdxDonchian55;
            if (tf == TimeFrame.Hour4) return OmniEntryTrigger.AdxDonchian20;
            if (tf == TimeFrame.Daily) return OmniEntryTrigger.AdxRisingBreak20;
            if (tf == TimeFrame.Weekly) return OmniEntryTrigger.Donchian55;
            return OmniEntryTrigger.AdxDonchian20;
        }

        private void ApplyAutoRiskReward(TimeFrame tf)
        {
            if (EntryTrigger != OmniEntryTrigger.Auto)
                return;
            if (tf == TimeFrame.Minute || tf == TimeFrame.Minute3)
            {
                StopAtrMultiple = 1.5;
                RewardRiskMultiple = 2.0;
            }
            if (tf == TimeFrame.Minute5 || tf == TimeFrame.Minute15 || tf == TimeFrame.Hour2 || tf == TimeFrame.Daily)
            {
                StopAtrMultiple = 2.0;
                RewardRiskMultiple = 2.0;
            }
            if (tf == TimeFrame.Minute30)
            {
                StopAtrMultiple = 2.5;
                RewardRiskMultiple = 3.0;
            }
        }
    }
}
