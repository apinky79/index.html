# Entity Relationship Diagram

## Core ERD (logical)

```mermaid
erDiagram
  WORKSPACE ||--o{ INSTRUMENT : contains
  WORKSPACE ||--o{ JOB : tracks
  WORKSPACE ||--o{ STRATEGY_DEFINITION : owns

  INSTRUMENT ||--o{ INSTRUMENT_PROVIDER_MAP : maps
  INSTRUMENT ||--o{ SERIES : has
  INSTRUMENT ||--o{ REGIME_SNAPSHOT : assessed
  INSTRUMENT ||--o{ OPTIMISATION_RUN : optimised
  INSTRUMENT ||--o{ RECOMMENDATION_SET : recommended

  SERIES ||--o{ SERIES_SEGMENT : partitions
  SERIES ||--o{ DATA_QUALITY_ISSUE : flags
  SERIES ||--o{ INGEST_JOB : via

  STRATEGY_DEFINITION ||--o{ OPTIMISATION_RUN : runs
  OPTIMISATION_RUN ||--o{ OPTIMISATION_FOLD : folds
  OPTIMISATION_RUN ||--o{ PARAMETER_PLATEAU : finds
  OPTIMISATION_RUN ||--o| OPTIMISATION_RESULT_REF : points

  REGIME_TAXONOMY ||--o{ REGIME_SNAPSHOT : labels
  REGIME_MODEL ||--o{ REGIME_SNAPSHOT : produced_by
  REGIME_SNAPSHOT ||--o{ REGIME_SEGMENT : expands

  RECOMMENDATION_SET ||--o{ RECOMMENDATION_ITEM : ranks
  RECOMMENDATION_SET }o--|| REGIME_SNAPSHOT : conditioned_on
  RECOMMENDATION_SET }o--o| OPTIMISATION_RUN : uses_memory
  RECOMMENDATION_ITEM ||--o| EXPLANATION_DOCUMENT : explains
  RECOMMENDATION_ITEM ||--o| OVERFIT_ASSESSMENT : risks
  RECOMMENDATION_ITEM ||--o{ RECOMMENDATION_FEEDBACK : receives
  RECOMMENDATION_ITEM ||--o{ OUTCOME_EVENT : validates

  JOB ||--o{ JOB_EVENT : emits
  PLUGIN_INSTALL ||--o{ PLUGIN_PERMISSION : grants

  WORKSPACE ||--o{ SETTINGS_ENTRY : configures
  WORKSPACE ||--o{ SECRET_REF : references
```

## Recommendation context (detail)

```mermaid
erDiagram
  RECOMMENDATION_SET {
    string id PK
    string instrument_id FK
    string regime_snapshot_id FK
    string status
    string engine_version
    datetime as_of_ts
    string config_hash
  }

  RECOMMENDATION_ITEM {
    string id PK
    string recommendation_set_id FK
    int rank
    json parameters
    float robustness_score
    float suitability_score
    string strategy_id FK
  }

  EXPLANATION_DOCUMENT {
    string id PK
    string recommendation_item_id FK
    json summary
    string artifact_path
  }

  OVERFIT_ASSESSMENT {
    string id PK
    string recommendation_item_id FK
    float risk_score
    json flags
  }

  RECOMMENDATION_FEEDBACK {
    string id PK
    string recommendation_item_id FK
    string action
    string note
    datetime created_at
  }
```

## Optimisation context (detail)

```mermaid
erDiagram
  STRATEGY_DEFINITION {
    string id PK
    string name
    string version
    json parameter_schema
    json default_constraints
  }

  OPTIMISATION_RUN {
    string id PK
    string strategy_id FK
    string instrument_id FK
    string timeframe_id
    string status
    string engine_version
    string data_fingerprint
    int seed
    json objective_spec
    string artifact_dir
  }

  OPTIMISATION_FOLD {
    string id PK
    string optimisation_run_id FK
    int fold_index
    datetime train_start
    datetime train_end
    datetime test_start
    datetime test_end
  }

  PARAMETER_PLATEAU {
    string id PK
    string optimisation_run_id FK
    json center_params
    json bounds
    float stability_score
  }

  OPTIMISATION_RESULT_REF {
    string optimisation_run_id PK
    string trials_parquet_path
    json top_n_summary
    json metric_stats
  }
```

## Market data context (detail)

```mermaid
erDiagram
  INSTRUMENT {
    string id PK
    string symbol
    string asset_class
    boolean is_active
  }

  SERIES {
    string id PK
    string instrument_id FK
    string timeframe_id
    string source_kind
    int bar_count
    datetime first_ts
    datetime last_ts
    string content_fingerprint
  }

  SERIES_SEGMENT {
    string id PK
    string series_id FK
    string relative_path
    datetime start_ts
    datetime end_ts
    string checksum
  }

  DATA_QUALITY_ISSUE {
    string id PK
    string series_id FK
    string issue_type
    datetime ts
    json details
  }
```

## Cardinality notes

| Relationship | Cardinality | Rule |
|---|---|---|
| Instrument → Series | 1:N | Multiple timeframes/sources allowed |
| OptimisationRun → Folds | 1:N | Walk-forward partitions |
| RecommendationSet → Items | 1:N | Typically top-K (K configurable, default 5) |
| Item → Explanation | 1:1 | Mandatory for published recommendations |
| Item → Feedback | 1:N | User may revise feedback |
| Item → Outcomes | 1:N | Multiple live windows possible |

## Artifact ER (filesystem, not SQL)

```text
artifacts/
  bars/{seriesId}/{yyyy}/{mm}.parquet
  features/{snapshotId}.parquet
  opt-runs/{runId}/
    config.json
    trials.parquet
    folds.json
    plateaus.json
    report.json
  recommendations/{setId}/
    items/{itemId}.json
    explanation/{itemId}.json
  models/regime/{modelId}/
```

SQL rows reference these paths; deleting a run deletes SQL + artifact directory in one command transaction (best-effort FS cleanup with tombstones if FS fails).
