import path from 'node:path';

import type { ImportFormat } from './types.js';

const EXTENSION_MAP: Record<string, ImportFormat> = {
  '.optres': 'optres',
  '.cbotset': 'cbotset',
  '.csv': 'csv',
  '.json': 'json',
};

export function detectFormatFromPath(filePath: string): ImportFormat | undefined {
  const ext = path.extname(filePath).toLowerCase();
  return EXTENSION_MAP[ext];
}

export function assertSupportedFormat(filePath: string): ImportFormat {
  const format = detectFormatFromPath(filePath);
  if (!format) {
    throw new Error(
      `Unsupported file type: ${path.extname(filePath) || '(none)'}. Supported: .optres, .cbotset, .csv, .json`,
    );
  }
  return format;
}
