export type ConsumptionProfile =
  | 'LowConsumer'
  | 'HighConsumer'
  | 'LowDaytime'
  | 'HighDaytime'
  | 'SolarProducer'
  | 'Industrial';

export type GenerationPeriod = 'ThreeMonths' | 'SixMonths' | 'OneYear' | 'TwoYears';

export interface GenerateMeterReadsRequest {
  customerId: string;
  profile: ConsumptionProfile;
  period: GenerationPeriod;
}

export interface DailyMeterReadSummary {
  date: string;
  consumptionHigh: number;
  consumptionLow: number;
  totalConsumption: number;
  production: number;
}

export interface GenerateMeterReadsResponse {
  customerId: string;
  profile: ConsumptionProfile;
  period: GenerationPeriod;
  totalHourlyRowsGenerated: number;
  dailySummary: DailyMeterReadSummary[];
}

export interface ConsumptionProfileOption {
  value: ConsumptionProfile;
  label: string;
  description: string;
}

export interface PeriodOption {
  value: GenerationPeriod;
  label: string;
}
