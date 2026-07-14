import { ImportWorkbench } from '../features/import/ImportWorkbench';

/**
 * Application shell — Phase 2 hosts the Import Engine workbench.
 */
export function AppShell() {
  const appName = window.marketdna?.appName ?? 'MarketDNA';
  const appVersion = window.marketdna?.appVersion ?? '0.2.0';

  return (
    <div className="min-h-full">
      <nav className="flex items-center justify-between border-b border-white/10 px-6 py-4">
        <div className="flex items-baseline gap-3">
          <span className="text-xl font-semibold tracking-tight">{appName}</span>
          <span className="text-sm text-[var(--md-muted)]">v{appVersion}</span>
        </div>
        <span className="text-sm text-[var(--md-muted)]">Import Engine</span>
      </nav>
      <ImportWorkbench />
    </div>
  );
}
