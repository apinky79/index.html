import { z } from 'zod';

import { APP_NAME, APP_VERSION } from './constants.js';
import { readEnv, type EnvSource } from './env.js';

/**
 * Application configuration schema.
 * Phase 1A: identity + logging only. Extended in later phases.
 */
export const appConfigSchema = z.object({
  appName: z.string().min(1).default(APP_NAME),
  appVersion: z.string().min(1).default(APP_VERSION),
  nodeEnv: z.enum(['development', 'test', 'production']).default('development'),
  logLevel: z.enum(['fatal', 'error', 'warn', 'info', 'debug', 'trace', 'silent']).default('info'),
});

export type AppConfig = z.infer<typeof appConfigSchema>;

/**
 * Load and validate application configuration from the environment.
 */
export function loadAppConfig(source: EnvSource = process.env): AppConfig {
  return appConfigSchema.parse({
    appName: readEnv('MARKETDNA_APP_NAME', source) ?? APP_NAME,
    appVersion: readEnv('MARKETDNA_APP_VERSION', source) ?? APP_VERSION,
    nodeEnv: readEnv('NODE_ENV', source) ?? 'development',
    logLevel: readEnv('LOG_LEVEL', source) ?? 'info',
  });
}
