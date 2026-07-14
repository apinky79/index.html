import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, expect, it } from 'vitest';

import { ImportEngine } from './service.js';
import type { ImportEngineStore, MappedImport } from './index.js';

const fixturesDir = path.join(path.dirname(fileURLToPath(import.meta.url)), '../fixtures');

class MemoryStore implements ImportEngineStore {
  runs = new Map<string, MappedImport>();

  async findRunIdByFingerprint(contentHash: string) {
    for (const [id, mapped] of this.runs) {
      if (mapped.contentHash === contentHash) return id;
    }
    return undefined;
  }

  async saveImport(mapped: MappedImport) {
    this.runs.set(mapped.run.id, mapped);
  }
}

describe('ImportEngine', () => {
  it('imports an .optres file into domain run/trials', async () => {
    const store = new MemoryStore();
    const engine = new ImportEngine({
      workspaceRoot: os.tmpdir(),
      store,
    });

    const content = fs.readFileSync(path.join(fixturesDir, 'sample.optres'));
    const result = await engine.importFileSync({
      sourcePath: path.join(fixturesDir, 'sample.optres'),
      content,
    });

    expect(result.status).toBe('completed');
    expect(result.run?.instrumentSymbol).toBe('EURUSD');
    expect(result.trialCount).toBe(3);
    expect(store.runs.size).toBe(1);
    const mapped = [...store.runs.values()][0]!;
    expect(mapped.trials[0]?.parameters.values.emaFast).toBe(9);
  });

  it('detects duplicate content hashes', async () => {
    const store = new MemoryStore();
    const engine = new ImportEngine({ workspaceRoot: os.tmpdir(), store });
    const content = fs.readFileSync(path.join(fixturesDir, 'sample.json'));

    const first = await engine.importFileSync({
      sourcePath: 'sample.json',
      content,
    });
    const second = await engine.importFileSync({
      sourcePath: 'sample.json',
      content,
    });

    expect(first.status).toBe('completed');
    expect(second.status).toBe('duplicate');
    expect(second.duplicateOfRunId).toBe(first.run?.id);
  });
});
