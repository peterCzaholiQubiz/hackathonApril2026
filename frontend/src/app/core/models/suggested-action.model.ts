export type ActionType = 'outreach' | 'discount' | 'review' | 'escalate' | 'upsell';
export type Priority = 'high' | 'medium' | 'low';

export interface SuggestedAction {
  id: string;
  riskScoreId: string;
  customerId: string;
  actionType: ActionType;
  priority: Priority;
  title: string;
  description: string | null;
  generatedAt: string;
}
