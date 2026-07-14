import { contextBridge, ipcRenderer } from 'electron';

export interface MarketDnaBridge {
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
  listImportHistory: () => Promise<unknown[]>;
  listRuns: () => Promise<unknown[]>;
  getRun: (runId: string) => Promise<unknown>;
  listTrials: (payload: {
    runId: string;
    offset?: number;
    limit?: number;
  }) => Promise<{ trials: unknown[]; total: number }>;
  onImportProgress: (listener: (progress: unknown) => void) => () => void;
  onImportCompleted: (listener: (result: unknown) => void) => () => void;
}

const bridge: MarketDnaBridge = {
  appName: 'MarketDNA',
  appVersion: '0.2.0',
  getIdentity: () => ipcRenderer.invoke('app:getIdentity'),
  chooseImportFiles: () => ipcRenderer.invoke('import:chooseFiles'),
  enqueueImportPaths: (paths) => ipcRenderer.invoke('import:enqueuePaths', paths),
  enqueueImportBuffer: (payload) => ipcRenderer.invoke('import:enqueueBuffer', payload),
  listImportHistory: () => ipcRenderer.invoke('import:listHistory'),
  listRuns: () => ipcRenderer.invoke('import:listRuns'),
  getRun: (runId) => ipcRenderer.invoke('import:getRun', runId),
  listTrials: (payload) => ipcRenderer.invoke('import:listTrials', payload),
  onImportProgress: (listener) => {
    const handler = (_event: Electron.IpcRendererEvent, progress: unknown) => listener(progress);
    ipcRenderer.on('import:progress', handler);
    return () => ipcRenderer.removeListener('import:progress', handler);
  },
  onImportCompleted: (listener) => {
    const handler = (_event: Electron.IpcRendererEvent, result: unknown) => listener(result);
    ipcRenderer.on('import:completed', handler);
    return () => ipcRenderer.removeListener('import:completed', handler);
  },
};

contextBridge.exposeInMainWorld('marketdna', bridge);
