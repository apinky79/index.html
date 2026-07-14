import { describe, expect, it } from 'vitest';

import { createAnalyticsClient } from './index.js';

describe('analytics client (Phase 1A stub)', () => {
  it('reports workers not ready', async () => {
    const client = createAnalyticsClient();
    const health = await client.health();
    expect(health.ok).toBe(false);
    expect(health.engine.ready).toBe(false);
    expect(health.engine.protocol).toBe('stub');
    expect(health.message).toMatch(/Phase 1A/);
  });
});
