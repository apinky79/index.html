/**
 * Bundle Electron main + preload as CommonJS for runtime compatibility
 * with ESM workspace packages.
 */
import { build } from 'esbuild';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptsDir = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(scriptsDir, '..');

await build({
  entryPoints: [path.join(root, 'electron/main.ts')],
  outfile: path.join(root, 'dist-electron/main.cjs'),
  bundle: true,
  platform: 'node',
  format: 'cjs',
  target: 'node20',
  external: ['electron'],
  sourcemap: true,
});

await build({
  entryPoints: [path.join(root, 'electron/preload.ts')],
  outfile: path.join(root, 'dist-electron/preload.js'),
  bundle: true,
  platform: 'node',
  format: 'cjs',
  target: 'node20',
  external: ['electron'],
  sourcemap: true,
});

console.log('Electron bundles written to dist-electron/');
