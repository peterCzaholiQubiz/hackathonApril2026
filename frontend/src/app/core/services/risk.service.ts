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
}
