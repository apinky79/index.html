# MarketDNA

Quantitative market intelligence research platform (architecture frozen under `docs/architecture/`).

## Current phase: Phase 2 — Import Engine

Import, validate, parse, and store optimisation corpora (`.optres`, `.cbotset`, CSV, JSON). Browse imported runs and trials in the desktop app.

## Prerequisites

- Node.js ≥ 20
- pnpm 9.15.x

## Install

```bash
pnpm install
```

## Build / test

```bash
pnpm build
pnpm test
pnpm lint
pnpm typecheck
```

## Run

```bash
pnpm dev
```

Use **Browse files** or drag-and-drop a sample from `data/samples/optimisation/sample.optres`.

## Packages

| Package | Role |
|---|---|
| `@marketdna/import-engine` | Parsers, validation, queue, domain mapping |
| `@marketdna/database` | Workspace index + NDJSON trial artifacts |
| `@marketdna/domain` | Ubiquitous language types |
| `@marketdna/shared` | Config / logging / ids |
| `@marketdna/desktop` | Electron + React Import workbench |

## Docs

- Architecture (frozen): `docs/architecture/`
- Phase 2 compliance: `docs/phases/phase-2-import-engine.md`
