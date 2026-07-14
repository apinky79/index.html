/**
 * Preload bridge — Phase 1A exposes read-only app identity only.
 * Values mirror @marketdna/shared constants (kept inline so the sandboxed
 * preload stays a classic script without ESM workspace imports).
 */
import { contextBridge } from 'electron';

export interface MarketDnaBridge {
  appName: string;
  appVersion: string;
  initMessage: string;
}

const bridge: MarketDnaBridge = {
  appName: 'MarketDNA',
  appVersion: '0.1.0',
  initMessage: 'Application Initialised Successfully',
};

contextBridge.exposeInMainWorld('marketdna', bridge);
