import fs from 'node:fs/promises';
import path from 'node:path';

import type { OptimisationRun, OptimisationTrial } from '@marketdna/domain';
import type { ImportEngineStore, ImportJobResult, MappedImport } from '@marketdna/import-engine';

export interface WorkspaceIndex {
  version: 1;
  runs: Array<{
    id: string;
    contentHash: string;
    strategyName?: string;
    instrumentSymbol?: string;
    timeframeCode?: string;
    trialCount: number;
    sourceFileName?: string;
    importedAt?: string;
    format?: string;
    artifactDir: string;
  }>;
  imports: ImportHistoryEntry[];
}

export interface ImportHistoryEntry {
  jobId: string;
  status: ImportJobResult['status'];
  sourcePath: string;
  sourceFileName: string;
  contentHash: string;
  format?: string;
  runId?: string;
  trialCount?: number;
  duplicateOfRunId?: string;
  issues: ImportJobResult['issues'];
  completedAt?: string;
}

export interface ListTrialsOptions {
  offset?: number;
  limit?: number;
}

export interface WorkspaceDatabase {
  workspacePath: string;
  asImportStore(): ImportEngineStore;
  recordImportResult(result: ImportJobResult): Promise<void>;
  listRuns(): Promise<WorkspaceIndex['runs']>;
  getRun(runId: string): Promise<OptimisationRun | undefined>;
  listTrials(
    runId: string,
    options?: ListTrialsOptions,
  ): Promise<{ trials: OptimisationTrial[]; total: number }>;
  listImportHistory(): Promise<ImportHistoryEntry[]>;
  ping(): Promise<{ ok: true }>;
}

/**
 * Create / open a workspace database rooted at `workspacePath`.
 */
export async function openWorkspaceDatabase(workspacePath: string): Promise<WorkspaceDatabase> {
  if (!workspacePath.trim()) {
    throw new Error('workspacePath is required');
  }

  await fs.mkdir(workspacePath, { recursive: true });
  await fs.mkdir(path.join(workspacePath, 'artifacts'), { recursive: true });
  const indexPath = path.join(workspacePath, 'workspace-index.json');

  const readIndex = async (): Promise<WorkspaceIndex> => {
    try {
      const raw = await fs.readFile(indexPath, 'utf8');
      return JSON.parse(raw) as WorkspaceIndex;
    } catch {
      return { version: 1, runs: [], imports: [] };
    }
  };

  const writeIndex = async (index: WorkspaceIndex): Promise<void> => {
    await fs.writeFile(indexPath, JSON.stringify(index, null, 2), 'utf8');
  };

  const saveImport = async (
    mapped: MappedImport,
    sourcePath: string,
    format: string,
  ): Promise<void> => {
    const artifactDir = mapped.run.artifactDir.startsWith(workspacePath)
      ? mapped.run.artifactDir
      : path.join(workspacePath, 'artifacts', 'opt-runs', mapped.run.id);

    mapped.run.artifactDir = artifactDir.replace(/\\/g, '/');
    await fs.mkdir(artifactDir, { recursive: true });

    await fs.writeFile(
      path.join(artifactDir, 'run.json'),
      JSON.stringify(mapped.run, null, 2),
      'utf8',
    );

    const trialsPath = path.join(artifactDir, 'trials.ndjson');
    const streamChunks: string[] = [];
    for (const trial of mapped.trials) {
      streamChunks.push(JSON.stringify(trial));
    }
    await fs.writeFile(trialsPath, `${streamChunks.join('\n')}\n`, 'utf8');

    await fs.writeFile(
      path.join(artifactDir, 'meta.json'),
      JSON.stringify(
        {
          format,
          sourcePath,
          contentHash: mapped.contentHash,
          trialCount: mapped.trials.length,
          writtenAt: new Date().toISOString(),
        },
        null,
        2,
      ),
      'utf8',
    );

    const index = await readIndex();
    index.runs = index.runs.filter((run) => run.id !== mapped.run.id);
    index.runs.unshift({
      id: mapped.run.id,
      contentHash: mapped.contentHash,
      strategyName: mapped.run.strategyName,
      instrumentSymbol: mapped.run.instrumentSymbol,
      timeframeCode: mapped.run.timeframeCode,
      trialCount: mapped.trials.length,
      sourceFileName: mapped.run.sourceFileName,
      importedAt: mapped.run.importedAt,
      format,
      artifactDir: mapped.run.artifactDir,
    });
    await writeIndex(index);
  };

  const api: WorkspaceDatabase = {
    workspacePath,

    asImportStore(): ImportEngineStore {
      return {
        async findRunIdByFingerprint(contentHash: string) {
          const index = await readIndex();
          return index.runs.find((run) => run.contentHash === contentHash)?.id;
        },
        async saveImport(mapped, sourcePath, format) {
          await saveImport(mapped, sourcePath, format);
        },
      };
    },

    async recordImportResult(result) {
      const index = await readIndex();
      index.imports.unshift({
        jobId: result.jobId,
        status: result.status,
        sourcePath: result.sourcePath,
        sourceFileName: result.sourceFileName,
        contentHash: result.contentHash,
        format: result.format,
        runId: result.run?.id,
        trialCount: result.trialCount,
        duplicateOfRunId: result.duplicateOfRunId,
        issues: result.issues,
        completedAt: result.completedAt,
      });
      // Keep history bounded in the index; full ledger can grow via imports.jsonl later.
      index.imports = index.imports.slice(0, 500);
      await writeIndex(index);
    },

    async listRuns() {
      const index = await readIndex();
      return index.runs;
    },

    async getRun(runId) {
      const index = await readIndex();
      const entry = index.runs.find((run) => run.id === runId);
      if (!entry) return undefined;
      const raw = await fs.readFile(path.join(entry.artifactDir, 'run.json'), 'utf8');
      return JSON.parse(raw) as OptimisationRun;
    },

    async listTrials(runId, options = {}) {
      const offset = options.offset ?? 0;
      const limit = options.limit ?? 50;
      const index = await readIndex();
      const entry = index.runs.find((run) => run.id === runId);
      if (!entry) return { trials: [], total: 0 };

      const raw = await fs.readFile(path.join(entry.artifactDir, 'trials.ndjson'), 'utf8');
      const lines = raw.split('\n').filter((line) => line.trim().length > 0);
      const slice = lines
        .slice(offset, offset + limit)
        .map((line) => JSON.parse(line) as OptimisationTrial);
      return { trials: slice, total: lines.length };
    },

    async listImportHistory() {
      const index = await readIndex();
      return index.imports;
    },

    async ping() {
      return { ok: true };
    },
  };

  // Ensure index file exists
  const existing = await readIndex();
  await writeIndex(existing);

  return api;
}
