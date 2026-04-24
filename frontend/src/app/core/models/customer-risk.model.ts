import { RiskScore } from './risk-score.model';
import { RiskExplanation } from './risk-explanation.model';
import { SuggestedAction } from './suggested-action.model';

export interface CustomerRisk extends RiskScore {
  riskExplanations: RiskExplanation[];
  suggestedActions: SuggestedAction[];
}
