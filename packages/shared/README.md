# @marketdna/shared

Foundation utilities for MarketDNA.

## Contents (Phase 1A)

| Module | Role |
|---|---|
| `config` | Zod-validated application configuration |
| `env` | Environment variable access helpers |
| `logging` | Structured logging via Pino |
| `constants` | Application identity constants |

## Usage

```ts
import { loadAppConfig, createLogger, APP_NAME, APP_VERSION } from '@marketdna/shared';

const config = loadAppConfig();
const log = createLogger('bootstrap');
log.info({ app: APP_NAME, version: APP_VERSION }, 'initialised');
```

## Boundaries

- No domain business logic
- No database access
- No analytics / Python bridge
