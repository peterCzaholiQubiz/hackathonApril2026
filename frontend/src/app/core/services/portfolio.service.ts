import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ENVIRONMENT } from '../tokens/environment.token';
import { ApiResponse } from '../models/api-response.model';
import { PortfolioSnapshot } from '../models/portfolio-snapshot.model';

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
}
