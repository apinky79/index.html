# Folder Structure

Monorepo layout designed for Electron + local API + Python engines + shared contracts.  
Names are normative for v1 scaffolding; packages may be renamed only with contract updates.

```text
market-intelligence-ai/
├── README.md
├── docs/
│   ├── architecture/                 # This architecture set
│   └── roadmap/
│       └── development-roadmap.md
├── package.json                      # pnpm workspace root
├── pnpm-workspace.yaml
├── turbo.json
├── .editorconfig
├── .gitignore
├── LICENSE
│
├── apps/
│   ├── desktop/                      # Electron application
│   │   ├── electron/
│   │   │   ├── main.ts               # Main process entry
│   │   │   ├── preload.ts            # contextBridge
│   │   │   ├── ipc/                  # Typed IPC routers
│   │   │   ├── windows/              # BrowserWindow factory
│   │   │   ├── menu/
│   │   │   ├── updater/              # electron-updater hooks
│   │   │   └── security/
│   │   ├── src/                      # React renderer
│   │   │   ├── main.tsx
│   │   │   ├── app/
│   │   │   │   ├── AppShell.tsx
│   │   │   │   ├── routes/
│   │   │   │   └── providers/
│   │   │   ├── features/             # UI feature modules
│   │   │   │   ├── workspace/
│   │   │   │   ├── market-data/
│   │   │   │   ├── charts/
│   │   │   │   ├── regime/
│   │   │   │   ├── optimisation/
│   │   │   │   ├── recommendations/
│   │   │   │   ├── jobs/
│   │   │   │   └── settings/
│   │   │   ├── shared/               # UI-only shared utilities
│   │   │   └── styles/               # Tokens, themes, global
│   │   ├── index.html
│   │   ├── package.json
│   │   ├── electron-builder.yml
│   │   ├── tsconfig.json
│   │   └── vite.config.ts
│   │
│   ├── local-api/                    # Application services (Node)
│   │   ├── src/
│   │   │   ├── main.ts
│   │   │   ├── modules/
│   │   │   │   ├── catalog/
│   │   │   │   ├── market-data/
│   │   │   │   ├── structure/
│   │   │   │   ├── macro/
│   │   │   │   ├── regime/
│   │   │   │   ├── optimisation/
│   │   │   │   ├── recommendation/
│   │   │   │   ├── feedback/
│   │   │   │   ├── jobs/
│   │   │   │   ├── settings/
│   │   │   │   └── plugins/
│   │   │   ├── infrastructure/
│   │   │   │   ├── prisma/
│   │   │   │   ├── artifacts/
│   │   │   │   ├── python-bridge/
│   │   │   │   ├── keychain/
│   │   │   │   └── logging/
│   │   │   └── health/
│   │   ├── prisma/
│   │   │   ├── schema.prisma
│   │   │   └── migrations/
│   │   ├── test/
│   │   └── package.json
│   │
│   └── python-engine/                # Analytics / ML / optimisation
│       ├── pyproject.toml            # uv or poetry
│       ├── README.md
│       ├── mia_engine/
│       │   ├── __init__.py
│       │   ├── grpc_server.py
│       │   ├── jobs/
│       │   ├── features/
│       │   ├── structure/
│       │   ├── regime/
│       │   ├── optimisation/
│       │   │   ├── search/
│       │   │   ├── walk_forward/
│       │   │   ├── monte_carlo/      # stub → full
│       │   │   └── scoring/
│       │   ├── recommendation/
│       │   ├── explanation/
│       │   ├── ml/                   # future trainers/inference
│       │   ├── io/                   # parquet, arrow, sqlite readers
│       │   └── util/
│       ├── proto/                    # symlink or copy of shared proto
│       └── tests/
│
├── packages/
│   ├── contracts/                    # Shared TypeScript DTOs + zod schemas
│   │   ├── src/
│   │   │   ├── instruments.ts
│   │   │   ├── market-data.ts
│   │   │   ├── regime.ts
│   │   │   ├── optimisation.ts
│   │   │   ├── recommendation.ts
│   │   │   ├── jobs.ts
│   │   │   ├── explanations.ts
│   │   │   └── index.ts
│   │   └── package.json
│   ├── proto/                        # Canonical protobuf definitions
│   │   ├── market_data.proto
│   │   ├── regime.proto
│   │   ├── optimisation.proto
│   │   ├── recommendation.proto
│   │   └── jobs.proto
│   ├── domain/                       # Pure TS domain helpers (no IO)
│   ├── ui-kit/                       # Design tokens + primitives
│   ├── plugin-sdk/                   # Plugin SPI for third parties
│   │   ├── src/
│   │   │   ├── manifest.ts
│   │   │   ├── capabilities.ts
│   │   │   ├── host.ts
│   │   │   └── index.ts
│   │   └── package.json
│   ├── eslint-config/
│   └── tsconfig/
│
├── plugins/                          # First-party plugins (optional)
│   ├── data-csv/
│   ├── data-stub/
│   └── notify-desktop/
│
├── data/                             # Dev-only sample datasets (git-lfs if needed)
│   └── samples/
│       ├── BTCUSD/
│       ├── ETHUSD/
│       ├── XAUUSD/
│       ├── EURUSD/
│       ├── NAS100/
│       └── SPX500/
│
├── scripts/
│   ├── bootstrap.sh
│   ├── generate-proto.sh
│   ├── seed-instruments.ts
│   └── doctor.ts                     # env health check
│
└── .github/
    └── workflows/
        ├── ci.yml
        └── release.yml
```

## Runtime workspace directory (user machine)

Not in repo — created per install:

```text
~/Library/Application Support/MarketIntelligenceAI/   # macOS example
├── workspaces/
│   └── default/
│       ├── workspace.sqlite
│       ├── workspace.sqlite-wal
│       ├── artifacts/
│       │   ├── bars/
│       │   ├── features/
│       │   ├── opt-runs/
│       │   ├── models/
│       │   └── explanations/
│       ├── plugins/
│       ├── logs/
│       └── cache/
├── settings.json                     # non-secret preferences
└── .lock
```

Windows / Linux equivalents via Electron `app.getPath('userData')`.

## Dependency direction

```text
desktop → contracts, ui-kit, plugin-sdk (types only)
local-api → contracts, domain, plugin-sdk, prisma
python-engine → proto stubs only (no Electron imports)
plugins → plugin-sdk + declared host APIs
```

**Forbidden:** `python-engine` importing `apps/desktop`; UI importing Prisma clients directly; plugins importing `local-api` internals.
