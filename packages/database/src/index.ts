export {
  openWorkspaceDatabase,
  type WorkspaceDatabase,
  type WorkspaceIndex,
  type ImportHistoryEntry,
  type ListTrialsOptions,
} from './workspace-db.js';

/** @deprecated Phase 1A stub retained for compatibility — prefer openWorkspaceDatabase. */
export {
  DATABASE_PACKAGE_STATUS,
  createDatabaseClient,
  type DatabaseClient,
  type DatabaseClientOptions,
  type DatabasePackageStatus,
} from './client.js';
