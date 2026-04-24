import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ENVIRONMENT } from '../tokens/environment.token';
import { ApiResponse } from '../models/api-response.model';

@Injectable({ providedIn: 'root' })
export class RiskService {
  private readonly http = inject(HttpClient);
  private readonly env = inject(ENVIRONMENT);

  getDistribution(): Observable<ApiResponse<unknown>> {
    return this.http.get<ApiResponse<unknown>>(
      `${this.env.apiUrl}/api/risk/distribution`
    );
  }

  getTopAtRisk(type: string, limit?: number): Observable<ApiResponse<unknown[]>> {
    let params = new HttpParams().set('type', type);
    if (limit != null) params = params.set('limit', limit.toString());

    return this.http.get<ApiResponse<unknown[]>>(
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
