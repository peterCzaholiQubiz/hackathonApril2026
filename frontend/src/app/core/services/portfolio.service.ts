import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ENVIRONMENT } from '../tokens/environment.token';
import { ApiResponse } from '../models/api-response.model';
import { PortfolioSnapshot } from '../models/portfolio-snapshot.model';

export interface EnergyHeatmapCell {
  year: number;
  month: number;
  total: number;
}

@Injectable({ providedIn: 'root' })
export class PortfolioService {
  private readonly http = inject(HttpClient);
  private readonly env = inject(ENVIRONMENT);

  getCurrent(): Observable<ApiResponse<PortfolioSnapshot>> {
    return this.http.get<ApiResponse<PortfolioSnapshot>>(
      `${this.env.apiUrl}/api/portfolio/current`
    );
  }

  getHistory(): Observable<ApiResponse<PortfolioSnapshot[]>> {
    return this.http.get<ApiResponse<PortfolioSnapshot[]>>(
      `${this.env.apiUrl}/api/portfolio/history`
    );
  }

  getSegments(): Observable<ApiResponse<Record<string, unknown>>> {
    return this.http.get<ApiResponse<Record<string, unknown>>>(
      `${this.env.apiUrl}/api/portfolio/segments`
    );
  }

  getEnergyHeatmap(unit: 'kWh' | 'm3', direction: 'Consumption' | 'Production'): Observable<ApiResponse<EnergyHeatmapCell[]>> {
    const params = new HttpParams().set('unit', unit).set('direction', direction);
    return this.http.get<ApiResponse<EnergyHeatmapCell[]>>(
      `${this.env.apiUrl}/api/portfolio/energy-heatmap`,
      { params }
    );
  }
}
