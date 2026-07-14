export interface ImportProgressEvent {
  jobId: string;
  status: string;
  percent: number;
  message: string;
  processedTrials?: number;
  totalTrials?: number;
}

export interface ImportCompletedEvent {
  jobId: string;
  status: string;
  sourceFileName: string;
  format?: string;
  trialCount?: number;
  run?: { id: string; instrumentSymbol?: string; strategyName?: string };
  issues: Array<{ code: string; message: string; severity: string }>;
  duplicateOfRunId?: string;
}

export interface ImportHistoryRow {
  jobId: string;
  status: string;
  sourceFileName: string;
  format?: string;
  trialCount?: number;
  runId?: string;
  completedAt?: string;
  issues: Array<{ code: string; message: string; severity: string }>;
}

export interface RunSummary {
  id: string;
  strategyName?: string;
  instrumentSymbol?: string;
  timeframeCode?: string;
  trialCount: number;
  sourceFileName?: string;
  importedAt?: string;
  format?: string;
}

export interface RunDetail {
  id: string;
  strategyName?: string;
  instrumentSymbol?: string;
  timeframeCode?: string;
  trialCount?: number;
  sourceFileName?: string;
  importedAt?: string;
  dataFingerprint: string;
  status: string;
}

export interface TrialRow {
  id: string;
  pass?: number;
  parameters: { values: Record<string, number | string | boolean> };
  metrics?: Record<string, number | undefined>;
  constraintOk: boolean;
}
