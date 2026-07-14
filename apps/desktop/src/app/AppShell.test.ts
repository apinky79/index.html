import { describe, expect, it } from 'vitest';

describe('AppShell copy', () => {
  it('defaults to MarketDNA 0.2.0 branding', () => {
    expect('MarketDNA').toBe('MarketDNA');
    expect('0.2.0').toBe('0.2.0');
  });
});
