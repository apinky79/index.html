# @marketdna/import-engine

Import framework for optimisation corpora.

## Supported formats

| Extension | Role |
|---|---|
| `.optres` | cTrader-style optimisation result dumps (JSON key/value + passes) |
| `.cbotset` | cTrader parameter set (single trial run) |
| `.csv` | Tabular trials with `param_*` / metric columns |
| `.json` | MarketDNA native optimisation JSON |

## Responsibilities

- Detect format
- Validate structure
- Extract metadata
- Fingerprint for duplicate detection
- Map into `OptimisationRun` + `OptimisationTrial[]`
- Queue imports with progress events

## Non-goals

- No optimisation search
- No AI / recommendations / market regime analysis
