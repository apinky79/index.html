import { describe, expect, it } from 'vitest';

import { APP_INIT_MESSAGE, APP_NAME, APP_VERSION } from '@marketdna/shared';

describe('AppShell copy', () => {
  it('matches the Phase 1A shell contract', () => {
    expect(APP_NAME).toBe('MarketDNA');
    expect(APP_VERSION).toBe('0.1.0');
    expect(APP_INIT_MESSAGE).toBe('Application Initialised Successfully');
  });
});
