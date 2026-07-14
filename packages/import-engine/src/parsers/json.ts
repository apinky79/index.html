import type { TrialMetrics } from '@marketdna/domain';

import type { ParsedOptimisationDraft, ParsedTrialDraft } from '../types.js';

function asRecord(value: unknown): Record<string, unknown> | undefined {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : undefined;
}

function asArray(value: unknown): unknown[] {
  return Array.isArray(value) ? value : [];
}

function coerceParamValue(value: unknown): number | string | boolean {
  if (typeof value === 'boolean' || typeof value === 'number') return value;
  if (typeof value === 'string') {
    const lower = value.toLowerCase();
    if (lower === 'true') return true;
    if (lower === 'false') return false;
    const num = Number(value);
    if (value.trim() !== '' && Number.isFinite(num)) return num;
    return value;
  }
  return String(value);
}

export function extractParameters(raw: unknown): Record<string, number | string | boolean> {
  const record = asRecord(raw) ?? {};
  const out: Record<string, number | string | boolean> = {};
  for (const [key, value] of Object.entries(record)) {
    if (value === null || value === undefined) continue;
    if (typeof value === 'object') continue;
    out[key] = coerceParamValue(value);
  }
  return out;
}

const METRIC_ALIASES: Record<string, keyof TrialMetrics> = {
  fitness: 'fitness',
  Fitness: 'fitness',
  netprofit: 'netProfit',
  NetProfit: 'netProfit',
  'net profit': 'netProfit',
  equity: 'equity',
  Equity: 'equity',
  balance: 'balance',
  Balance: 'balance',
  trades: 'trades',
  Trades: 'trades',
  winningtrades: 'winningTrades',
  WinningTrades: 'winningTrades',
  losingtrades: 'losingTrades',
  LosingTrades: 'losingTrades',
  profitfactor: 'profitFactor',
  ProfitFactor: 'profitFactor',
  maxequitydrawdownpct: 'maxEquityDrawdownPct',
  MaxEquityDrawdownPercent: 'maxEquityDrawdownPct',
  maxbalancedrawdownpct: 'maxBalanceDrawdownPct',
  MaxBalanceDrawdownPercent: 'maxBalanceDrawdownPct',
  maxequitydrawdown: 'maxEquityDrawdown',
  MaxEquityDrawdown: 'maxEquityDrawdown',
  maxbalancedrawdown: 'maxBalanceDrawdown',
  MaxBalanceDrawdown: 'maxBalanceDrawdown',
  averagetrade: 'averageTrade',
  AverageTrade: 'averageTrade',
};

export function extractMetrics(raw: Record<string, unknown>): TrialMetrics {
  const metrics: TrialMetrics = {};
  for (const [key, value] of Object.entries(raw)) {
    if (typeof value !== 'number' && typeof value !== 'string') continue;
    const num = typeof value === 'number' ? value : Number(value);
    if (!Number.isFinite(num)) continue;
    const normalised = METRIC_ALIASES[key] ?? METRIC_ALIASES[key.replace(/\s+/g, '')];
    if (normalised) {
      metrics[normalised] = num;
    } else if (!key.toLowerCase().includes('param')) {
      metrics[key] = num;
    }
  }
  return metrics;
}

export function parseNativeJson(text: string): ParsedOptimisationDraft {
  const parsed = JSON.parse(text) as unknown;
  const root = asRecord(parsed);
  if (!root) {
    throw new Error('JSON root must be an object');
  }

  if (root.format === 'marketdna.optimisation' || root.schemaVersion) {
    return parseMarketDnaDocument(root);
  }

  // Generic JSON optimisation dump with trials/passes arrays
  const trialsRaw =
    asArray(root.trials).length > 0
      ? asArray(root.trials)
      : asArray(root.passes).length > 0
        ? asArray(root.passes)
        : asArray(root.Passes);

  if (trialsRaw.length === 0) {
    throw new Error('JSON document has no trials/passes array');
  }

  const trials = trialsRaw.map((item, index) => mapUnknownTrial(item, index));
  const chart = asRecord(root.Chart) ?? asRecord(root.chart);
  const symbol =
    (typeof root.instrumentSymbol === 'string' && root.instrumentSymbol) ||
    (typeof root.Symbol === 'string' && root.Symbol) ||
    (typeof chart?.Symbol === 'string' && chart.Symbol) ||
    undefined;

  return {
    format: 'json',
    strategyName:
      (typeof root.strategyName === 'string' && root.strategyName) ||
      (typeof root.RobotName === 'string' && root.RobotName) ||
      undefined,
    instrumentSymbol: symbol,
    timeframeCode:
      (typeof root.timeframe === 'string' && root.timeframe) ||
      (typeof root.Period === 'string' && root.Period) ||
      (typeof chart?.Period === 'string' && chart.Period) ||
      undefined,
    range: {
      start:
        typeof root.start === 'string'
          ? root.start
          : typeof root.StartDate === 'string'
            ? new Date(root.StartDate).toISOString()
            : undefined,
      end:
        typeof root.end === 'string'
          ? root.end
          : typeof root.EndDate === 'string'
            ? new Date(root.EndDate).toISOString()
            : undefined,
      inclusivity: 'startClosedEndOpen',
    },
    schemaId:
      (typeof root.schemaId === 'string' && root.schemaId) ||
      `schema_${(typeof root.RobotName === 'string' && root.RobotName) || 'json'}`,
    trials,
    rawMetadata: root,
  };
}

function parseMarketDnaDocument(root: Record<string, unknown>): ParsedOptimisationDraft {
  const trialsRaw = asArray(root.trials);
  const trials = trialsRaw.map((item, index) => mapUnknownTrial(item, index));
  return {
    format: 'json',
    strategyName: typeof root.strategyName === 'string' ? root.strategyName : undefined,
    strategyId: typeof root.strategyId === 'string' ? root.strategyId : undefined,
    instrumentSymbol: typeof root.instrumentSymbol === 'string' ? root.instrumentSymbol : undefined,
    timeframeCode: typeof root.timeframe === 'string' ? root.timeframe : undefined,
    range: {
      start:
        typeof asRecord(root.range)?.start === 'string'
          ? (asRecord(root.range)!.start as string)
          : undefined,
      end:
        typeof asRecord(root.range)?.end === 'string'
          ? (asRecord(root.range)!.end as string)
          : undefined,
      inclusivity: 'startClosedEndOpen',
    },
    schemaId: typeof root.schemaId === 'string' ? root.schemaId : 'marketdna.optimisation.params',
    trials,
    rawMetadata: root,
  };
}

export function mapUnknownTrial(item: unknown, index: number): ParsedTrialDraft {
  const record = asRecord(item) ?? {};
  const parameters =
    extractParameters(record.parameters ?? record.Parameters ?? record.params) || {};

  // Flatten param_* keys sitting on the trial object
  for (const [key, value] of Object.entries(record)) {
    if (key.toLowerCase().startsWith('param_') || key.toLowerCase().startsWith('parameter')) {
      const name = key.replace(/^param(eter)?_/i, '');
      parameters[name] = coerceParamValue(value);
    }
  }

  // If still empty, treat non-metric scalar fields as parameters
  if (Object.keys(parameters).length === 0) {
    for (const [key, value] of Object.entries(record)) {
      if (['pass', 'Pass', 'metrics', 'fitness', 'Fitness'].includes(key)) continue;
      if (METRIC_ALIASES[key] || METRIC_ALIASES[key.replace(/\s+/g, '')]) continue;
      if (typeof value === 'object') continue;
      parameters[key] = coerceParamValue(value);
    }
  }

  const passValue = record.pass ?? record.Pass;
  const pass =
    typeof passValue === 'number'
      ? passValue
      : typeof passValue === 'string' && Number.isFinite(Number(passValue))
        ? Number(passValue)
        : index + 1;

  return {
    pass,
    parameters,
    metrics: extractMetrics(record),
    constraintOk: record.constraintOk === false ? false : true,
  };
}
