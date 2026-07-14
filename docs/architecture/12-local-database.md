# Local Database Design

## Role of the local database

The local DB is the **offline system of record** for metadata, relationships, job state, and pointers to large artifacts. It enables:

- Portable workspaces
- Crash-safe job recovery
- Fast UI queries for lists and status
- Future sync by attaching cursors to row versions

Detailed table list: [04-database-design.md](04-database-design.md)

## Physical deployment

| Item | Choice |
|---|---|
| Engine | SQLite 3 with WAL mode |
| Access | Prisma Client from `local-api` only |
| Location | Under Electron `userData/workspaces/<id>/workspace.sqlite` |
| Backups | Timed copy to `backups/workspace-YYYYMMDD-HHMMSS.sqlite` |
| Multi-process | Single writer (local-api); Python reads via replica file or read-only URI / Parquet |

### Writer discipline

- Only `local-api` opens SQLite read-write
- Python workers read Parquet + optional read-only SQLite snapshot or query via gRPC “catalog” RPCs for small metadata
- Avoid dual-writer corruption

## Dual-store consistency

SQLite ↔ Artifacts consistency protocol:

1. Write artifacts first (or stage)
2. Commit DB pointers in a transaction
3. On success, publish
4. GC orphan artifacts on a maintenance job
5. Tombstone DB rows if artifact missing → mark `CORRUPT` for repair UI

## Retention policies (settings-driven)

| Data | Default |
|---|---|
| Job events | 30 days detailed; aggregate thereafter |
| Optimisation trials parquet | Keep until user deletes run |
| Ingest staging | Delete immediately after commit |
| Logs | 14 days |
| Backups | Last 10 copies |

## Performance practices

- Covering indexes for main list screens
- Paginate runs/recommendations
- Avoid unbounded `job_event` growth — cap rows per job or spill to JSONL
- `VACUUM` / `ANALYZE` on maintenance schedule (idle)

## Portability / export

Workspace export format (future):

```text
*.miaworkspace (zip)
  manifest.json
  workspace.sqlite
  artifacts/**
```

Import rewrites absolute paths → relative.

## Encryption (phase-gated)

- Optional SQLCipher or encrypted disk volume guidance for regulated users
- Secrets still not in DB (keychain refs only)
- Architecture allows `ciphered=true` flag in manifest without rewriting domain schema

## Seed data

On first launch:

- Create default workspace
- Seed six instruments
- Seed regime taxonomy v1
- Seed default settings
- Seed metric definitions

## Integrity repair on boot

1. Apply migrations
2. Jobs in `running` → `interrupted`
3. Verify critical artifact paths for recent runs
4. Record `last_boot_ok` in `app_meta`

## Testing

- Migration tests from empty + from N-1 schema
- Concurrent read with write smoke
- Corruption simulation fixtures
