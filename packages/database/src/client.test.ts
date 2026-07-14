import { describe, expect, it } from 'vitest';

import { DATABASE_PACKAGE_STATUS, createDatabaseClient } from './index.js';

describe('database package (Phase 1A stub)', () => {
  it('declares schema not ready', () => {
    expect(DATABASE_PACKAGE_STATUS.schemaReady).toBe(false);
    expect(DATABASE_PACKAGE_STATUS.phase).toBe('1A');
  });

  it('creates a client that reports not-ready on ping', async () => {
    const client = createDatabaseClient({ workspacePath: '/tmp/marketdna-ws' });
    const result = await client.ping();
    expect(result.ok).toBe(false);
    expect(result.reason).toMatch(/Phase 1A/);
  });

  it('rejects empty workspace paths', () => {
    expect(() => createDatabaseClient({ workspacePath: '  ' })).toThrow(/workspacePath/);
  });
});
