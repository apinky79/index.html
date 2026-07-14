import type { AssetClass, EntityId, Instant } from './primitives.js';

/** Tradable market universe entry. */
export interface Market {
  id: EntityId;
  code: string;
  name: string;
  assetClass: AssetClass;
  isActive: boolean;
}

/** Canonical research instrument (e.g. BTCUSD). */
export interface Instrument {
  id: EntityId;
  marketId: EntityId;
  symbol: string;
  displayName: string;
  baseCode?: string;
  quoteCode?: string;
  isActive: boolean;
}

/** Canonical bar resolution. */
export interface Timeframe {
  id: EntityId;
  code: string;
  minutes: number;
  isStandard: boolean;
}

/** Half-open or closed research window. */
export interface DateRange {
  start: Instant;
  end: Instant;
  inclusivity: 'closed' | 'startClosedEndOpen';
}

/** Probability in [0, 1]. */
export interface Probability {
  value: number;
}

/** Calibrated confidence attached to assessments. */
export interface ConfidenceScore {
  value: number;
  method: string;
  calibrationRef?: EntityId;
}

/** Normalised score. */
export interface Score {
  value: number;
  scale: '0_1' | 'unbounded';
  direction: 'higherBetter' | 'lowerBetter';
}

/** Durable interval for one strategy parameter. */
export interface ParameterRange {
  name: string;
  type: 'int' | 'float' | 'enum';
  min: number | string;
  max: number | string;
  step?: number;
  unit?: string;
}

/** Concrete parameter point. */
export interface ParameterSet {
  schemaId: string;
  values: Record<string, number | string | boolean>;
}
