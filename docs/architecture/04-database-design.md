# Database Design

## Storage strategy (polyglot local)

| Store | Role | Contents |
|---|---|---|
| **SQLite** (`workspace.sqlite`) | System of record for metadata & transactions | Instruments, jobs, runs, recommendations, settings refs, plugins |
| **Parquet partitions** | High-volume analytical arrays | OHLCV bars, feature matrices, optimisation trial grids |
| **JSON / JSONL artifacts** | Human-auditable side documents | Explanations, engine configs, diagnostics |
| **Optional DuckDB** (later) | OLAP acceleration | Read-only views over Parquet + SQLite attachments |

**Rule:** Never store millions of OHLC rows as SQLite rows in v1. SQLite holds *pointers*, quality stats, and fingerprints; bars live columnar on disk.

## Logical schemas (SQLite)

### Identity & workspace

| Table | Purpose |
|---|---|
| `workspace` | Workspace id, name, created/updated, schema version |
| `device_profile` | Local device id for future sync |
| `app_meta` | Key/value boot metadata (last migration, last repair) |

### Catalog

| Table | Purpose |
|---|---|
| `instrument` | Canonical instruments |
| `instrument_alias` | Alternate symbols |
| `instrument_provider_map` | External provider codes |
| `timeframe` | Canonical timeframe enum/table (`M1`…`D1`, custom) |

`instrument` key fields:

- `id` (ULID)
- `symbol` (`BTCUSD`)
- `display_name`
- `asset_class`
- `base_code` / `quote_code` (nullable for indices)
- `tick_size` / `point_value` (nullable until broker map exists)
- `is_active`
- `created_at` / `updated_at`

### Market data

| Table | Purpose |
|---|---|---|
| `series` | One series per (instrument, timeframe, source) |
| `series_segment` | Contiguous file partitions (path, start/end, checksum) |
| `ingest_job` | Import/stream job linkage |
| `data_quality_issue` | Gap/spike/duplicate records |

`series` fields include `bar_count`, `first_ts`, `last_ts`, `content_fingerprint`, `source_kind` (`csv|provider|broker|replay`).

### Structure & macro

| Table | Purpose |
|---|---|---|
| `structure_snapshot` | Windowed structure features summary + artifact path |
| `macro_event` | Calendar/news events (populate later) |
| `macro_context_snapshot` | Aggregated macro features for a window |

### Regime

| Table | Purpose |
|---|---|---|
| `regime_taxonomy` | Versioned label dictionary |
| `regime_model` | Model metadata (name, version, artifact) |
| `regime_snapshot` | Point-in-time current assessment |
| `regime_segment` | Labeled contiguous regime intervals |
| `regime_transition` | Detected transitions for analytics |

### Strategy & optimisation

| Table | Purpose |
|---|---|---|
| `strategy_definition` | Strategy identity + parameter schema JSON |
| `parameter_space` | Search space constraints (could be JSON on strategy) |
| `optimisation_run` | One optimisation execution |
| `optimisation_fold` | Walk-forward folds / OOS partitions |
| `optimisation_metric_def` | Metric catalog (Sharpe, MaxDD, …) |
| `optimisation_result_ref` | Pointer to Parquet trials + top-N summary JSON |
| `parameter_plateau` | Detected stable regions |

`optimisation_run` critical fields:

- `strategy_id`, `instrument_id`, `timeframe_id`
- `status`, `engine_version`, `config_hash`, `data_fingerprint`
- `seed`, `started_at`, `finished_at`
- `artifact_dir`
- `objective_spec` (JSON: multi-objective weights / constraints)

### Recommendation & explanation

| Table | Purpose |
|---|---|---|
| `recommendation_set` | One recommendation request/result bundle |
| `recommendation_item` | Ranked parameter sets |
| `recommendation_metric` | Per-item metric snapshot |
| `explanation_document` | Structured explanation + path to verbose artifact |
| `overfit_assessment` | Risk scores / flags |
| `recommendation_feedback` | Accept/reject + notes |

### Feedback / learning

| Table | Purpose |
|---|---|---|
| `outcome_import` | Bot/trading performance import batch |
| `outcome_event` | Linked live outcomes vs recommendation items |
| `learning_prior` | Aggregated priors derived from history (versioned) |

### Jobs, plugins, settings

| Table | Purpose |
|---|---|---|
| `job` | Generic async job row |
| `job_event` | Progress / log milestones (or JSONL file + pointer) |
| `plugin_install` | Installed plugins |
| `plugin_permission` | Granted capabilities |
| `settings_entry` | Non-secret settings (JSON values) |
| `secret_ref` | Keychain account/service pointers only |

## Indexing guidance

- `(instrument_id, timeframe_id)` on `series`
- `(status, created_at)` on `job` and `optimisation_run`
- `(instrument_id, as_of_ts)` on `regime_snapshot`
- `(recommendation_set_id, rank)` on `recommendation_item`
- Unique: `instrument.symbol`, `series(instrument_id,timeframe_id,source_kind)` as product policy

## Referential integrity

- Soft deletes preferred for instruments with historical runs (`is_active=false`)
- Hard delete cascaded only for draft workspace reset commands
- Artifact paths are relative to workspace root; DB never stores other users’ absolute home paths in portable export format (rewrite on open)

## Migration policy

1. Prisma migrations shipped with the app
2. On boot: open DB → migrate → integrity check → job repair
3. Breaking artifact schema versions require `engine_version` gates and explicit upgrade jobs

## Sample Prisma-shaped sketch (illustrative, not runnable code drop)

Conceptual models (names only):

`Workspace`, `Instrument`, `Series`, `IngestJob`, `RegimeSnapshot`, `StrategyDefinition`, `OptimisationRun`, `RecommendationSet`, `RecommendationItem`, `ExplanationDocument`, `Job`, `PluginInstall`, `SettingsEntry`, `SecretRef`

Full field-level ERD: [05-entity-relationship.md](05-entity-relationship.md)  
Local file layout & retention: [12-local-database.md](12-local-database.md)
