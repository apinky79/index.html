export type {
  AssetClass,
  DocketStatus,
  EntityId,
  EvidencePolarity,
  Instant,
  OptimisationRunStatus,
  RegimeLabel,
} from './primitives.js';

export { BoundedContext } from './primitives.js';

export type {
  ConfidenceScore,
  DateRange,
  Instrument,
  Market,
  ParameterRange,
  ParameterSet,
  Probability,
  Score,
  Timeframe,
} from './catalog.js';

export type {
  Evidence,
  HistoricalSimilarity,
  LearningRecord,
  MarketRegime,
  MarketSnapshot,
  OptimisationRun,
  OptimisationTrial,
  Recommendation,
  RecommendationDocket,
  RobustnessReport,
  StrategyDNA,
} from './research.js';

export type { TrialMetrics } from './metrics.js';

export { V1_INSTRUMENT_SYMBOLS, type V1InstrumentSymbol } from './meta.js';
export { APP_NAME, APP_VERSION } from './meta.js';
