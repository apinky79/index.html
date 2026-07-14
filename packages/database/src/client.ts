/**
 * Legacy Phase 1A stub retained for compatibility.
 */
export const DATABASE_PACKAGE_STATUS = {
  phase: '2',
  schemaReady: true,
  engine: 'workspace-fs' as const,
  orm: 'json-index' as const,
} as const;

export type DatabasePackageStatus = typeof DATABASE_PACKAGE_STATUS;

export interface DatabaseClientOptions {
  workspacePath: string;
}

export interface DatabaseClient {
  readonly status: DatabasePackageStatus;
  readonly workspacePath: string;
  ping(): Promise<{ ok: boolean; reason?: string }>;
}

export function createDatabaseClient(options: DatabaseClientOptions): DatabaseClient {
  if (!options.workspacePath.trim()) {
    throw new Error('workspacePath is required');
  }

  return {
    status: DATABASE_PACKAGE_STATUS,
    workspacePath: options.workspacePath,
    async ping() {
      return { ok: true };
    },
  };
}
