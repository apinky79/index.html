import pino, { type Logger, type LoggerOptions } from 'pino';

import { loadAppConfig } from './config.js';

export type { Logger };

export interface CreateLoggerOptions {
  /** Logical component name bound onto every log line. */
  name: string;
  /** Override log level (defaults to config). */
  level?: LoggerOptions['level'];
}

/**
 * Create a structured logger for a MarketDNA component.
 */
export function createLogger(options: CreateLoggerOptions | string): Logger {
  const name = typeof options === 'string' ? options : options.name;
  const level =
    typeof options === 'string'
      ? loadAppConfig().logLevel
      : (options.level ?? loadAppConfig().logLevel);

  return pino({
    name,
    level,
    base: {
      service: 'marketdna',
    },
  });
}
