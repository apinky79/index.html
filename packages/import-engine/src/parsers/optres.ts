import type { ParsedOptimisationDraft } from '../types.js';
import { extractMetrics, extractParameters, mapUnknownTrial, parseNativeJson } from './json.js';

/**
 * Parse a `.optres` optimisation results file.
 *
 * cTrader documents .optres as a collection of key-value pairs with settings
 * and metrics. MarketDNA accepts JSON-encoded .optres documents containing a
 * passes/results array. Binary proprietary blobs are rejected with a clear error.
 */
export function parseOptres(text: string): ParsedOptimisationDraft {
  const trimmed = text.trim();
  if (!trimmed.startsWith('{') && !trimmed.startsWith('[')) {
    throw new Error(
      '.optres appears binary or non-JSON. Export/save as JSON key-value .optres or use CSV/JSON.',
    );
  }

  const parsed = JSON.parse(trimmed) as unknown;

  if (Array.isArray(parsed)) {
    const trials = parsed.map((item, index) => mapUnknownTrial(item, index));
    return {
      format: 'optres',
      schemaId: 'optres.parameters',
      trials,
      rawMetadata: { passes: parsed },
    };
  }

  if (!parsed || typeof parsed !== 'object') {
    throw new Error('.optres root must be a JSON object or array');
  }

  const root = parsed as Record<string, unknown>;

  // Prefer MarketDNA / generic JSON parser structure, then remap format.
  if (
    Array.isArray(root.trials) ||
    Array.isArray(root.passes) ||
    Array.isArray(root.Passes) ||
    Array.isArray(root.Results) ||
    Array.isArray(root.OptimizationResults)
  ) {
    const draft = parseNativeJson(JSON.stringify(root));
    return {
      ...draft,
      format: 'optres',
      schemaId: draft.schemaId.startsWith('schema_') ? 'optres.parameters' : draft.schemaId,
      strategyName:
        draft.strategyName ??
        (typeof root.RobotName === 'string' ? root.RobotName : undefined) ??
        (typeof root.AlgorithmName === 'string' ? root.AlgorithmName : undefined),
      instrumentSymbol:
        draft.instrumentSymbol ?? (typeof root.Symbol === 'string' ? root.Symbol : undefined),
      timeframeCode:
        draft.timeframeCode ?? (typeof root.Period === 'string' ? root.Period : undefined),
    };
  }

  // Flat key-value form: Passes.<n>.* or pass-indexed nested maps
  const nestedPasses = collectIndexedPasses(root);
  if (nestedPasses.length > 0) {
    return {
      format: 'optres',
      strategyName: typeof root.RobotName === 'string' ? root.RobotName : undefined,
      instrumentSymbol: typeof root.Symbol === 'string' ? root.Symbol : undefined,
      timeframeCode: typeof root.Period === 'string' ? root.Period : undefined,
      range: {
        start:
          typeof root.StartDate === 'string' ? new Date(root.StartDate).toISOString() : undefined,
        end: typeof root.EndDate === 'string' ? new Date(root.EndDate).toISOString() : undefined,
        inclusivity: 'startClosedEndOpen',
      },
      schemaId: 'optres.parameters',
      trials: nestedPasses,
      rawMetadata: root,
    };
  }

  // Single pass stored as top-level Parameters + metrics
  const parameters = extractParameters(root.Parameters ?? root.parameters);
  if (Object.keys(parameters).length > 0) {
    return {
      format: 'optres',
      strategyName: typeof root.RobotName === 'string' ? root.RobotName : undefined,
      instrumentSymbol: typeof root.Symbol === 'string' ? root.Symbol : undefined,
      timeframeCode: typeof root.Period === 'string' ? root.Period : undefined,
      schemaId: 'optres.parameters',
      trials: [
        {
          pass: 1,
          parameters,
          metrics: extractMetrics(root),
          constraintOk: true,
        },
      ],
      rawMetadata: root,
    };
  }

  throw new Error(
    '.optres JSON did not contain recognisable passes/trials. Expected Passes/Results arrays or Parameters.',
  );
}

function collectIndexedPasses(root: Record<string, unknown>): ReturnType<typeof mapUnknownTrial>[] {
  const passMap = new Map<number, Record<string, unknown>>();

  for (const [key, value] of Object.entries(root)) {
    const match = /^Passes?[.[](\d+)[.\]]?(.*)$/i.exec(key);
    if (!match) continue;
    const pass = Number(match[1]);
    const rest = match[2]?.replace(/^[.\]]/, '') ?? '';
    const bucket = passMap.get(pass) ?? { pass };
    if (rest.toLowerCase().startsWith('param')) {
      const params =
        (bucket.parameters as Record<string, unknown> | undefined) ??
        ((bucket.parameters = {}) as Record<string, unknown>);
      params[rest.replace(/^parameters?[.\]]?/i, '') || rest] = value;
    } else if (rest) {
      bucket[rest] = value;
    } else if (value && typeof value === 'object') {
      Object.assign(bucket, value as object);
    }
    passMap.set(pass, bucket);
  }

  if (passMap.size === 0) return [];
  return [...passMap.entries()]
    .sort((a, b) => a[0] - b[0])
    .map(([pass, record], index) => mapUnknownTrial({ ...record, pass }, index));
}
