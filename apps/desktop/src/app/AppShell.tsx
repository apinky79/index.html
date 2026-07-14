import { APP_INIT_MESSAGE, APP_NAME, APP_VERSION } from '@marketdna/shared';

/**
 * Blank application shell for Phase 1A.
 * No feature UI — identity + initialisation confirmation only.
 */
export function AppShell() {
  const appName = window.marketdna?.appName ?? APP_NAME;
  const appVersion = window.marketdna?.appVersion ?? APP_VERSION;
  const initMessage = window.marketdna?.initMessage ?? APP_INIT_MESSAGE;

  return (
    <main className="flex min-h-full items-center justify-center px-6">
      <section
        className="w-full max-w-lg rounded-2xl border border-white/10 px-10 py-12 text-center shadow-2xl"
        style={{ background: 'color-mix(in srgb, var(--md-panel) 92%, transparent)' }}
      >
        <p className="text-sm uppercase tracking-[0.2em] text-[var(--md-muted)]">
          Research Platform
        </p>
        <h1 className="mt-3 text-4xl font-semibold tracking-tight text-[var(--md-fg)]">
          {appName}
        </h1>
        <p className="mt-2 text-lg text-[var(--md-muted)]">Version {appVersion}</p>
        <div className="mx-auto mt-8 h-px w-24 bg-[var(--md-accent)]/60" />
        <p className="mt-8 text-base text-[var(--md-fg)]">{initMessage}</p>
      </section>
    </main>
  );
}
