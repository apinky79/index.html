import type { ParsedOptimisationDraft, ParsedTrialDraft } from '../types.js';
import { extractMetrics } from './json.js';

/**
 * Parse CSV optimisation exports.
 *
 * Expected header conventions:
 * - parameter columns: `param_<name>` or exact parameter names listed after metrics
 * - metric columns: fitness, net_profit, trades, profit_factor, max_drawdown, ...
 * - optional: pass, symbol, timeframe, strategy
 */
export function parseCsv(text: string): ParsedOptimisationDraft {
  const rows = parseCsvRows(text);
  if (rows.length < 2) {
    throw new Error('CSV must include a header row and at least one data row');
  }

  const header = rows[0]!.map((cell) => cell.trim());
  const dataRows = rows.slice(1).filter((row) => row.some((cell) => cell.trim() !== ''));

  const trials: ParsedTrialDraft[] = dataRows.map((row, index) => {
    const record: Record<string, string> = {};
    header.forEach((col, i) => {
      record[col] = row[i] ?? '';
    });

    const parameters: Record<string, number | string | boolean> = {};
    for (const [key, value] of Object.entries(record)) {
      const lower = key.toLowerCase();
      if (lower.startsWith('param_')) {
        parameters[key.slice(6)] = coerce(value);
      }
    }

    // If no param_ columns, treat non-reserved columns as parameters
    if (Object.keys(parameters).length === 0) {
      for (const [key, value] of Object.entries(record)) {
        const lower = key.toLowerCase();
        if (
          [
            'pass',
            'fitness',
            'net_profit',
            'netprofit',
            'trades',
            'profit_factor',
            'profitfactor',
            'max_drawdown',
            'maxdrawdown',
            'symbol',
            'timeframe',
            'strategy',
            'strategy_name',
          ].includes(lower)
        ) {
          continue;
        }
        if (value.trim() === '') continue;
        parameters[key] = coerce(value);
      }
    }

    const metrics = extractMetrics(
      Object.fromEntries(Object.entries(record).map(([k, v]) => [k, coerce(v)])),
    );

    return {
      pass: record.pass ? Number(record.pass) : index + 1,
      parameters,
      metrics,
      constraintOk: true,
    };
  });

  const first = dataRows[0] ?? [];
  const record: Record<string, string> = {};
  header.forEach((col, i) => {
    record[col] = first[i] ?? '';
  });

  return {
    format: 'csv',
    strategyName: record.strategy || record.strategy_name || undefined,
    instrumentSymbol: record.symbol || undefined,
    timeframeCode: record.timeframe || record.period || undefined,
    schemaId: 'csv.parameters',
    trials,
    rawMetadata: { header, rowCount: dataRows.length },
  };
}

function coerce(value: string): number | string | boolean {
  const trimmed = value.trim();
  if (trimmed.toLowerCase() === 'true') return true;
  if (trimmed.toLowerCase() === 'false') return false;
  if (trimmed !== '' && Number.isFinite(Number(trimmed))) return Number(trimmed);
  return trimmed;
}

/** RFC4180-ish CSV parser supporting quotes and commas. */
export function parseCsvRows(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = '';
  let inQuotes = false;

  for (let i = 0; i < text.length; i += 1) {
    const char = text[i]!;
    const next = text[i + 1];

    if (inQuotes) {
      if (char === '"' && next === '"') {
        cell += '"';
        i += 1;
      } else if (char === '"') {
        inQuotes = false;
      } else {
        cell += char;
      }
      continue;
    }

    if (char === '"') {
      inQuotes = true;
    } else if (char === ',') {
      row.push(cell);
      cell = '';
    } else if (char === '\n') {
      row.push(cell);
      rows.push(row);
      row = [];
      cell = '';
    } else if (char === '\r') {
      // ignore
    } else {
      cell += char;
    }
  }

  if (cell.length > 0 || row.length > 0) {
    row.push(cell);
    rows.push(row);
  }

  return rows;
}
