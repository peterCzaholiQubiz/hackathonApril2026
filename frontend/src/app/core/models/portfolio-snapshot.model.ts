export interface PortfolioSnapshot {
  id: string;
  createdAt: string;
  totalCustomers: number;
  greenCount: number;
  yellowCount: number;
  redCount: number;
  greenPct: number;
  yellowPct: number;
  redPct: number;
  avgChurnScore: number;
  avgPaymentScore: number;
  avgMarginScore: number;
  segmentBreakdown: Record<string, { green: number; yellow: number; red: number }>;
}
