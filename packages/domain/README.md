# @marketdna/domain

TypeScript representation of the MarketDNA **ubiquitous language**.

Aligned with `docs/architecture/17-domain-model.md`.

## Phase 1A scope

- Type definitions and enums only
- No business logic
- No persistence
- No engine implementations

## Contents

| Area | Export |
|---|---|
| Identity | `EntityId`, `Instant` |
| Catalog | `Market`, `Instrument`, `Timeframe`, `AssetClass` |
| Intelligence | `MarketSnapshot`, `MarketRegime`, `RegimeLabel` |
| Optimisation | `OptimisationRun`, `StrategyDNA`, `ParameterRange` |
| Recommendations | `RecommendationDocket`, `Recommendation`, `Evidence` |
| Learning | `LearningRecord` |
| Bounded contexts | `BoundedContext` enum |

Future phases attach services and invariants without renaming these types.
