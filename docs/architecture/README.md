# Architecture Documentation Index

Complete foundation architecture for **Market Intelligence AI**.  
No application runtime code is included in this phase (by design).

| Order | Document | Covers |
|---|---|---|
| 0a | [00-principles.md](00-principles.md) | Design principles, layering, versioning |
| 0b | [00-tech-stack.md](00-tech-stack.md) | Stack choices and rationale |
| 1 | [01-system-architecture.md](01-system-architecture.md) | Full software architecture |
| 2 | [02-folder-structure.md](02-folder-structure.md) | Monorepo + runtime workspace layout |
| 3 | [03-module-breakdown.md](03-module-breakdown.md) | Modules and replaceability |
| 4 | [04-database-design.md](04-database-design.md) | Logical DB design |
| 5 | [05-entity-relationship.md](05-entity-relationship.md) | ER diagrams |
| 6 | [06-api-architecture.md](06-api-architecture.md) | IPC, commands/queries, gRPC |
| 7 | [07-desktop-architecture.md](07-desktop-architecture.md) | Electron / React desktop |
| 8 | [08-ai-architecture.md](08-ai-architecture.md) | Classical → ML → generative layers |
| 9 | [09-data-ingestion.md](09-data-ingestion.md) | Ingest pipelines |
| 10 | [10-optimisation-engine.md](10-optimisation-engine.md) | Optimisation engine |
| 11 | [11-recommendation-engine.md](11-recommendation-engine.md) | Recommendation engine |
| 12 | [12-local-database.md](12-local-database.md) | Local SQLite + artifacts |
| 13 | [13-settings-storage.md](13-settings-storage.md) | Settings & secrets |
| 14 | [14-plugin-architecture.md](14-plugin-architecture.md) | Plugin SPI |
| 15 | [../roadmap/development-roadmap.md](../roadmap/development-roadmap.md) | Phased roadmap |
| 16 | [16-quantitative-intelligence-architecture.md](16-quantitative-intelligence-architecture.md) | **Quantitative intelligence layer** — six research engines |

> **North star:** Doc 16 reframes the product as a quantitative hedge-fund research platform. Optimisation is a corpus input; the primary artifact is an evidence-backed `RecommendationDocket`.

Start at the [root README](../../README.md) for product framing.
