# MarketDNA — Phase 1A Development Foundation

Production-ready monorepo scaffold for the MarketDNA desktop research platform.

Architecture documentation remains authoritative under `docs/architecture/` (**frozen**).

---

## Phase 1A scope

Repository & development foundation only:

- pnpm + Turborepo monorepo
- Electron + React + Vite + Tailwind desktop shell
- Shared packages: `shared`, `domain`, `database`, `analytics-client`
- ESLint, Prettier, Vitest, TypeScript, GitHub Actions
- Environment, configuration, and logging frameworks

**Not in Phase 1A:** database schema, business logic, Python analytics, optimisation, AI, recommendations, data ingestion, feature UI.

---

## Prerequisites

- Node.js ≥ 20
- pnpm 9.15.x (`corepack enable && corepack prepare pnpm@9.15.0 --activate`)

---

## Install

```bash
pnpm install
```

Copy environment template if needed:

```bash
cp .env.example .env
```

---

## Build

```bash
pnpm build
```

Builds all packages and the desktop renderer + Electron main/preload.

---

## Test

```bash
pnpm test
```

---

## Lint / typecheck / format

```bash
pnpm lint
pnpm typecheck
pnpm format:check
```

---

## Run (desktop shell)

```bash
pnpm dev
```

Opens Electron displaying:

```text
MarketDNA
Version 0.1.0
Application Initialised Successfully
```

---

## Workspace packages

| Package | Role |
|---|---|
| `@marketdna/shared` | Config, env, logging, app identity |
| `@marketdna/domain` | Ubiquitous language types (no business logic) |
| `@marketdna/database` | Persistence boundary stub (no schema) |
| `@marketdna/analytics-client` | Analytics worker client stub (no Python) |
| `@marketdna/desktop` | Electron + React blank shell |
| `@marketdna/eslint-config` | Shared ESLint flat config |
| `@marketdna/typescript-config` | Shared TSConfigs |

---

## Architecture docs

See `docs/architecture/README.md`. Do not redesign architecture in this phase.

## Phase 1B (preview only — not started)

Contracts hardening, local-api skeleton, workspace bootstrap plumbing, and deeper IPC — still **no** full engines or schema-heavy product features unless separately approved.
