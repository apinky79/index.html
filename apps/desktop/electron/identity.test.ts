import { describe, expect, it } from 'vitest';

import { APP_NAME, APP_VERSION } from '@marketdna/shared';

describe('desktop identity', () => {
  it('uses MarketDNA 0.1.0', () => {
    expect(APP_NAME).toBe('MarketDNA');
    expect(APP_VERSION).toBe('0.1.0');
  });
});
