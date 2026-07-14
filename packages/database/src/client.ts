/**
 * Database package status for Phase 1A.
 * Schema and Prisma arrive in a later phase — intentionally absent here.
 */
export const DATABASE_PACKAGE_STATUS = {
  phase: '1A',
  schemaReady: false,
  engine: 'sqlite' as const,
  orm: 'prisma' as const,
} as const;

export type DatabasePackageStatus = typeof DATABASE_PACKAGE_STATUS;

export interface DatabaseClientOptions {
  /** Absolute workspace directory that will later hold workspace.sqlite. */
  workspacePath: string;
}

/**
 * Persistence port — Phase 1A: connectivity stub only.
 * No queries, no schema, no migrations.
 */
export interface DatabaseClient {
  readonly status: DatabasePackageStatus;
  readonly workspacePath: string;
  /**
   * Health probe. Phase 1A always reports not-ready.
   */
  ping(): Promise<{ ok: false; reason: string }>;
}

/**
 * Create a database client stub for the given workspace path.
 */
export function createDatabaseClient(options: DatabaseClientOptions): DatabaseClient {
  if (!options.workspacePath.trim()) {
    throw new Error('workspacePath is required');
  }

  return {
    status: DATABASE_PACKAGE_STATUS,
    workspacePath: options.workspacePath,
    async ping() {
      return {
        ok: false,
        reason: 'Database schema is not available in Phase 1A',
      };
    },
  };
}
