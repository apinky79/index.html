# @marketdna/database

Persistence boundary for MarketDNA.

## Phase 1A scope

- Package boundary and lifecycle stub only
- **No Prisma schema**
- **No migrations**
- **No domain table definitions**

Later phases introduce SQLite + Prisma per `docs/architecture/04-database-design.md`
and `docs/architecture/12-local-database.md`.

## API (stub)

```ts
import { createDatabaseClient } from '@marketdna/database';

const db = createDatabaseClient({ workspacePath: '/tmp/ws' });
await db.ping(); // throws until Phase 1B+ wiring
```
