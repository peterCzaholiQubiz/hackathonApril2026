export type RiskType = 'churn' | 'payment' | 'margin' | 'overall';
export type Confidence = 'high' | 'medium' | 'low';

export interface RiskExplanation {
  id: string;
  riskScoreId: string;
  customerId: string;
  riskType: RiskType;
  explanation: string;
  confidence: Confidence;
  generatedAt: string;
  modelUsed: string;
}
