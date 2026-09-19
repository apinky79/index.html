# cTrader build notes (newer cAlgo API)

If you see **CS1061** on `ExponentialMovingAverage`, your cTrader build uses the unified MA API:

```csharp
// Old (may not compile)
_ema50 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 50);

// New
_ema50 = Indicators.MovingAverage(Bars.ClosePrices, 50, MovingAverageType.Exponential);
```

Field types: use `MovingAverage`, not `ExponentialMovingAverage`.

**H4 ADX** — bars first:

```csharp
_h4Dms = Indicators.DirectionalMovementSystem(_h4Bars, 14);
```

**Volume** — `NormalizeVolumeInUnits` returns `double`:

```csharp
private double VolumeForRisk(...) { ... return Symbol.NormalizeVolumeInUnits(...); }
double vol = VolumeForRisk(stopDist);
ExecuteMarketOrder(side, SymbolName, vol, Label);
```

Copy the latest **`LearnedBtcAutopilotBot.cs`** from the repo; it includes these fixes.
