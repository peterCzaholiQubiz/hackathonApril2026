import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ENVIRONMENT } from '../tokens/environment.token';

@Injectable({ providedIn: 'root' })
export class StatusService {
  private readonly http = inject(HttpClient);
  private readonly env = inject(ENVIRONMENT);

  check(): Observable<null> {
    return this.http.get<null>(`${this.env.apiUrl}/api/status/check`);
  }
}