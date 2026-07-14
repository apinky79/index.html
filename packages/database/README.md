# @marketdna/database

Persistence for MarketDNA.

## Phase 2

Implements the **optimisation import store**:

- Workspace metadata index (`workspace-index.json`)
- Per-run `run.json` + `trials.ndjson` artifacts (scalable append / paginated read)
- Import history ledger

Aligned with architecture polyglot storage:

- Metadata + pointers as the system of record index
- Bulk trials as columnar-friendly NDJSON artifacts (Parquet-ready layout)

Prisma schema for the full domain remains future work; this package now serves import browsing.
