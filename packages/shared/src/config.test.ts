import { describe, expect, it } from 'vitest';

import { APP_INIT_MESSAGE, APP_NAME, APP_VERSION } from './constants.js';
import { loadAppConfig } from './config.js';
import { readEnv, requireEnv } from './env.js';

describe('constants', () => {
  it('exposes MarketDNA identity', () => {
    expect(APP_NAME).toBe('MarketDNA');
    expect(APP_VERSION).toBe('0.1.0');
    expect(APP_INIT_MESSAGE).toBe('Application Initialised Successfully');
  });
});

describe('env', () => {
  it('reads optional values', () => {
    expect(readEnv('FOO', { FOO: 'bar' })).toBe('bar');
    expect(readEnv('FOO', {})).toBeUndefined();
  });

  it('requires values', () => {
    expect(requireEnv('FOO', { FOO: 'bar' })).toBe('bar');
    expect(() => requireEnv('FOO', {})).toThrow(/FOO/);
  });
});

describe('loadAppConfig', () => {
  it('applies defaults', () => {
    const config = loadAppConfig({});
    expect(config.appName).toBe('MarketDNA');
    expect(config.appVersion).toBe('0.1.0');
    expect(config.nodeEnv).toBe('development');
    expect(config.logLevel).toBe('info');
  });

  it('reads overrides from env', () => {
    const config = loadAppConfig({
      MARKETDNA_APP_NAME: 'MarketDNA',
      LOG_LEVEL: 'debug',
      NODE_ENV: 'test',
    });
    expect(config.logLevel).toBe('debug');
    expect(config.nodeEnv).toBe('test');
  });
});
