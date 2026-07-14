# Phase 2 — Import Engine Compliance

**Status:** Implemented  
**Architecture:** Frozen (docs 00–17 not redesigned)

## Delivered

| Requirement | Evidence |
|---|---|
| Formats `.optres` `.cbotset` CSV JSON | `@marketdna/import-engine` parsers + fixtures |
| Drag and drop | `ImportWorkbench` drop zone → buffer IPC |
| File browser | Electron `dialog.showOpenDialog` |
| Import history | `workspace-index.json` imports ledger + UI |
| Validation / error reporting | `validateParsedDraft` + issue UI |
| Duplicate detection | SHA-256 content fingerprint |
| Metadata extraction | Strategy/symbol/timeframe/range/metrics |
| Progress indicator | Queue progress events + UI bar |
| Import queue | `ImportQueue` serial worker |
| Domain mapping | `OptimisationRun` + `OptimisationTrial[]` |
| Scale posture | NDJSON trial artifacts + paginated reads |
| Browse imported .optres | Runs table + trial pager |
| Unit tests | import-engine + database + shared |

## Explicit exclusions (honoured)

- No optimisation search / walk-forward / Monte Carlo logic
- No AI / recommendations / regime analysis
- No market-data OHLC ingest

## Storage note

Phase 2 uses a workspace JSON index + NDJSON trial artifacts under `userData/workspaces/default`, matching the architecture rule that bulk trials live as artifacts while metadata remains queryable. Full Prisma schema remains deferred.
