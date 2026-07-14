/**
 * Initial v1 instrument seed symbols from architecture.
 * Catalog seeding is a later phase — this constant is documentation-aligned metadata only.
 */
export const V1_INSTRUMENT_SYMBOLS = [
  'BTCUSD',
  'ETHUSD',
  'XAUUSD',
  'EURUSD',
  'NAS100',
  'SPX500',
] as const;

export type V1InstrumentSymbol = (typeof V1_INSTRUMENT_SYMBOLS)[number];

/** Product display identity (re-exported for domain consumers). */
export { APP_NAME, APP_VERSION } from '@marketdna/shared';
