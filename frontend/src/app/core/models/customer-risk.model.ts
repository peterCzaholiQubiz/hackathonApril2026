import { RiskScore } from './risk-score.model';
import { RiskExplanation } from './risk-explanation.model';
import { SuggestedAction } from './suggested-action.model';

export interface CustomerRisk {
  score: RiskScore;
  explanations: RiskExplanation[];
  actions: SuggestedAction[];
}
