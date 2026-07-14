import { app, BrowserWindow, dialog, ipcMain } from 'electron';
import fs from 'node:fs/promises';
import path from 'node:path';

import { ImportEngine, type ImportJobResult, type ImportProgress } from '@marketdna/import-engine';
import { openWorkspaceDatabase, type WorkspaceDatabase } from '@marketdna/database';

const APP_NAME = 'MarketDNA';
const APP_VERSION = '0.2.0';

let mainWindow: BrowserWindow | null = null;
let workspaceDb: WorkspaceDatabase | null = null;
let importEngine: ImportEngine | null = null;

function workspaceRoot(): string {
  return path.join(app.getPath('userData'), 'workspaces', 'default');
}

async function ensureServices(): Promise<{ db: WorkspaceDatabase; engine: ImportEngine }> {
  if (!workspaceDb) {
    workspaceDb = await openWorkspaceDatabase(workspaceRoot());
  }
  if (!importEngine) {
    importEngine = new ImportEngine({
      workspaceRoot: workspaceRoot(),
      store: workspaceDb.asImportStore(),
    });

    importEngine.onProgress((progress) => {
      mainWindow?.webContents.send('import:progress', progress);
    });

    importEngine.onCompleted((result) => {
      void workspaceDb?.recordImportResult(result).then(() => {
        mainWindow?.webContents.send('import:completed', result);
      });
    });
  }
  return { db: workspaceDb, engine: importEngine };
}

function createMainWindow(): BrowserWindow {
  const win = new BrowserWindow({
    width: 1280,
    height: 840,
    title: APP_NAME,
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });

  win.once('ready-to-show', () => {
    win.show();
  });

  const devServerUrl = process.env.VITE_DEV_SERVER_URL;
  if (devServerUrl) {
    void win.loadURL(devServerUrl);
  } else {
    void win.loadFile(path.join(__dirname, '../dist/index.html'));
  }

  return win;
}

function registerIpc(): void {
  ipcMain.handle('app:getIdentity', () => ({
    appName: APP_NAME,
    appVersion: APP_VERSION,
    workspacePath: workspaceRoot(),
  }));

  ipcMain.handle('import:chooseFiles', async () => {
    const result = await dialog.showOpenDialog({
      title: 'Import optimisation files',
      properties: ['openFile', 'multiSelections'],
      filters: [
        {
          name: 'Optimisation imports',
          extensions: ['optres', 'cbotset', 'csv', 'json'],
        },
      ],
    });
    if (result.canceled) return [];
    return result.filePaths;
  });

  ipcMain.handle('import:enqueuePaths', async (_event, filePaths: string[]) => {
    const { engine } = await ensureServices();
    const jobIds: string[] = [];
    for (const filePath of filePaths) {
      const content = await fs.readFile(filePath);
      const jobId = engine.enqueueFile({ sourcePath: filePath, content });
      jobIds.push(jobId);
    }
    return { jobIds, queueSize: engine.queueSize() };
  });

  ipcMain.handle(
    'import:enqueueBuffer',
    async (_event, payload: { fileName: string; data: ArrayBuffer; sourcePath?: string }) => {
      const { engine } = await ensureServices();
      const content = Buffer.from(payload.data);
      const sourcePath = payload.sourcePath ?? payload.fileName;
      const jobId = engine.enqueueFile({
        sourcePath,
        sourceFileName: payload.fileName,
        content,
      });
      return { jobId, queueSize: engine.queueSize() };
    },
  );

  ipcMain.handle('import:listHistory', async () => {
    const { db } = await ensureServices();
    return db.listImportHistory();
  });

  ipcMain.handle('import:listRuns', async () => {
    const { db } = await ensureServices();
    return db.listRuns();
  });

  ipcMain.handle('import:getRun', async (_event, runId: string) => {
    const { db } = await ensureServices();
    return db.getRun(runId);
  });

  ipcMain.handle(
    'import:listTrials',
    async (_event, payload: { runId: string; offset?: number; limit?: number }) => {
      const { db } = await ensureServices();
      return db.listTrials(payload.runId, {
        offset: payload.offset,
        limit: payload.limit,
      });
    },
  );
}

app.whenReady().then(async () => {
  await ensureServices();
  registerIpc();
  console.info(`[${APP_NAME}] main process ready (v${APP_VERSION})`);
  mainWindow = createMainWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      mainWindow = createMainWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

export type { ImportJobResult, ImportProgress };
