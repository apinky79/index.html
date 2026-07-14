# Architecture Principles

Market Intelligence AI is designed to institutional standards: modular cores, stable contracts, offline-first local truth, and replaceable engines.

## Non-negotiables

1. **Contracts over implementations** — Modules communicate through versioned interfaces (TypeScript types / OpenAPI / protobuf / JSON Schema). Implementations may be swapped without cascading rewrites.
2. **Offline-first local truth** — The authoritative workspace state lives on disk (SQLite + artifact store). Network sources enrich; they do not own the source of truth.
3. **Robustness before peak performance** — Optimisation and recommendation pipelines must discourage curve fitting. Peak equity or raw profit alone is never a ranking key.
4. **Explain every decision** — Recommendations, regime labels, and rejected parameter sets carry structured evidence objects consumable by UI and future AI chat.
5. **Side-effect isolation** — Data ingestion, optimisation, and recommendation writes go through explicit command boundaries; UI never mutates domain tables directly.
6. **Deterministic where possible** — Same inputs + same seed + same code version → same output. Non-determinism is confined to explicitly stochastic stages (e.g. Monte Carlo) and recorded.
7. **Failure is a first-class state** — Jobs, ingestions, and model inferences surface typed errors, retries, and resumability — not silent defaults.
8. **Security of local secrets** — API keys, broker tokens, and LLM keys use OS keychain / encrypted vault; never plain-text in SQLite or config JSON by default.
9. **Observability** — Structured logs, job traces, and performance metrics from day one of Phase 1.
10. **Plugin safety** — Future plugins run sandboxed with declared capabilities; core never imports vendor SDK code deeply into domain logic.

## Layering model

```
┌─────────────────────────────────────────────────────────┐
│  Presentation (Electron Renderer / React)               │
├─────────────────────────────────────────────────────────┤
│  Application shell (IPC, routing, session, settings UI) │
├─────────────────────────────────────────────────────────┤
│  Local API / Application services (commands + queries)  │
├─────────────────────────────────────────────────────────┤
│  Domain services (regime, recommendation, optimisation) │
├─────────────────────────────────────────────────────────┤
│  Infrastructure (SQLite, FS artifacts, Python workers)  │
├─────────────────────────────────────────────────────────┤
│  Adapters (data providers, brokers, plugins)            │
└─────────────────────────────────────────────────────────┘
```

Dependency rule: **upper layers depend downward; adapters depend on domain ports, never the reverse.**

## CQRS-lite

- **Commands** mutate state (import bars, start optimisation, accept recommendation).
- **Queries** read projections (regime snapshot, recommendation cards, chart series).
- Optimisation and recommendation produce **immutable run artifacts**; UI compares versions rather than mutating past runs.

## Instrument abstraction

Never hard-code `BTCUSD` in domain logic. Use:

- `InstrumentId` (stable internal UUID / ULID)
- `symbol` (display / external code)
- `assetClass` (`crypto` | `fx` | `metal` | `index` | …)
- `quoteCurrency`, `baseCurrency` (where applicable)
- `providerMappings` (per data feed / broker)

v1 seeds six instruments; schema supports unlimited.

## Curve-fitting defenses (product-level)

Embedded in architecture, not bolted on later:

- Walk-forward / out-of-sample split contracts in optimisation schema
- Parameter stability / plateau scoring
- Multi-objective fitness (drawdown, expectancy, trade count floors)
- Regime-conditioned evaluation (params ranked per regime, not only global)
- Overfit risk scores attached to every recommendation
- Historical optimisation memory as priors, not as hard targets

## Versioning

| Artifact | Version field |
|---|---|
| Domain contracts | SemVer (`@mia/contracts`) |
| DB schema | Prisma migrations |
| Recommendation algorithm | `engineVersion` on each run |
| Plugins | Manifest `apiVersion` compatible range |
| Settings schema | `settingsSchemaVersion` with migrators |

## What v1 deliberately excludes

v1 builds the **foundation** only: shell, contracts, local DB, ingest pipeline stubs, regime skeleton, recommendation skeleton, plugin SPI, roadmap-aligned scaffolding. No full ML trainers, no broker execution, no cloud sync, no Telegram, no production Monte Carlo UI — those land in later roadmap phases with stable extension points already defined here.
