import type {
  ImportCompletedEvent,
  ImportHistoryRow,
  ImportProgressEvent,
  RunDetail,
  RunSummary,
  TrialRow,
} from './features/import/types';

interface MarketDnaBridge {
  appName: string;
  appVersion: string;
  getIdentity: () => Promise<{
    appName: string;
    appVersion: string;
    workspacePath: string;
  }>;
  chooseImportFiles: () => Promise<string[]>;
  enqueueImportPaths: (paths: string[]) => Promise<{ jobIds: string[]; queueSize: number }>;
  enqueueImportBuffer: (payload: {
    fileName: string;
    data: ArrayBuffer;
    sourcePath?: string;
  }) => Promise<{ jobId: string; queueSize: number }>;
  listImportHistory: () => Promise<ImportHistoryRow[]>;
  listRuns: () => Promise<RunSummary[]>;
  getRun: (runId: string) => Promise<RunDetail | null>;
  listTrials: (payload: {
    runId: string;
    offset?: number;
    limit?: number;
  }) => Promise<{ trials: TrialRow[]; total: number }>;
  onImportProgress: (listener: (progress: ImportProgressEvent) => void) => () => void;
  onImportCompleted: (listener: (result: ImportCompletedEvent) => void) => () => void;
}

declare global {
  interface Window {
    marketdna?: MarketDnaBridge;
  }
}

export {};
