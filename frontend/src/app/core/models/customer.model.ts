import { RiskScore, RiskScoreSummary } from './risk-score.model';

export interface Customer {
  id: string;
  crmExternalId: string;
  name: string;
  companyName: string | null;
  email: string | null;
  phone: string | null;
  segment: string | null;
  accountManager: string | null;
  onboardingDate: string | null;
  isActive: boolean;
  importedAt: string;
  updatedAt: string;
  energyTypes?: string[];
  latestRisk?: RiskScoreSummary | null;
  riskScores?: RiskScore[];
}

export interface CustomerDetail extends Customer {
  contracts: Contract[];
  invoices: Invoice[];
}

export interface Contract {
  id: string;
  customerId: string;
  contractType: string | null;
  startDate: string | null;
  endDate: string | null;
  monthlyValue: number | null;
  currency: string;
  status: string | null;
  autoRenew: boolean;
}

export interface Invoice {
  id: string;
  customerId: string;
  invoiceNumber: string | null;
  issuedDate: string | null;
  dueDate: string | null;
  amount: number | null;
  currency: string;
  status: string | null;
}

export interface Interaction {
  id: string;
  customerId: string;
  interactionDate: string | null;
  channel: string | null;
  direction: string | null;
  summary: string | null;
  sentiment: string | null;
}

export interface Complaint {
  id: string;
  customerId: string;
  createdDate: string | null;
  resolvedDate: string | null;
  category: string | null;
  severity: string | null;
  description: string | null;
}
