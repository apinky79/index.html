import { EventEmitter } from 'node:events';

import type { ImportJobResult, ImportProgress, ProgressListener } from '../types.js';

export interface QueuedImportRequest {
  jobId: string;
  sourcePath: string;
  sourceFileName: string;
  content: Buffer;
}

type Worker = (
  request: QueuedImportRequest,
  onProgress: ProgressListener,
) => Promise<ImportJobResult>;

/**
 * Serial import queue with progress fan-out.
 * Supports high volume over time by processing one heavy import at a time
 * while accepting unlimited enqueue operations.
 */
export class ImportQueue {
  private readonly emitter = new EventEmitter();
  private readonly pending: QueuedImportRequest[] = [];
  private active = false;
  private readonly worker: Worker;

  constructor(worker: Worker) {
    this.worker = worker;
  }

  onProgress(listener: ProgressListener): () => void {
    this.emitter.on('progress', listener);
    return () => this.emitter.off('progress', listener);
  }

  onCompleted(listener: (result: ImportJobResult) => void): () => void {
    this.emitter.on('completed', listener);
    return () => this.emitter.off('completed', listener);
  }

  size(): number {
    return this.pending.length + (this.active ? 1 : 0);
  }

  enqueue(request: QueuedImportRequest): void {
    this.pending.push(request);
    this.emitter.emit('progress', {
      jobId: request.jobId,
      status: 'queued',
      percent: 0,
      message: 'Queued for import',
    } satisfies ImportProgress);
    void this.pump();
  }

  private async pump(): Promise<void> {
    if (this.active) return;
    const next = this.pending.shift();
    if (!next) return;

    this.active = true;
    try {
      const result = await this.worker(next, (progress) => this.emitter.emit('progress', progress));
      this.emitter.emit('completed', result);
    } finally {
      this.active = false;
      void this.pump();
    }
  }
}
