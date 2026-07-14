/**
 * Trial performance metrics extracted during import.
 * Kept additive so future engines can attach without renaming domain shapes.
 */
export interface TrialMetrics {
  fitness?: number;
  netProfit?: number;
  equity?: number;
  balance?: number;
  trades?: number;
  winningTrades?: number;
  losingTrades?: number;
  profitFactor?: number;
  maxEquityDrawdownPct?: number;
  maxBalanceDrawdownPct?: number;
  maxEquityDrawdown?: number;
  maxBalanceDrawdown?: number;
  averageTrade?: number;
  [key: string]: number | undefined;
}

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
