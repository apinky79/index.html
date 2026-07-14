import type { ImportIssue, ParsedOptimisationDraft } from '../types.js';

export function validateParsedDraft(draft: ParsedOptimisationDraft): ImportIssue[] {
  const issues: ImportIssue[] = [];

  if (!draft.trials.length) {
    issues.push({
      code: 'NO_TRIALS',
      message: 'Import contains no optimisation trials/passes',
      severity: 'error',
    });
  }

  if (!draft.schemaId.trim()) {
    issues.push({
      code: 'MISSING_SCHEMA',
      message: 'Parameter schema id could not be derived',
      severity: 'error',
    });
  }

  draft.trials.forEach((trial, index) => {
    const keys = Object.keys(trial.parameters);
    if (keys.length === 0) {
      issues.push({
        code: 'EMPTY_PARAMETERS',
        message: `Trial at index ${index} has no parameters`,
        path: `trials[${index}]`,
        severity: 'error',
      });
    }
  });

  if (!draft.instrumentSymbol) {
    issues.push({
      code: 'MISSING_INSTRUMENT',
      message: 'Instrument/symbol metadata missing — a placeholder will be assigned',
      severity: 'warning',
    });
  }

  if (!draft.timeframeCode) {
    issues.push({
      code: 'MISSING_TIMEFRAME',
      message: 'Timeframe metadata missing — a placeholder will be assigned',
      severity: 'warning',
    });
  }

  return issues;
}

export function hasBlockingErrors(issues: ImportIssue[]): boolean {
  return issues.some((issue) => issue.severity === 'error');
}
