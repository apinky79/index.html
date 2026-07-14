import type { DateRange, OptimisationRun, OptimisationTrial } from '@marketdna/domain';
import { createId, sha256Hex } from '@marketdna/shared';

import type { ImportFormat, MappedImport, ParsedOptimisationDraft } from './types.js';

function normaliseSymbol(symbol?: string): string {
  return (symbol ?? 'UNKNOWN').toUpperCase().replace(/[^A-Z0-9]/g, '');
}

function normaliseTimeframe(code?: string): string {
  if (!code) return 'UNKNOWN';
  return code.toUpperCase();
}

function defaultRange(): DateRange {
  return {
    start: '1970-01-01T00:00:00.000Z',
    end: '1970-01-01T00:00:00.001Z',
    inclusivity: 'startClosedEndOpen',
  };
}

/**
 * Map a parsed draft into immutable domain objects.
 */
export function mapDraftToDomain(
  draft: ParsedOptimisationDraft,
  options: {
    contentHash: string;
    sourceFileName: string;
    artifactDir: string;
    importedAt?: string;
  },
): MappedImport {
  const runId = createId('opt');
  const instrumentSymbol = normaliseSymbol(draft.instrumentSymbol);
  const timeframeCode = normaliseTimeframe(draft.timeframeCode);
  const strategyName = draft.strategyName ?? pathStem(options.sourceFileName);
  const strategyId = draft.strategyId ?? `strat_${slug(strategyName)}`;
  const instrumentId = `inst_${instrumentSymbol.toLowerCase()}`;
  const timeframeId = `tf_${timeframeCode.toLowerCase()}`;
  const importedAt = options.importedAt ?? new Date().toISOString();

  const run: OptimisationRun = {
    id: runId,
    strategyId,
    instrumentId,
    timeframeId,
    source: 'import',
    status: 'imported',
    dataFingerprint: options.contentHash,
    range: {
      start: draft.range?.start ?? defaultRange().start,
      end: draft.range?.end ?? defaultRange().end,
      inclusivity: draft.range?.inclusivity ?? 'startClosedEndOpen',
    },
    artifactDir: options.artifactDir,
    strategyName,
    instrumentSymbol,
    timeframeCode,
    trialCount: draft.trials.length,
    sourceFileName: options.sourceFileName,
    importedAt,
  };

  const trials: OptimisationTrial[] = draft.trials.map((trial, index) => ({
    id: createId('trial'),
    runId,
    pass: trial.pass ?? index + 1,
    parameters: {
      schemaId: draft.schemaId,
      values: trial.parameters,
    },
    constraintOk: trial.constraintOk ?? true,
    metrics: trial.metrics,
  }));

  return {
    run,
    trials,
    contentHash: options.contentHash,
    format: draft.format,
  };
}

export function fingerprintBuffer(buffer: Buffer): string {
  return sha256Hex(buffer);
}

export function buildArtifactDir(workspaceRoot: string, runId: string): string {
  return `${workspaceRoot.replace(/\\/g, '/')}/artifacts/opt-runs/${runId}`;
}

function slug(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_|_$/g, '')
    .slice(0, 48);
}

function pathStem(fileName: string): string {
  const base = fileName.includes('.') ? fileName.slice(0, fileName.lastIndexOf('.')) : fileName;
  return base || 'imported_strategy';
}

export type { ImportFormat };
