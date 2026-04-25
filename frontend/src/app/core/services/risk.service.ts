import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ENVIRONMENT } from '../tokens/environment.token';
import { ApiResponse } from '../models/api-response.model';
import { HeatLevel } from '../models/risk-score.model';

export interface TopAtRiskItem {
  customerId: string;
  name: string;
  companyName: string | null;
  segment: string | null;
  churnScore: number;
  paymentScore: number;
  marginScore: number;
  overallScore: number;
  heatLevel: HeatLevel;
}

export interface HeatBand {
  count: number;
  pct: number;
  totalMonthlyValue: number;
}

export interface HeatSummary {
  totalCustomers: number;
  green: HeatBand;
  yellow: HeatBand;
  red: HeatBand;
}

export interface RiskItemAction {
  actionType: string;
  priority: string;
  title: string;
  description: string | null;
}

export interface RiskDimensionItem {
  customerId: string;
  name: string;
  companyName: string | null;
  segment: string | null;
  churnScore: number;
  paymentScore: number;
  marginScore: number;
  overallScore: number;
  heatLevel: HeatLevel;
  monthlyContractValue: number;
  explanation: string | null;
  confidence: string | null;
  topAction: RiskItemAction | null;
}

export interface RiskDimensionGroup {
  dimension: string;
  label: string;
  avgScore: number;
  totalFlagged: number;
  totalMonthlyValue: number;
  items: RiskDimensionItem[];
}

export interface RiskDimensionGroupsResponse {
  heatSummary: HeatSummary;
  dimensions: RiskDimensionGroup[];
}

export interface CustomerScatterPoint {
  customerId: string;
  name: string;
  companyName: string | null;
  segment: string | null;
  churnScore: number;
  paymentScore: number;
  marginScore: number;
  overallScore: number;
  heatLevel: HeatLevel;
  monthlyContractValue: number;
}

@Injectable({ providedIn: 'root' })
export class RiskService {
  private readonly http = inject(HttpClient);
  private readonly env = inject(ENVIRONMENT);

  getDistribution(): Observable<ApiResponse<unknown>> {
    return this.http.get<ApiResponse<unknown>>(
      `${this.env.apiUrl}/api/risk/distribution`
    );
  }

  getTopAtRisk(type: string, limit?: number): Observable<ApiResponse<TopAtRiskItem[]>> {
    let params = new HttpParams().set('type', type);
    if (limit != null) params = params.set('limit', limit.toString());

    return this.http.get<ApiResponse<TopAtRiskItem[]>>(
      `${this.env.apiUrl}/api/risk/top-at-risk`,
      { params }
    );
  }

  getGroups(): Observable<ApiResponse<unknown>> {
    return this.http.get<ApiResponse<unknown>>(
      `${this.env.apiUrl}/api/risk/groups`
    );
  }

  getRiskDimensionGroups(limit = 10): Observable<ApiResponse<RiskDimensionGroupsResponse | null>> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.http.get<ApiResponse<RiskDimensionGroupsResponse | null>>(
      `${this.env.apiUrl}/api/risk/dimension-groups`,
      { params }
    );
  }

  getScatterData(): Observable<ApiResponse<CustomerScatterPoint[]>> {
    return this.http.get<ApiResponse<CustomerScatterPoint[]>>(
      `${this.env.apiUrl}/api/risk/scatter-data`
    );
  }
}
