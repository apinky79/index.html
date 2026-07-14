/**
 * Analytics worker identity returned by health checks.
 */
export interface AnalyticsEngineInfo {
  name: string;
  version: string;
  protocol: 'grpc' | 'stub';
  ready: boolean;
}

export interface AnalyticsHealth {
  ok: boolean;
  engine: AnalyticsEngineInfo;
  message: string;
}

/**
 * Port toward the quantitative analytics process pool.
 * Phase 1A exposes health only — no research RPCs.
 */
export interface AnalyticsClient {
  health(): Promise<AnalyticsHealth>;
}

export interface AnalyticsClientOptions {
  /** Future: localhost gRPC target. Ignored by Phase 1A stub. */
  target?: string;
}

/**
 * Create a stub analytics client. Always reports not-ready until workers exist.
 */
export function createAnalyticsClient(_options: AnalyticsClientOptions = {}): AnalyticsClient {
  return {
    async health() {
      return {
        ok: false,
        engine: {
          name: 'marketdna-analytics',
          version: '0.0.0-stub',
          protocol: 'stub',
          ready: false,
        },
        message: 'Analytics workers are not available in Phase 1A',
      };
    },
  };
}
