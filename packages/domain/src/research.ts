import type {
  ConfidenceScore,
  DateRange,
  ParameterRange,
  ParameterSet,
  Probability,
  Score,
} from './catalog.js';
import type {
  DocketStatus,
  EntityId,
  EvidencePolarity,
  Instant,
  OptimisationRunStatus,
  RegimeLabel,
} from './primitives.js';

/** Point-in-time multi-domain market state package. */
export interface MarketSnapshot {
  id: EntityId;
  instrumentId: EntityId;
  timeframeId: EntityId;
  asOf: Instant;
  universeFingerprint: string;
  coverage: Record<string, boolean>;
}

/** Market character classification at asOf. */
export interface MarketRegime {
  id: EntityId;
  instrumentId: EntityId;
  timeframeId: EntityId;
  asOf: Instant;
  primary: RegimeLabel;
  overlays: RegimeLabel[];
  confidence: ConfidenceScore;
  supportingEvidenceIds: EntityId[];
  contradictingEvidenceIds: EntityId[];
  historicalFrequency: Probability;
  taxonomyVersion: string;
  engineVersion: string;
  snapshotId: EntityId;
}

/** Citeable fact backing a claim. */
export interface Evidence {
  id: EntityId;
  kind: string;
  claim: string;
  polarity: EvidencePolarity;
  asOf: Instant;
  sourceRef: {
    type: string;
    id: EntityId;
  };
}

/** Ranked historical analogue result set (header only in Phase 1A types). */
export interface HistoricalSimilarity {
  id: EntityId;
  querySnapshotId: EntityId;
  asOf: Instant;
  engineVersion: string;
}

/** One optimisation corpus unit (import or internal). */
export interface OptimisationRun {
  id: EntityId;
  strategyId: EntityId;
  instrumentId: EntityId;
  timeframeId: EntityId;
  source: 'import' | 'internal';
  status: OptimisationRunStatus;
  dataFingerprint: string;
  range: DateRange;
  artifactDir: string;
}

/** Single evaluated parameter point. */
export interface OptimisationTrial {
  id: EntityId;
  runId: EntityId;
  parameters: ParameterSet;
  constraintOk: boolean;
}

/** Robust parameter family — core IP shape (types only). */
export interface StrategyDNA {
  id: EntityId;
  strategyId: EntityId;
  ranges: ParameterRange[];
  robustnessScore: Score;
  overfitRisk: Score;
  supportingRunIds: EntityId[];
  engineVersion: string;
  updatedAt: Instant;
}

/** Stress verdict header. */
export interface RobustnessReport {
  id: EntityId;
  subjectType: 'trial' | 'dna';
  subjectId: EntityId;
  robustnessScore: Score;
  gatesPassed: boolean;
  engineVersion: string;
  asOf: Instant;
}

/** One ranked recommendation item. */
export interface Recommendation {
  id: EntityId;
  docketId: EntityId;
  rank: number;
  strategyId: EntityId;
  dnaId?: EntityId;
  parameterRanges: ParameterRange[];
  confidence: ConfidenceScore;
  evidenceIds: EntityId[];
  caveats: string[];
  robustnessReportId: EntityId;
}

/** North-star research packet. */
export interface RecommendationDocket {
  id: EntityId;
  instrumentId: EntityId;
  timeframeId: EntityId;
  strategyId: EntityId;
  asOf: Instant;
  status: DocketStatus;
  regimeId: EntityId;
  confidence: ConfidenceScore;
  reasoningSummary: string;
  universeFingerprint: string;
  engineVersion: string;
}

/** Grade of a past docket vs realised outcomes. */
export interface LearningRecord {
  id: EntityId;
  docketId: EntityId;
  judgement:
    'within_band' | 'underperformed' | 'overperformed' | 'invalidated' | 'insufficient_data';
  createdAt: Instant;
}
