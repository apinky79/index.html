/**
 * Opaque entity identifier (ULID/UUID string).
 * Never use broker symbols as primary identity.
 */
export type EntityId = string;

/** ISO-8601 UTC timestamp. */
export type Instant = string;

/** Bounded contexts from the frozen domain model. */
export enum BoundedContext {
  MarketData = 'market_data',
  MacroData = 'macro_data',
  MarketIntelligence = 'market_intelligence',
  Optimisation = 'optimisation',
  Recommendations = 'recommendations',
  Learning = 'learning',
  Accounts = 'accounts',
  Settings = 'settings',
}

/** Instrument asset classification. */
export type AssetClass = 'crypto' | 'fx' | 'metal' | 'index' | 'other';

/**
 * Regime taxonomy v1 (docs/architecture/17-domain-model.md).
 * Algorithms may change; labels remain stable.
 */
export type RegimeLabel =
  | 'StrongBull'
  | 'WeakBull'
  | 'StrongBear'
  | 'Range'
  | 'Breakout'
  | 'HighVolatility'
  | 'LowVolatility'
  | 'Accumulation'
  | 'Distribution'
  | 'Capitulation'
  | 'Unresolved';

export type DocketStatus =
  'draft' | 'issued' | 'accepted' | 'rejected' | 'superseded' | 'abstained';

export type OptimisationRunStatus =
  'queued' | 'running' | 'completed' | 'failed' | 'cancelled' | 'imported';

export type EvidencePolarity = 'supports' | 'contradicts' | 'contextual';
