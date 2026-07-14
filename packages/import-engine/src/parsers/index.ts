import { assertSupportedFormat } from '../detect-format.js';
import type { ImportFormat, ParsedOptimisationDraft } from '../types.js';
import { parseCbotset } from './cbotset.js';
import { parseCsv } from './csv.js';
import { parseNativeJson } from './json.js';
import { parseOptres } from './optres.js';

export function parseOptimisationContent(
  format: ImportFormat,
  content: string,
): ParsedOptimisationDraft {
  switch (format) {
    case 'optres':
      return parseOptres(content);
    case 'cbotset':
      return parseCbotset(content);
    case 'csv':
      return parseCsv(content);
    case 'json':
      return parseNativeJson(content);
    default: {
      const _exhaustive: never = format;
      throw new Error(`Unhandled format: ${_exhaustive}`);
    }
  }
}

export function parseOptimisationFile(filePath: string, content: string): ParsedOptimisationDraft {
  const format = assertSupportedFormat(filePath);
  return parseOptimisationContent(format, content);
}

export { parseCbotset, parseCsv, parseNativeJson, parseOptres };
