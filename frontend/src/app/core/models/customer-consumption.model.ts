export interface CustomerConsumptionQueryParams {
  from?: string;
  to?: string;
  unit?: string;
}

export interface CustomerConsumption {
  from: string;
  to: string;
  selectedUnit: string | null;
  availableUnits: string[];
  points: CustomerConsumptionPoint[];
}

export interface CustomerConsumptionPoint {
  month: string;
  consumption: number;
  unit: string;
  quality: string;
  qualityBreakdown: CustomerConsumptionQualityBreakdown[];
}

export interface CustomerConsumptionQualityBreakdown {
  quality: string;
  readCount: number;
  consumption: number;
}