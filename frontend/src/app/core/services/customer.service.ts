import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ENVIRONMENT } from '../tokens/environment.token';
import { ApiResponse } from '../models/api-response.model';
import { Customer, CustomerDetail, Interaction, Complaint } from '../models/customer.model';
import { CustomerConsumption, CustomerConsumptionQueryParams } from '../models/customer-consumption.model';
import { CustomerRisk } from '../models/customer-risk.model';

export interface CustomerListParams {
  segment?: string;
  heatLevel?: string;
  search?: string;
  sortBy?: string;
  sortDir?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class CustomerService {
  private readonly http = inject(HttpClient);
  private readonly env = inject(ENVIRONMENT);

  getList(params: CustomerListParams = {}): Observable<ApiResponse<Customer[]>> {
    let httpParams = new HttpParams();
    if (params.segment) httpParams = httpParams.set('segment', params.segment);
    if (params.heatLevel) httpParams = httpParams.set('heatLevel', params.heatLevel);
    if (params.search) httpParams = httpParams.set('search', params.search);
    if (params.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
    if (params.sortDir) httpParams = httpParams.set('sortDir', params.sortDir);
    if (params.page != null) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize != null) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<ApiResponse<Customer[]>>(
      `${this.env.apiUrl}/api/customers`,
      { params: httpParams }
    );
  }

  getById(id: string): Observable<ApiResponse<CustomerDetail>> {
    return this.http.get<ApiResponse<CustomerDetail>>(
      `${this.env.apiUrl}/api/customers/${id}`
    );
  }

  getRisk(id: string): Observable<ApiResponse<CustomerRisk>> {
    return this.http.get<ApiResponse<CustomerRisk>>(
      `${this.env.apiUrl}/api/customers/${id}/risk`
    );
  }

  getInteractions(id: string): Observable<ApiResponse<Interaction[]>> {
    return this.http.get<ApiResponse<Interaction[]>>(
      `${this.env.apiUrl}/api/customers/${id}/interactions`
    );
  }

  getComplaints(id: string): Observable<ApiResponse<Complaint[]>> {
    return this.http.get<ApiResponse<Complaint[]>>(
      `${this.env.apiUrl}/api/customers/${id}/complaints`
    );
  }

  getConsumption(id: string, params: CustomerConsumptionQueryParams = {}): Observable<ApiResponse<CustomerConsumption>> {
    let httpParams = new HttpParams();
    if (params.from) httpParams = httpParams.set('from', params.from);
    if (params.to) httpParams = httpParams.set('to', params.to);
    if (params.unit) httpParams = httpParams.set('unit', params.unit);

    return this.http.get<ApiResponse<CustomerConsumption>>(
      `${this.env.apiUrl}/api/customers/${id}/consumption`,
      { params: httpParams }
    );
  }
}
