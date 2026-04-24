export type PaymentSeverity = 'low' | 'medium' | 'high';

export interface CustomerPayment {
  id: string;
  crmExternalId: string;
  invoiceId: string;
  paymentDate: string | null;
  amount: number | null;
  daysLate: number;
  severity: PaymentSeverity;
}

export interface PaymentSummary {
  low: number;
  medium: number;
  high: number;
}

export interface CustomerPayments {
  activeSeverity: PaymentSeverity | null;
  summary: PaymentSummary;
  payments: CustomerPayment[];
}

export interface CustomerPaymentsParams {
  severity?: PaymentSeverity;
  page?: number;
  pageSize?: number;
}