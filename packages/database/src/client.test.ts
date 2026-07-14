import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, expect, it } from 'vitest';

import { ImportEngine } from '@marketdna/import-engine';

import { createDatabaseClient, openWorkspaceDatabase } from './index.js';

const fixturesDir = path.join(
  path.dirname(fileURLToPath(import.meta.url)),
  '../../import-engine/fixtures',
);

describe('database package', () => {
  it('reports ready ping via legacy client', async () => {
    const client = createDatabaseClient({ workspacePath: '/tmp/marketdna-ws' });
    const result = await client.ping();
    expect(result.ok).toBe(true);
  });

  it('persists imported runs and paginates trials', async () => {
    const workspacePath = await fs.mkdtemp(path.join(os.tmpdir(), 'marketdna-db-'));
    const db = await openWorkspaceDatabase(workspacePath);
    const engine = new ImportEngine({
      workspaceRoot: workspacePath,
      store: db.asImportStore(),
    });

    const content = await fs.readFile(path.join(fixturesDir, 'sample.optres'));
    const result = await engine.importFileSync({
      sourcePath: path.join(fixturesDir, 'sample.optres'),
      content,
    });
    await db.recordImportResult(result);

    expect(result.status).toBe('completed');
    const runs = await db.listRuns();
    expect(runs).toHaveLength(1);

    const trials = await db.listTrials(result.run!.id, { offset: 0, limit: 2 });
    expect(trials.total).toBe(3);
    expect(trials.trials).toHaveLength(2);

    const history = await db.listImportHistory();
    expect(history[0]?.status).toBe('completed');
  });
});
