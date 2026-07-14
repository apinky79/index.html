# Tech Stack Recommendation

## Decision summary

| Concern | Primary choice | Alternatives considered | Decision drivers |
|---|---|---|---|
| Desktop shell | **Electron** | Tauri, .NET Avalonia | Trading UI ecosystem, Node sidecar maturity, packaging maturity, hiring pool |
| UI language | **TypeScript + React 19** | Vue, Svelte, Flutter | Team velocity, chart libs, Electron congruence |
| Local UI styling | **CSS Modules + design tokens** (or Tailwind if team prefers) | MUI-heavy | Avoid card-dashboard default; full control for institutional chrome |
| Charting | **TradingView Lightweight Charts** + optional WebGL layer | Chart.js, D3 alone, Highcharts | Financial OHLCV performance, realtime updates |
| Local backend | **NestJS** (or Fastify + clean modules) | Express ad-hoc | Structure, DI, testability, CQRS-friendly |
| IPC | **Electron contextBridge + typed IPC** | Remote modules | Security, maintainability |
| Cross-language RPC | **gRPC over localhost** (protobuf) | REST, ZeroMQ alone | Typed contracts Node↔Python, streaming job progress |
| Analytics runtime | **Python 3.12+** | Node numerical, Julia, R | pandas/Polars, scikit-learn, research ecosystem |
| Heavy dataframes | **Polars** (+ pandas interop where needed) | pandas-only | Speed on large OHLCV / optimisation result tables |
| ML (future) | **scikit-learn** → optional **PyTorch** for deep models | TensorFlow | Incremental path; explainability first |
| Local DB | **SQLite** | DuckDB alone, Postgres local | Offline-first, portable DB file, Prisma support |
| Analytical store (optional later) | **DuckDB** embedded for OLAP queries | ClickHouse local | Fast aggregations over optimisation history without leaving device |
| ORM / migrations | **Prisma** | Drizzle, Knex, raw SQL | Type-safe schema, migrations, commercial DX |
| Job queue (local) | **BullMQ on Redis** *or* SQLite-backed job table | In-process only | Prefer **SQLite job table + worker processes** in v1 (zero Redis dependency) |
| Validation | **Zod** (TS) + **Pydantic** (Python) | Joi, marshmallow | Shared mental model, runtime safety |
| Testing | **Vitest / Playwright / pytest** | Jest | Modern, fast |
| Packaging | **electron-builder** | electron-forge | Installers, auto-update hooks |
| Logging | **pino** (Node) + structured Python logging | winston | Performance + JSON logs |
| Secrets | **OS keychain** via `keytar` / Electron safeStorage | Plain env | Commercial security baseline |

---

## Why Electron (not Tauri for v1)

- Institutional trading tools commonly ship Electron; support models and crash reporting are well understood.
- Python worker orchestration, Node-side Prisma, and charting are natural on Chromium + Node.
- Tauri is excellent for lean apps, but crypto/FX charting + heavy local analytics + plugin loading favour Electron’s maturity for **v1 commercial quality**. Revisit Tauri for a “Lite” SKU later behind the same contracts.

## Why React + TypeScript

- Strong typing across IPC payloads, domain DTOs, and plugin manifests.
- Concurrent UI patterns for long-running jobs (deferred values, transitions) map well to optimisation progress.
- Ecosystem for financial charts, virtualized tables (optimisation result grids), and accessibility.

## Why NestJS / modular Fastify for the local API

The “API” is primarily a **local application service** behind Electron IPC (and optional localhost HTTP for Python callbacks / debugging). NestJS brings:

- Module boundaries matching domain modules
- Dependency injection for swapping providers (data feeds, brokers)
- Guards for settings/license gates later
- Easy test harness without spinning Electron during unit tests

If the team wants lower ceremony, **Fastify + explicit module folders** is acceptable; the architecture does not depend on NestJS itself — only on **clear application modules**.

## Why SQLite + Prisma

- Single portable workspace file (or set of files) for offline use.
- ACID transactions for job state and recommendation acceptance.
- Prisma migrations give commercial upgrade paths between app versions.
- Instrument / run / recommendation history fit relational modeling well.

DuckDB may be added **beside** SQLite later for analytical scans over millions of optimisation rows — not as the transactional system of record.

## Why Python analytics workers

- Regime detection features, walk-forward engines, Monte Carlo, and ML live in Python.
- Polars/NumPy give institutional throughput on bar data.
- Researchers can evolve models without shipping Electron rebuilds if workers are versioned artifacts.

Communication: **gRPC streaming** for job progress + artifact paths; large tensors/results land as **Parquet / Arrow files** on disk, not in protobuf payloads.

## Why TradingView Lightweight Charts

- Optimized for streaming candles and markers.
- License/model familiar to traders.
- Complements (doesn’t try to replace) future TradingView integration as an adapter.

## Why not a remote-first backend in v1

Cloud sync, multi-device, and collaborative workspaces are **roadmap features**. Building remote-first now would fight offline-first requirements and slow the foundation. The domain model already includes `Workspace`, `DeviceId`, and `SyncCursor` placeholders for later.

## Package / monorepo layout

Recommended: **pnpm workspaces + Turborepo**

```
apps/
  desktop/          # Electron + React
  local-api/        # NestJS/Fastify application services
  python-engine/    # Analytics / optimisation / AI workers
packages/
  contracts/        # Shared TS types + protobuf / OpenAPI
  domain/           # Pure domain logic (TS)
  ui-kit/           # Design tokens + primitives
  plugin-sdk/       # Plugin SPI
```

Python may live as `apps/python-engine` with Poetry/uv; contracts mirrored via generated protobuf stubs.

## Charting & realtime strategy

- UI subscribes to **query projections** and **job event streams** (IPC).
- Market bars: append-only local store; renderer receives incremental candle updates.
- Avoid React re-render storms: chart libs mutate internally; React owns chrome + legends + recommendation panels.

## Licensing / commercial packaging (later)

- Code-signed installers
- Optional online license check (offline grace period)
- Feature flags via settings + license entitlements (plugin-capable)

---

## Explicit non-choices for v1

| Rejected for v1 | Reason |
|---|---|
| Postgres as primary DB | Unnecessary ops burden for desktop |
| Kubernetes / microservices cloud | Out of scope; wrong deployment model |
| Pure browser SPA | Loses native FS, workers, offline packaging |
| Monolithic Jupyter-in-app | Wrong UX for commercial desktop product |
| Training deep nets in UI process | Blocks UI; wrong process isolation |
