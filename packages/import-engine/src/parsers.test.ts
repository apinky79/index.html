import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, expect, it } from 'vitest';

import {
  detectFormatFromPath,
  parseCbotset,
  parseCsv,
  parseNativeJson,
  parseOptres,
  validateParsedDraft,
} from './index.js';

const fixturesDir = path.join(path.dirname(fileURLToPath(import.meta.url)), '../fixtures');

function readFixture(name: string): string {
  return fs.readFileSync(path.join(fixturesDir, name), 'utf8');
}

describe('format detection', () => {
  it('detects supported extensions', () => {
    expect(detectFormatFromPath('a.optres')).toBe('optres');
    expect(detectFormatFromPath('a.cbotset')).toBe('cbotset');
    expect(detectFormatFromPath('a.csv')).toBe('csv');
    expect(detectFormatFromPath('a.json')).toBe('json');
    expect(detectFormatFromPath('a.txt')).toBeUndefined();
  });
});

describe('parsers', () => {
  it('parses .optres passes into trials', () => {
    const draft = parseOptres(readFixture('sample.optres'));
    expect(draft.format).toBe('optres');
    expect(draft.instrumentSymbol).toBe('EURUSD');
    expect(draft.trials).toHaveLength(3);
    expect(draft.trials[0]?.parameters.emaFast).toBe(9);
    expect(draft.trials[0]?.metrics?.fitness).toBe(1.84);
  });

  it('parses .cbotset into a single trial', () => {
    const draft = parseCbotset(readFixture('sample.cbotset'));
    expect(draft.trials).toHaveLength(1);
    expect(draft.instrumentSymbol).toBe('BTCUSD');
    expect(draft.trials[0]?.parameters.emaFast).toBe(10);
  });

  it('parses CSV rows', () => {
    const draft = parseCsv(readFixture('sample.csv'));
    expect(draft.trials).toHaveLength(3);
    expect(draft.instrumentSymbol).toBe('XAUUSD');
    expect(draft.trials[1]?.parameters.emaSlow).toBe(25);
  });

  it('parses MarketDNA native JSON', () => {
    const draft = parseNativeJson(readFixture('sample.json'));
    expect(draft.strategyId).toBe('strat_native_json_bot');
    expect(draft.trials).toHaveLength(2);
  });
});

describe('validation', () => {
  it('flags empty trial sets', () => {
    const issues = validateParsedDraft({
      format: 'json',
      schemaId: 'x',
      trials: [],
      rawMetadata: {},
    });
    expect(issues.some((i) => i.code === 'NO_TRIALS')).toBe(true);
  });
});
