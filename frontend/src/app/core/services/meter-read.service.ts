import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ENVIRONMENT } from '../tokens/environment.token';
import {
  ConsumptionProfileOption,
  GenerateMeterReadsRequest,
  GenerateMeterReadsResponse,
  PeriodOption,
} from '../models/meter-read.model';

@Injectable({ providedIn: 'root' })
export class MeterReadService {
  private readonly http = inject(HttpClient);
  private readonly env = inject(ENVIRONMENT);

  readonly profileOptions: ConsumptionProfileOption[] = [
    { value: 'LowConsumer',   label: 'Low Consumer',   description: 'Small household or low-usage SMB, ~2 500 kWh/year' },
    { value: 'HighConsumer',  label: 'High Consumer',  description: 'Large household or medium business, ~12 000 kWh/year' },
    { value: 'LowDaytime',    label: 'Low Daytime',    description: 'Night-shift or off-peak heavy user' },
    { value: 'HighDaytime',   label: 'High Daytime',   description: 'Office or retail — heavy peak / daytime usage' },
    { value: 'SolarProducer', label: 'Solar Producer', description: 'Prosumer with rooftop panels' },
    { value: 'Industrial',    label: 'Industrial',     description: 'High-voltage industrial connection, ~80 000 kWh/year' },
  ];

  readonly periodOptions: PeriodOption[] = [
    { value: 'ThreeMonths', label: '3 Months' },
    { value: 'SixMonths',   label: '6 Months' },
    { value: 'OneYear',     label: '1 Year'   },
    { value: 'TwoYears',    label: '2 Years'  },
  ];

  generate(request: GenerateMeterReadsRequest): Observable<GenerateMeterReadsResponse> {
    return this.http.post<GenerateMeterReadsResponse>(
      `${this.env.apiUrl}/api/meter-reads/generate`,
      request
    );
  }
}
