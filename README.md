# Market Intelligence AI

**Commercial-grade desktop platform for market regime analysis and robust trading parameter recommendation.**

> This repository currently contains the **foundation architecture and implementation plan only**. No application runtime code has been generated yet, by design.

---

## What this product is

Market Intelligence AI is organised like a **quantitative hedge fund research platform**.

It helps algorithmic traders:

1. Classify the **current market regime** (with supporting and contradicting evidence)
2. Find **historically similar** market periods
3. Mine **Strategy DNA** — robust parameter families across regimes (not peak-profit trials)
4. Stress-test candidates with a full **robustness** battery
5. Issue **evidence-backed parameter range recommendations** for future conditions
6. **Learn** by grading every recommendation against realised outcomes

It is **not** a backtester product and **not** a price-prediction engine.  
Optimisation imports are research inputs; the intellectual product is the recommendation docket.

See the quantitative intelligence layer: [docs/architecture/16-quantitative-intelligence-architecture.md](docs/architecture/16-quantitative-intelligence-architecture.md).

---

## Documentation index

| # | Deliverable | Document |
|---|---|---|
| 1 | Full software architecture | [docs/architecture/01-system-architecture.md](docs/architecture/01-system-architecture.md) |
| 2 | Folder structure | [docs/architecture/02-folder-structure.md](docs/architecture/02-folder-structure.md) |
| 3 | Module breakdown | [docs/architecture/03-module-breakdown.md](docs/architecture/03-module-breakdown.md) |
| 4 | Database design | [docs/architecture/04-database-design.md](docs/architecture/04-database-design.md) |
| 5 | Entity relationship diagram | [docs/architecture/05-entity-relationship.md](docs/architecture/05-entity-relationship.md) |
| 6 | API architecture | [docs/architecture/06-api-architecture.md](docs/architecture/06-api-architecture.md) |
| 7 | Desktop application architecture | [docs/architecture/07-desktop-architecture.md](docs/architecture/07-desktop-architecture.md) |
| 8 | AI architecture | [docs/architecture/08-ai-architecture.md](docs/architecture/08-ai-architecture.md) |
| 9 | Data ingestion architecture | [docs/architecture/09-data-ingestion.md](docs/architecture/09-data-ingestion.md) |
| 10 | Optimisation engine architecture | [docs/architecture/10-optimisation-engine.md](docs/architecture/10-optimisation-engine.md) |
| 11 | Recommendation engine architecture | [docs/architecture/11-recommendation-engine.md](docs/architecture/11-recommendation-engine.md) |
| 12 | Local database design | [docs/architecture/12-local-database.md](docs/architecture/12-local-database.md) |
| 13 | Settings storage design | [docs/architecture/13-settings-storage.md](docs/architecture/13-settings-storage.md) |
| 14 | Plugin architecture | [docs/architecture/14-plugin-architecture.md](docs/architecture/14-plugin-architecture.md) |
| 15 | Development roadmap | [docs/roadmap/development-roadmap.md](docs/roadmap/development-roadmap.md) |
| 16 | Quantitative intelligence layer | [docs/architecture/16-quantitative-intelligence-architecture.md](docs/architecture/16-quantitative-intelligence-architecture.md) |
| 17 | Domain model (ubiquitous language) | [docs/architecture/17-domain-model.md](docs/architecture/17-domain-model.md) |

Supporting:

- [Tech stack rationale](docs/architecture/00-tech-stack.md)
- [Architecture principles](docs/architecture/00-principles.md)

---

## Design posture

| Principle | Meaning |
|---|---|
| Research-platform organisation | Engines mirror hedge-fund desks (regime, analogues, DNA, robustness, recommendation, learning) |
| Modular | Every engine is replaceable behind versioned research contracts |
| Offline-first | Local SQLite + local analytics; cloud is an optional future layer |
| Robustness over fit | DNA families and robustness gates outrank peak backtest equity |
| Evidence or abstain | No recommendation without an EvidenceGraph; abstention is valid |
| Expandable | Macro, on-chain, brokers, chat, cloud sync plug in without core rewrites |

---

## Initial markets (v1 data contracts)

`BTCUSD` · `ETHUSD` · `XAUUSD` · `EURUSD` · `NAS100` · `SPX500`

Instrument identity is abstracted (`InstrumentId`, `Symbol`, `AssetClass`, `Venue`) so the set is unbounded.

---

## Recommended stack (summary)

| Layer | Choice | Why (short) |
|---|---|---|
| Desktop shell | Electron | Mature for trading tools; Node + Chromium; native OS integration |
| UI | React + TypeScript | Strong ecosystem, fast charting libs, maintainable UI |
| Local API | Fastify (Node) or NestJS | Typed IPC/HTTP bridge between UI and services |
| Persistence | SQLite + Prisma | Offline-first, portable, migrations, type-safe access |
| Analytics / ML / backtest | Python (pandas, Polars, NumPy, scikit-learn) | Institutional-grade numerical stack |
| Process bridge | gRPC or ZeroMQ + typed IPC | Fast, language-agnostic UI ↔ Python workers |
| Charts | TradingView Lightweight Charts + Canvas/WebGL | High-performance OHLCV and overlays |
| Packaging | electron-builder | Commercial installers (Windows / macOS / Linux) |

Full rationale: [docs/architecture/00-tech-stack.md](docs/architecture/00-tech-stack.md)

---

## Status

**Phase 0 — Architecture complete.**  
Implementation begins at Roadmap Phase 1 (scaffold + contracts). See the [development roadmap](docs/roadmap/development-roadmap.md).
