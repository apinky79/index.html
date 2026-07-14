import { describe, expect, it } from 'vitest';

import { BoundedContext, V1_INSTRUMENT_SYMBOLS } from './index.js';

describe('domain vocabulary', () => {
  it('exposes bounded contexts from the domain model', () => {
    expect(BoundedContext.MarketIntelligence).toBe('market_intelligence');
    expect(BoundedContext.Recommendations).toBe('recommendations');
    expect(BoundedContext.Optimisation).toBe('optimisation');
  });

  it('lists the six v1 instrument symbols', () => {
    expect(V1_INSTRUMENT_SYMBOLS).toHaveLength(6);
    expect(V1_INSTRUMENT_SYMBOLS).toContain('BTCUSD');
    expect(V1_INSTRUMENT_SYMBOLS).toContain('SPX500');
  });
});
