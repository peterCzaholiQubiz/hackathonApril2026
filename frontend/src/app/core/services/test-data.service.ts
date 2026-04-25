import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ENVIRONMENT } from '../tokens/environment.token';
import { ApiResponse } from '../models/api-response.model';
import {
  GenerateActivitiesRequest,
  GenerateActivitiesResponse,
  GenerateTestDataRequest,
  GenerateTestDataResponse,
  GenerateYearlyMeterReadsRequest,
  GenerateYearlyMeterReadsResponse,
} from '../models/test-data.model';

@Injectable({ providedIn: 'root' })
export class TestDataService {
  private readonly http = inject(HttpClient);
  private readonly env = inject(ENVIRONMENT);

  generatePortfolioData(request: GenerateTestDataRequest): Observable<ApiResponse<GenerateTestDataResponse>> {
    return this.http.post<ApiResponse<GenerateTestDataResponse>>(
      `${this.env.apiUrl}/api/test-data/generate`,
      request
    );
  }

  generateActivities(request: GenerateActivitiesRequest): Observable<ApiResponse<GenerateActivitiesResponse>> {
    return this.http.post<ApiResponse<GenerateActivitiesResponse>>(
      `${this.env.apiUrl}/api/test-data/generate-activities`,
      request
    );
  }

  generateYearlyMeterReads(request: GenerateYearlyMeterReadsRequest): Observable<GenerateYearlyMeterReadsResponse> {
    return this.http.post<GenerateYearlyMeterReadsResponse>(
      `${this.env.apiUrl}/api/meter-reads/generate-yearly`,
      request
    );
  }
}
