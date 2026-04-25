export interface GenerateTestDataRequest {
  customerCount: number;
  runPipeline: boolean;
}

export interface GenerateTestDataResponse {
  customersCreated: number;
  connectionsCreated: number;
  meterReadsCreated: number;
  contractsCreated: number;
  invoicesCreated: number;
  paymentsCreated: number;
  complaintsCreated: number;
  interactionsCreated: number;
  pipelineRan: boolean;
}

export interface GenerateYearlyMeterReadsRequest {
  customerIds: string[];
  year: number;
  producerPercentage: number;
}

export interface MeterReadGenerationSkippedCustomer {
  customerId: string;
  reason: string;
}

export interface GenerateYearlyMeterReadsResponse {
  year: number;
  requestedCustomerCount: number;
  eligibleCustomerCount: number;
  producerCustomerCount: number;
  processedConnectionCount: number;
  consumptionRowsGenerated: number;
  productionRowsGenerated: number;
  reducedConsumptionHourCount: number;
  producerCustomerIds: string[];
  skippedCustomers: MeterReadGenerationSkippedCustomer[];
}
