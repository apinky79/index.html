import type { ParsedOptimisationDraft } from '../types.js';
import { extractParameters, mapUnknownTrial } from './json.js';

/**
 * Parse a cTrader `.cbotset` file (JSON parameter set) into a single-trial run.
 */
export function parseCbotset(text: string): ParsedOptimisationDraft {
  const root = JSON.parse(text) as Record<string, unknown>;
  const chart =
    root.Chart && typeof root.Chart === 'object'
      ? (root.Chart as Record<string, unknown>)
      : undefined;
  const parameters = extractParameters(root.Parameters ?? root.parameters ?? {});

  if (Object.keys(parameters).length === 0) {
    throw new Error('.cbotset file has no Parameters object');
  }

  const trial = mapUnknownTrial(
    {
      pass: 1,
      parameters,
    },
    0,
  );

  return {
    format: 'cbotset',
    instrumentSymbol: typeof chart?.Symbol === 'string' ? chart.Symbol : undefined,
    timeframeCode: typeof chart?.Period === 'string' ? chart.Period : undefined,
    schemaId: 'cbotset.parameters',
    trials: [trial],
    rawMetadata: root,
  };
}
