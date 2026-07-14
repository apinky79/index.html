import { useCallback, useEffect, useMemo, useState } from 'react';

import type {
  ImportCompletedEvent,
  ImportHistoryRow,
  ImportProgressEvent,
  RunDetail,
  RunSummary,
  TrialRow,
} from './types';

const PAGE_SIZE = 25;

export function ImportWorkbench() {
  const [dragging, setDragging] = useState(false);
  const [queueSize, setQueueSize] = useState(0);
  const [progress, setProgress] = useState<ImportProgressEvent[]>([]);
  const [history, setHistory] = useState<ImportHistoryRow[]>([]);
  const [runs, setRuns] = useState<RunSummary[]>([]);
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  const [runDetail, setRunDetail] = useState<RunDetail | null>(null);
  const [trials, setTrials] = useState<TrialRow[]>([]);
  const [trialTotal, setTrialTotal] = useState(0);
  const [trialOffset, setTrialOffset] = useState(0);
  const [errorBanner, setErrorBanner] = useState<string | null>(null);
  const [workspacePath, setWorkspacePath] = useState<string>('');

  const api = window.marketdna;

  const refresh = useCallback(async () => {
    if (!api) return;
    const [nextHistory, nextRuns, identity] = await Promise.all([
      api.listImportHistory(),
      api.listRuns(),
      api.getIdentity(),
    ]);
    setHistory(nextHistory);
    setRuns(nextRuns);
    setWorkspacePath(identity.workspacePath);
  }, [api]);

  const loadRun = useCallback(
    async (runId: string, offset = 0) => {
      if (!api) return;
      setSelectedRunId(runId);
      setTrialOffset(offset);
      const [detail, page] = await Promise.all([
        api.getRun(runId),
        api.listTrials({ runId, offset, limit: PAGE_SIZE }),
      ]);
      setRunDetail(detail);
      setTrials(page.trials);
      setTrialTotal(page.total);
    },
    [api],
  );

  useEffect(() => {
    void refresh();
    if (!api) return;

    const offProgress = api.onImportProgress((event) => {
      setProgress((prev) => {
        const next = prev.filter((row) => row.jobId !== event.jobId);
        next.unshift(event);
        return next.slice(0, 12);
      });
    });

    const offCompleted = api.onImportCompleted((event: ImportCompletedEvent) => {
      setQueueSize((size) => Math.max(0, size - 1));
      if (event.status === 'failed') {
        setErrorBanner(event.issues.map((i) => i.message).join('; ') || 'Import failed');
      } else if (event.status === 'duplicate') {
        setErrorBanner(`Duplicate skipped: ${event.sourceFileName}`);
      } else {
        setErrorBanner(null);
      }
      void refresh().then(() => {
        if (event.run?.id) {
          void loadRun(event.run.id);
        }
      });
    });

    return () => {
      offProgress();
      offCompleted();
    };
  }, [api, loadRun, refresh]);

  const enqueueFiles = useCallback(
    async (files: FileList | File[]) => {
      if (!api) {
        setErrorBanner('MarketDNA bridge unavailable — open via Electron desktop app.');
        return;
      }
      const list = Array.from(files);
      for (const file of list) {
        const buffer = await file.arrayBuffer();
        const result = await api.enqueueImportBuffer({
          fileName: file.name,
          data: buffer,
          sourcePath: file.name,
        });
        setQueueSize(result.queueSize);
      }
    },
    [api],
  );

  const onBrowse = useCallback(async () => {
    if (!api) return;
    const paths = await api.chooseImportFiles();
    if (!paths.length) return;
    const result = await api.enqueueImportPaths(paths);
    setQueueSize(result.queueSize);
  }, [api]);

  const selectedRun = useMemo(
    () => runs.find((run) => run.id === selectedRunId) ?? null,
    [runs, selectedRunId],
  );

  return (
    <div className="mx-auto flex w-full max-w-7xl flex-col gap-6 px-6 py-8">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs uppercase tracking-[0.18em] text-[var(--md-muted)]">Phase 2</p>
          <h1 className="mt-1 text-3xl font-semibold">Import Engine</h1>
          <p className="mt-2 max-w-2xl text-sm text-[var(--md-muted)]">
            Import `.optres`, `.cbotset`, CSV, and JSON optimisation files into domain{' '}
            <code className="text-[var(--md-fg)]">OptimisationRun</code> /{' '}
            <code className="text-[var(--md-fg)]">OptimisationTrial</code> objects. No optimisation
            or AI logic — corpus ingestion only.
          </p>
        </div>
        <div className="rounded-xl border border-white/10 px-4 py-3 text-sm text-[var(--md-muted)]">
          Queue: <span className="text-[var(--md-fg)]">{queueSize}</span>
          {workspacePath ? (
            <div className="mt-1 max-w-xs truncate text-xs" title={workspacePath}>
              Workspace: {workspacePath}
            </div>
          ) : null}
        </div>
      </header>

      {errorBanner ? (
        <div className="rounded-xl border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm text-amber-100">
          {errorBanner}
        </div>
      ) : null}

      <section
        className={`rounded-2xl border border-dashed px-6 py-10 text-center transition ${
          dragging
            ? 'border-[var(--md-accent)] bg-[var(--md-accent)]/10'
            : 'border-white/20 bg-[var(--md-panel)]/70'
        }`}
        onDragEnter={(event) => {
          event.preventDefault();
          setDragging(true);
        }}
        onDragOver={(event) => event.preventDefault()}
        onDragLeave={() => setDragging(false)}
        onDrop={(event) => {
          event.preventDefault();
          setDragging(false);
          if (event.dataTransfer.files?.length) {
            void enqueueFiles(event.dataTransfer.files);
          }
        }}
      >
        <p className="text-lg font-medium">Drag and drop optimisation files</p>
        <p className="mt-2 text-sm text-[var(--md-muted)]">
          Supported: .optres · .cbotset · .csv · .json
        </p>
        <button
          type="button"
          className="mt-6 rounded-lg bg-[var(--md-accent)] px-5 py-2.5 text-sm font-medium text-white"
          onClick={() => void onBrowse()}
        >
          Browse files
        </button>
      </section>

      <div className="grid gap-6 lg:grid-cols-2">
        <section className="rounded-2xl border border-white/10 bg-[var(--md-panel)]/80 p-5">
          <h2 className="text-lg font-semibold">Progress</h2>
          <ul className="mt-4 space-y-3 text-sm">
            {progress.length === 0 ? (
              <li className="text-[var(--md-muted)]">No active imports</li>
            ) : (
              progress.map((item) => (
                <li key={item.jobId} className="rounded-lg border border-white/5 px-3 py-2">
                  <div className="flex items-center justify-between gap-3">
                    <span>{item.message}</span>
                    <span className="text-[var(--md-muted)]">{item.percent}%</span>
                  </div>
                  <div className="mt-2 h-1.5 overflow-hidden rounded bg-white/10">
                    <div
                      className="h-full bg-[var(--md-accent)]"
                      style={{ width: `${item.percent}%` }}
                    />
                  </div>
                </li>
              ))
            )}
          </ul>
        </section>

        <section className="rounded-2xl border border-white/10 bg-[var(--md-panel)]/80 p-5">
          <h2 className="text-lg font-semibold">Import history</h2>
          <ul className="mt-4 max-h-64 space-y-2 overflow-auto text-sm">
            {history.length === 0 ? (
              <li className="text-[var(--md-muted)]">No imports yet</li>
            ) : (
              history.map((row) => (
                <li
                  key={row.jobId}
                  className="flex items-center justify-between gap-3 rounded-lg border border-white/5 px-3 py-2"
                >
                  <div>
                    <div className="font-medium">{row.sourceFileName}</div>
                    <div className="text-xs text-[var(--md-muted)]">
                      {row.format ?? 'unknown'} · {row.status}
                      {row.trialCount != null ? ` · ${row.trialCount} trials` : ''}
                    </div>
                    {row.issues?.length ? (
                      <div className="mt-1 text-xs text-amber-200/90">
                        {row.issues.map((issue) => issue.message).join(' · ')}
                      </div>
                    ) : null}
                  </div>
                  {row.runId ? (
                    <button
                      type="button"
                      className="text-[var(--md-accent)]"
                      onClick={() => void loadRun(row.runId!)}
                    >
                      Open
                    </button>
                  ) : null}
                </li>
              ))
            )}
          </ul>
        </section>
      </div>

      <section className="rounded-2xl border border-white/10 bg-[var(--md-panel)]/80 p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-lg font-semibold">Imported runs</h2>
          <span className="text-sm text-[var(--md-muted)]">{runs.length} stored</span>
        </div>
        <div className="mt-4 overflow-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="text-[var(--md-muted)]">
              <tr>
                <th className="px-2 py-2 font-medium">Strategy</th>
                <th className="px-2 py-2 font-medium">Instrument</th>
                <th className="px-2 py-2 font-medium">TF</th>
                <th className="px-2 py-2 font-medium">Trials</th>
                <th className="px-2 py-2 font-medium">Format</th>
                <th className="px-2 py-2 font-medium" />
              </tr>
            </thead>
            <tbody>
              {runs.map((run) => (
                <tr key={run.id} className="border-t border-white/5">
                  <td className="px-2 py-2">{run.strategyName ?? run.id}</td>
                  <td className="px-2 py-2">{run.instrumentSymbol ?? '—'}</td>
                  <td className="px-2 py-2">{run.timeframeCode ?? '—'}</td>
                  <td className="px-2 py-2">{run.trialCount}</td>
                  <td className="px-2 py-2">{run.format ?? '—'}</td>
                  <td className="px-2 py-2 text-right">
                    <button
                      type="button"
                      className="text-[var(--md-accent)]"
                      onClick={() => void loadRun(run.id)}
                    >
                      Browse
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      {selectedRun || runDetail ? (
        <section className="rounded-2xl border border-white/10 bg-[var(--md-panel)]/80 p-5">
          <h2 className="text-lg font-semibold">
            Run detail — {runDetail?.strategyName ?? selectedRun?.strategyName ?? selectedRunId}
          </h2>
          <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <dt className="text-[var(--md-muted)]">Instrument</dt>
              <dd>{runDetail?.instrumentSymbol ?? '—'}</dd>
            </div>
            <div>
              <dt className="text-[var(--md-muted)]">Timeframe</dt>
              <dd>{runDetail?.timeframeCode ?? '—'}</dd>
            </div>
            <div>
              <dt className="text-[var(--md-muted)]">Source file</dt>
              <dd>{runDetail?.sourceFileName ?? '—'}</dd>
            </div>
            <div>
              <dt className="text-[var(--md-muted)]">Fingerprint</dt>
              <dd className="truncate" title={runDetail?.dataFingerprint}>
                {runDetail?.dataFingerprint?.slice(0, 16) ?? '—'}…
              </dd>
            </div>
          </dl>

          <div className="mt-6 flex items-center justify-between">
            <h3 className="font-medium">Trials</h3>
            <div className="flex items-center gap-2 text-sm">
              <button
                type="button"
                className="rounded border border-white/15 px-3 py-1 disabled:opacity-40"
                disabled={trialOffset <= 0}
                onClick={() =>
                  selectedRunId && void loadRun(selectedRunId, Math.max(0, trialOffset - PAGE_SIZE))
                }
              >
                Prev
              </button>
              <span className="text-[var(--md-muted)]">
                {trialOffset + 1}–{Math.min(trialOffset + PAGE_SIZE, trialTotal)} of {trialTotal}
              </span>
              <button
                type="button"
                className="rounded border border-white/15 px-3 py-1 disabled:opacity-40"
                disabled={trialOffset + PAGE_SIZE >= trialTotal}
                onClick={() =>
                  selectedRunId && void loadRun(selectedRunId, trialOffset + PAGE_SIZE)
                }
              >
                Next
              </button>
            </div>
          </div>

          <div className="mt-3 overflow-auto">
            <table className="min-w-full text-left text-sm">
              <thead className="text-[var(--md-muted)]">
                <tr>
                  <th className="px-2 py-2 font-medium">Pass</th>
                  <th className="px-2 py-2 font-medium">Parameters</th>
                  <th className="px-2 py-2 font-medium">Fitness</th>
                  <th className="px-2 py-2 font-medium">PF</th>
                  <th className="px-2 py-2 font-medium">Trades</th>
                </tr>
              </thead>
              <tbody>
                {trials.map((trial) => (
                  <tr key={trial.id} className="border-t border-white/5 align-top">
                    <td className="px-2 py-2">{trial.pass ?? '—'}</td>
                    <td className="px-2 py-2 font-mono text-xs">
                      {Object.entries(trial.parameters.values)
                        .map(([key, value]) => `${key}=${String(value)}`)
                        .join(', ')}
                    </td>
                    <td className="px-2 py-2">{trial.metrics?.fitness ?? '—'}</td>
                    <td className="px-2 py-2">{trial.metrics?.profitFactor ?? '—'}</td>
                    <td className="px-2 py-2">{trial.metrics?.trades ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}
    </div>
  );
}
