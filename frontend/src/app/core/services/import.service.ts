import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ENVIRONMENT } from '../tokens/environment.token';
import { ApiResponse } from '../models/api-response.model';

@Injectable({ providedIn: 'root' })
export class ImportService {
  private readonly http = inject(HttpClient);
  private readonly env = inject(ENVIRONMENT);

  trigger(): Observable<ApiResponse<{ runId: string }>> {
    return this.http.post<ApiResponse<{ runId: string }>>(
      `${this.env.apiUrl}/api/import/trigger`,
      {}
    );
  }

  getStatus(): Observable<ApiResponse<{ status: string; lastRun: string | null }>> {
    return this.http.get<ApiResponse<{ status: string; lastRun: string | null }>>(
      `${this.env.apiUrl}/api/import/status`
    );
  }
}
