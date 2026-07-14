import path from 'node:path';

import { createId } from '@marketdna/shared';

import { assertSupportedFormat } from './detect-format.js';
import { buildArtifactDir, fingerprintBuffer, mapDraftToDomain } from './map-to-domain.js';
import { parseOptimisationFile } from './parsers/index.js';
import { ImportQueue, type QueuedImportRequest } from './queue/import-queue.js';
import type { ImportJobResult, ImportProgress, MappedImport, ProgressListener } from './types.js';
import { hasBlockingErrors, validateParsedDraft } from './validation/validate.js';

export interface ImportEngineStore {
  findRunIdByFingerprint(contentHash: string): Promise<string | undefined>;
  saveImport(mapped: MappedImport, sourcePath: string, format: string): Promise<void>;
}

export interface ImportEngineOptions {
  workspaceRoot: string;
  store: ImportEngineStore;
}

/**
 * Import Engine application service — no optimisation/AI logic.
 */
export class ImportEngine {
  private readonly workspaceRoot: string;
  private readonly store: ImportEngineStore;
  private readonly queue: ImportQueue;

  constructor(options: ImportEngineOptions) {
    this.workspaceRoot = options.workspaceRoot;
    this.store = options.store;
    this.queue = new ImportQueue((request, onProgress) => this.process(request, onProgress));
  }

  onProgress(listener: ProgressListener): () => void {
    return this.queue.onProgress(listener);
  }

  onCompleted(listener: (result: ImportJobResult) => void): () => void {
    return this.queue.onCompleted(listener);
  }

  queueSize(): number {
    return this.queue.size();
  }

  /**
   * Enqueue a file for import. Returns the job id immediately.
   */
  enqueueFile(input: { sourcePath: string; content: Buffer; sourceFileName?: string }): string {
    const jobId = createId('imp');
    const sourceFileName = input.sourceFileName ?? path.basename(input.sourcePath);
    assertSupportedFormat(sourceFileName);

    this.queue.enqueue({
      jobId,
      sourcePath: input.sourcePath,
      sourceFileName,
      content: input.content,
    });

    return jobId;
  }

  /**
   * Import synchronously (tests / tooling).
   */
  async importFileSync(input: {
    sourcePath: string;
    content: Buffer;
    sourceFileName?: string;
  }): Promise<ImportJobResult> {
    const jobId = createId('imp');
    return this.process(
      {
        jobId,
        sourcePath: input.sourcePath,
        sourceFileName: input.sourceFileName ?? path.basename(input.sourcePath),
        content: input.content,
      },
      () => undefined,
    );
  }

  private async process(
    request: QueuedImportRequest,
    onProgress: ProgressListener,
  ): Promise<ImportJobResult> {
    const emit = (partial: Omit<ImportProgress, 'jobId'>) =>
      onProgress({ jobId: request.jobId, ...partial });

    try {
      const format = assertSupportedFormat(request.sourceFileName);
      const contentHash = fingerprintBuffer(request.content);

      emit({ status: 'validating', percent: 5, message: 'Checking for duplicates' });
      const existing = await this.store.findRunIdByFingerprint(contentHash);
      if (existing) {
        const result: ImportJobResult = {
          jobId: request.jobId,
          status: 'duplicate',
          format,
          sourcePath: request.sourcePath,
          sourceFileName: request.sourceFileName,
          contentHash,
          issues: [
            {
              code: 'DUPLICATE_CONTENT',
              message: `Identical file already imported as run ${existing}`,
              severity: 'warning',
            },
          ],
          duplicateOfRunId: existing,
          completedAt: new Date().toISOString(),
        };
        emit({ status: 'duplicate', percent: 100, message: 'Duplicate import skipped' });
        return result;
      }

      emit({ status: 'parsing', percent: 20, message: `Parsing ${format}` });
      const text = request.content.toString('utf8');
      const draft = parseOptimisationFile(request.sourceFileName, text);
      const issues = validateParsedDraft(draft);

      if (hasBlockingErrors(issues)) {
        const result: ImportJobResult = {
          jobId: request.jobId,
          status: 'failed',
          format,
          sourcePath: request.sourcePath,
          sourceFileName: request.sourceFileName,
          contentHash,
          issues,
          completedAt: new Date().toISOString(),
        };
        emit({ status: 'failed', percent: 100, message: 'Validation failed' });
        return result;
      }

      emit({
        status: 'parsing',
        percent: 55,
        message: `Mapped ${draft.trials.length} trials`,
        processedTrials: draft.trials.length,
        totalTrials: draft.trials.length,
      });

      const mapped = mapDraftToDomain(draft, {
        contentHash,
        sourceFileName: request.sourceFileName,
        artifactDir: buildArtifactDir(this.workspaceRoot, 'pending'),
      });
      mapped.run.artifactDir = buildArtifactDir(this.workspaceRoot, mapped.run.id);

      emit({
        status: 'persisting',
        percent: 75,
        message: 'Persisting optimisation run',
        totalTrials: mapped.trials.length,
      });

      await this.store.saveImport(mapped, request.sourcePath, format);

      const result: ImportJobResult = {
        jobId: request.jobId,
        status: 'completed',
        format,
        sourcePath: request.sourcePath,
        sourceFileName: request.sourceFileName,
        contentHash,
        issues,
        run: mapped.run,
        trialCount: mapped.trials.length,
        completedAt: new Date().toISOString(),
      };

      emit({
        status: 'completed',
        percent: 100,
        message: 'Import completed',
        processedTrials: mapped.trials.length,
        totalTrials: mapped.trials.length,
      });

      return result;
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      const result: ImportJobResult = {
        jobId: request.jobId,
        status: 'failed',
        sourcePath: request.sourcePath,
        sourceFileName: request.sourceFileName,
        contentHash: fingerprintBuffer(request.content),
        issues: [{ code: 'IMPORT_EXCEPTION', message, severity: 'error' }],
        completedAt: new Date().toISOString(),
      };
      emit({ status: 'failed', percent: 100, message });
      return result;
    }
  }
}
