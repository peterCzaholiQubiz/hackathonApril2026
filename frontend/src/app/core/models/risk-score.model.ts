export type HeatLevel = 'green' | 'yellow' | 'red';

export interface RiskScore {
  id: string;
  customerId: string;
  snapshotId: string;
  churnScore: number;
  paymentScore: number;
  marginScore: number;
  overallScore: number;
  heatLevel: HeatLevel;
  scoredAt: string;
}

export interface RiskScoreSummary {
  churnScore: number;
  paymentScore: number;
  marginScore: number;
  overallScore: number;
  heatLevel: HeatLevel;
  scoredAt: string;
}
