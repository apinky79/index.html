import type {
  DateRange,
  OptimisationRun,
  OptimisationTrial,
  TrialMetrics,
} from '@marketdna/domain';

export type ImportFormat = 'optres' | 'cbotset' | 'csv' | 'json';

export type ImportJobStatus =
  'queued' | 'validating' | 'parsing' | 'persisting' | 'completed' | 'failed' | 'duplicate';

export interface ImportIssue {
  code: string;
  message: string;
  path?: string;
  severity: 'error' | 'warning';
}

export interface ParsedTrialDraft {
  pass?: number;
  parameters: Record<string, number | string | boolean>;
  metrics?: TrialMetrics;
  constraintOk?: boolean;
}

export interface ParsedOptimisationDraft {
  format: ImportFormat;
  strategyName?: string;
  strategyId?: string;
  instrumentSymbol?: string;
  timeframeCode?: string;
  range?: Partial<DateRange>;
  schemaId: string;
  trials: ParsedTrialDraft[];
  rawMetadata: Record<string, unknown>;
}

export interface ImportProgress {
  jobId: string;
  status: ImportJobStatus;
  percent: number;
  message: string;
  processedTrials?: number;
  totalTrials?: number;
}

export interface ImportJobResult {
  jobId: string;
  status: ImportJobStatus;
  format?: ImportFormat;
  sourcePath: string;
  sourceFileName: string;
  contentHash: string;
  issues: ImportIssue[];
  run?: OptimisationRun;
  trialCount?: number;
  duplicateOfRunId?: string;
  completedAt?: string;
}

export interface MappedImport {
  run: OptimisationRun;
  trials: OptimisationTrial[];
  contentHash: string;
  format: ImportFormat;
}

export type ProgressListener = (progress: ImportProgress) => void;
