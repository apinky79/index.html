import { describe, expect, it } from 'vitest';

import { sha256Hex, createId } from './ids.js';

describe('ids helpers', () => {
  it('hashes content stably', () => {
    expect(sha256Hex('abc')).toBe(sha256Hex('abc'));
    expect(sha256Hex('abc')).not.toBe(sha256Hex('abcd'));
  });

  it('creates prefixed ids', () => {
    expect(createId('opt').startsWith('opt_')).toBe(true);
  });
});
