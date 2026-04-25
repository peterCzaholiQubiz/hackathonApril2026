import { DecimalPipe } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { Customer } from '../../core/models/customer.model';
import {
  GenerateTestDataResponse,
  GenerateYearlyMeterReadsResponse,
  MeterReadGenerationSkippedCustomer,
} from '../../core/models/test-data.model';
import { CustomerService } from '../../core/services/customer.service';
import { TestDataService } from '../../core/services/test-data.service';

@Component({
  selector: 'app-test-data',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  template: `
    <div class="test-data">
      <header class="test-data__header">
        <h1 class="test-data__title">Test Data</h1>
        <p class="test-data__subtitle">
          Generate synthetic portfolio data and create yearly hourly meter reads for a selected customer subset.
        </p>
      </header>

      <section class="card test-data__card">
        <div class="test-data__section-header">
          <div>
            <h2>Portfolio dataset</h2>
            <p>Create synthetic customers, contracts, invoices, interactions, and baseline meter reads.</p>
          </div>
        </div>

        <div class="test-data__controls">
          <label class="control-group">
            <span class="control-label">Customer count</span>
            <input class="control-input" type="number" min="1" max="500" [(ngModel)]="syntheticCustomerCount" />
          </label>

          <label class="control-group control-group--checkbox">
            <input type="checkbox" [(ngModel)]="syntheticRunPipeline" />
            <span>Run risk pipeline after generation</span>
          </label>

          <button class="btn btn--primary" (click)="generatePortfolioData()" [disabled]="!canGeneratePortfolio">
            @if (generatingSynthetic) { Generating… } @else { Generate test data }
          </button>
        </div>

        @if (syntheticError) {
          <div class="test-data__error">{{ syntheticError }}</div>
        }

        @if (syntheticResponse) {
          <div class="test-data__stats">
            <div class="stat-card">
              <span class="stat-card__label">Customers</span>
              <span class="stat-card__value">{{ syntheticResponse.customersCreated | number }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-card__label">Connections</span>
              <span class="stat-card__value">{{ syntheticResponse.connectionsCreated | number }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-card__label">Meter reads</span>
              <span class="stat-card__value">{{ syntheticResponse.meterReadsCreated | number }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-card__label">Pipeline</span>
              <span class="stat-card__value">{{ syntheticResponse.pipelineRan ? 'Ran' : 'Skipped' }}</span>
            </div>
          </div>
        }
      </section>

      <section class="card test-data__card">
        <div class="test-data__section-header">
          <div>
            <h2>Yearly hourly meter reads</h2>
            <p>Select up to {{ maxSelectableCustomers }} customers. A deterministic random subset becomes solar producers.</p>
          </div>
        </div>

        <div class="test-data__controls">
          <label class="control-group">
            <span class="control-label">Year</span>
            <input
              class="control-input"
              type="number"
              min="2000"
              max="2100"
              [disabled]="generatingYearly"
              [(ngModel)]="generationYear"
              (ngModelChange)="onYearOrProducerChanged()" />
          </label>

          <label class="control-group">
            <span class="control-label">Producer share (%)</span>
            <input
              class="control-input"
              type="number"
              min="0"
              max="100"
              step="1"
              [disabled]="generatingYearly"
              [(ngModel)]="producerPercentage"
              (ngModelChange)="onYearOrProducerChanged()" />
          </label>

          <label class="control-group control-group--search">
            <span class="control-label">Search customers</span>
            <input
              class="control-input"
              type="search"
              placeholder="Filter by name or company"
              [disabled]="generatingYearly"
              [(ngModel)]="customerSearch" />
          </label>

          <div class="control-actions">
            <button class="btn btn--secondary" (click)="selectVisibleCustomers()" [disabled]="loadingCustomers || generatingYearly">
              Select visible
            </button>
            <button class="btn btn--ghost" (click)="clearSelection()" [disabled]="selectedCustomerIds.length === 0 || generatingYearly">
              Clear
            </button>
          </div>
        </div>

        <div class="test-data__selection-summary">
          <span>{{ selectedCustomerIds.length }} selected</span>
          <span>{{ filteredCustomers.length }} visible</span>
          <span>{{ customers.length }} loaded</span>
        </div>

        @if (customerLoadError) {
          <div class="test-data__error">{{ customerLoadError }}</div>
        }

        @if (yearlyError) {
          <div class="test-data__error">{{ yearlyError }}</div>
        }

        <div class="customer-picker" [class.customer-picker--loading]="loadingCustomers">
          @if (loadingCustomers) {
            <div class="customer-picker__empty">Loading customers…</div>
          } @else if (filteredCustomers.length === 0) {
            <div class="customer-picker__empty">No customers match the current filter.</div>
          } @else {
            @for (customer of filteredCustomers; track customer.id) {
              <label class="customer-option">
                <input
                  type="checkbox"
                  [disabled]="generatingYearly"
                  [checked]="isSelected(customer.id)"
                  (change)="toggleCustomer(customer.id, $any($event.target).checked)" />
                <div class="customer-option__body">
                  <span class="customer-option__name">{{ customer.companyName || customer.name }}</span>
                  <span class="customer-option__meta">
                    {{ customer.segment || 'Unsegmented' }} · {{ customer.crmExternalId }}
                  </span>
                </div>
              </label>
            }
          }
        </div>

        <div class="test-data__footer">
          <p class="test-data__hint">
            Existing generated rows for the same selected customers, year, and connections are replaced before new rows are inserted. Customers that already have non-generated reads for that year are skipped to avoid overlap.
          </p>

          <button class="btn btn--primary" (click)="generateYearlyMeterReads()" [disabled]="!canGenerateYearly">
            @if (generatingYearly) { Generating… } @else { Generate yearly meter reads }
          </button>
        </div>

        @if (yearlyResponse) {
          <div class="test-data__stats">
            <div class="stat-card">
              <span class="stat-card__label">Eligible customers</span>
              <span class="stat-card__value">{{ yearlyResponse.eligibleCustomerCount | number }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-card__label">Producer customers</span>
              <span class="stat-card__value">{{ yearlyResponse.producerCustomerCount | number }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-card__label">Consumption rows</span>
              <span class="stat-card__value">{{ yearlyResponse.consumptionRowsGenerated | number }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-card__label">Production rows</span>
              <span class="stat-card__value">{{ yearlyResponse.productionRowsGenerated | number }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-card__label">Reduced hours</span>
              <span class="stat-card__value">{{ yearlyResponse.reducedConsumptionHourCount | number }}</span>
            </div>
          </div>

          @if (yearlyResponse.producerCustomerIds.length > 0) {
            <div class="result-list">
              <h3>Producer customers</h3>
              <ul>
                @for (customerId of yearlyResponse.producerCustomerIds; track customerId) {
                  <li>{{ customerLabel(customerId) }}</li>
                }
              </ul>
            </div>
          }

          @if (yearlyResponse.skippedCustomers.length > 0) {
            <div class="result-list">
              <h3>Skipped customers</h3>
              <ul>
                @for (item of skippedCustomers; track item.customerId) {
                  <li>{{ customerLabel(item.customerId) }} — {{ item.reason }}</li>
                }
              </ul>
            </div>
          }
        }
      </section>
    </div>
  `,
  styles: [`
    .test-data {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
      padding: 2rem;
    }

    .test-data__header {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .test-data__title {
      margin: 0;
      font-size: 1.5rem;
      font-weight: 700;
    }

    .test-data__subtitle {
      margin: 0;
      color: var(--color-text-muted, #6b7280);
    }

    .test-data__card {
      padding: 1.5rem;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .test-data__section-header h2,
    .result-list h3 {
      margin: 0 0 0.25rem;
    }

    .test-data__section-header p,
    .result-list ul,
    .test-data__hint {
      margin: 0;
      color: var(--color-text-muted, #6b7280);
    }

    .test-data__controls {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      align-items: flex-end;
    }

    .control-group {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
      min-width: 160px;
    }

    .control-group--search {
      flex: 1;
      min-width: 260px;
    }

    .control-group--checkbox {
      flex-direction: row;
      align-items: center;
      min-width: auto;
      gap: 0.5rem;
      padding-bottom: 0.5rem;
    }

    .control-label {
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--color-text-muted, #6b7280);
    }

    .control-input {
      width: 100%;
      padding: 0.625rem 0.75rem;
      border: 1px solid var(--color-border, #d1d5db);
      border-radius: 0.5rem;
      background: var(--color-surface, #fff);
      color: var(--color-text, #111827);
      font: inherit;
    }

    .control-actions {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
    }

    .btn {
      border: none;
      border-radius: 0.5rem;
      padding: 0.75rem 1rem;
      font: inherit;
      font-weight: 600;
      cursor: pointer;
    }

    .btn:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }

    .btn--primary {
      background: var(--color-accent, #3b82f6);
      color: #fff;
    }

    .btn--secondary {
      background: var(--color-surface-2, #eef2ff);
      color: var(--color-text, #111827);
    }

    .btn--ghost {
      background: transparent;
      color: var(--color-text-muted, #6b7280);
      border: 1px solid var(--color-border, #d1d5db);
    }

    .test-data__error {
      padding: 0.875rem 1rem;
      background: #fef2f2;
      border-radius: 0.5rem;
      color: #dc2626;
    }

    .test-data__selection-summary {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      font-size: 0.875rem;
      color: var(--color-text-muted, #6b7280);
    }

    .customer-picker {
      max-height: 22rem;
      overflow: auto;
      border: 1px solid var(--color-border, #d1d5db);
      border-radius: 0.75rem;
      background: var(--color-surface, #fff);
    }

    .customer-picker--loading {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 12rem;
    }

    .customer-picker__empty {
      padding: 1.5rem;
      text-align: center;
      color: var(--color-text-muted, #6b7280);
    }

    .customer-option {
      display: flex;
      gap: 0.75rem;
      padding: 0.875rem 1rem;
      border-bottom: 1px solid var(--color-border, #e5e7eb);
      cursor: pointer;
      align-items: flex-start;
    }

    .customer-option:last-child {
      border-bottom: none;
    }

    .customer-option__body {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .customer-option__name {
      font-weight: 600;
      color: var(--color-text, #111827);
    }

    .customer-option__meta {
      font-size: 0.8125rem;
      color: var(--color-text-muted, #6b7280);
    }

    .test-data__footer {
      display: flex;
      gap: 1rem;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
    }

    .test-data__hint {
      max-width: 44rem;
      font-size: 0.875rem;
    }

    .test-data__stats {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 1rem;
    }

    .stat-card {
      padding: 1rem 1.25rem;
      border: 1px solid var(--color-border, #e5e7eb);
      border-radius: 0.75rem;
      background: var(--color-surface, #fff);
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .stat-card__label {
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--color-text-muted, #6b7280);
    }

    .stat-card__value {
      font-size: 1.5rem;
      font-weight: 700;
      color: var(--color-text, #111827);
    }

    .result-list ul {
      padding-left: 1.25rem;
    }
  `],
})
export class TestDataComponent implements OnInit {
  readonly maxSelectableCustomers = 25;

  private readonly customerService = inject(CustomerService);
  private readonly testDataService = inject(TestDataService);
  private customerLoadVersion = 0;

  customers: Customer[] = [];
  customerSearch = '';
  selectedCustomerIds: string[] = [];

  loadingCustomers = false;
  customerLoadError: string | null = null;

  syntheticCustomerCount = 25;
  syntheticRunPipeline = true;
  generatingSynthetic = false;
  syntheticError: string | null = null;
  syntheticResponse: GenerateTestDataResponse | null = null;

  generationYear = new Date().getUTCFullYear();
  producerPercentage = 30;
  generatingYearly = false;
  yearlyError: string | null = null;
  yearlyResponse: GenerateYearlyMeterReadsResponse | null = null;

  get filteredCustomers(): Customer[] {
    const term = this.customerSearch.trim().toLowerCase();
    if (!term) {
      return this.customers;
    }

    return this.customers.filter((customer) => {
      const label = `${customer.companyName || ''} ${customer.name} ${customer.crmExternalId}`.toLowerCase();
      return label.includes(term);
    });
  }

  get canGenerateYearly(): boolean {
    return !this.generatingYearly
      && this.selectedCustomerIds.length > 0
      && this.selectedCustomerIds.length <= this.maxSelectableCustomers
      && Number.isInteger(this.generationYear)
      && this.generationYear >= 2000
      && this.generationYear <= 2100
      && Number.isInteger(this.producerPercentage)
      && this.producerPercentage >= 0
      && this.producerPercentage <= 100;
  }

  get canGeneratePortfolio(): boolean {
    return !this.generatingSynthetic
      && Number.isInteger(this.syntheticCustomerCount)
      && this.syntheticCustomerCount >= 1
      && this.syntheticCustomerCount <= 500;
  }

  get skippedCustomers(): MeterReadGenerationSkippedCustomer[] {
    return this.yearlyResponse?.skippedCustomers ?? [];
  }

  async ngOnInit(): Promise<void> {
    await this.loadAllCustomers();
  }

  async loadAllCustomers(): Promise<void> {
    const loadVersion = ++this.customerLoadVersion;
    this.loadingCustomers = true;
    this.customerLoadError = null;

    try {
      const loadedCustomers: Customer[] = [];
      let page = 1;
      let total = 0;

      do {
        const response = await firstValueFrom(
          this.customerService.getList({
            page,
            pageSize: 100,
            sortBy: 'name',
            sortDir: 'asc',
          })
        );

        loadedCustomers.push(...(response.data ?? []));
        total = response.meta?.total ?? loadedCustomers.length;
        page += 1;
      } while (loadedCustomers.length < total);

      if (loadVersion === this.customerLoadVersion) {
        this.customers = loadedCustomers.sort((left, right) =>
          (left.companyName || left.name).localeCompare(right.companyName || right.name)
        );
      }
    } catch {
      if (loadVersion === this.customerLoadVersion) {
        this.customerLoadError = 'Unable to load customers for selection.';
      }
    } finally {
      if (loadVersion === this.customerLoadVersion) {
        this.loadingCustomers = false;
      }
    }
  }

  isSelected(customerId: string): boolean {
    return this.selectedCustomerIds.includes(customerId);
  }

  toggleCustomer(customerId: string, checked: boolean): void {
    this.clearYearlyResults();
    this.yearlyError = null;

    if (checked) {
      if (this.isSelected(customerId)) {
        return;
      }

      if (this.selectedCustomerIds.length >= this.maxSelectableCustomers) {
        this.yearlyError = `Select up to ${this.maxSelectableCustomers} customers per request.`;
        return;
      }

      this.selectedCustomerIds = [...this.selectedCustomerIds, customerId];
      return;
    }

    this.selectedCustomerIds = this.selectedCustomerIds.filter((selectedId) => selectedId !== customerId);
  }

  selectVisibleCustomers(): void {
    this.clearYearlyResults();
    this.yearlyError = null;

    const nextSelection = [...this.selectedCustomerIds];
    for (const customer of this.filteredCustomers) {
      if (nextSelection.includes(customer.id)) {
        continue;
      }

      if (nextSelection.length >= this.maxSelectableCustomers) {
        this.yearlyError = `Selection reached the ${this.maxSelectableCustomers} customer limit.`;
        break;
      }

      nextSelection.push(customer.id);
    }

    this.selectedCustomerIds = nextSelection;
  }

  clearSelection(): void {
    this.selectedCustomerIds = [];
    this.clearYearlyResults();
    this.yearlyError = null;
  }

  onYearOrProducerChanged(): void {
    this.clearYearlyResults();
    this.yearlyError = null;
  }

  generatePortfolioData(): void {
    this.generatingSynthetic = true;
    this.syntheticError = null;
    this.syntheticResponse = null;

    this.testDataService.generatePortfolioData({
      customerCount: this.syntheticCustomerCount,
      runPipeline: this.syntheticRunPipeline,
    }).subscribe({
      next: (response) => {
        this.syntheticResponse = response.data;
        this.generatingSynthetic = false;
        void this.loadAllCustomers();
      },
      error: (error) => {
        this.syntheticResponse = null;
        this.syntheticError = this.extractError(error, 'Test data generation failed.');
        this.generatingSynthetic = false;
      },
    });
  }

  generateYearlyMeterReads(): void {
    if (!this.canGenerateYearly) {
      return;
    }

    this.generatingYearly = true;
    this.clearYearlyResults();
    this.yearlyError = null;

    this.testDataService.generateYearlyMeterReads({
      customerIds: this.selectedCustomerIds,
      year: this.generationYear,
      producerPercentage: this.producerPercentage,
    }).subscribe({
      next: (response) => {
        this.yearlyResponse = response;
        this.generatingYearly = false;
      },
      error: (error) => {
        this.yearlyResponse = null;
        this.yearlyError = this.extractError(error, 'Yearly meter-read generation failed.');
        this.generatingYearly = false;
      },
    });
  }

  customerLabel(customerId: string): string {
    const customer = this.customers.find((item) => item.id === customerId);
    return customer ? (customer.companyName || customer.name) : customerId;
  }

  private extractError(error: unknown, fallback: string): string {
    if (typeof error !== 'object' || error === null) {
      return fallback;
    }

    const httpError = error as { error?: { error?: string; message?: string } | string };
    if (typeof httpError.error === 'string') {
      return httpError.error;
    }

    return httpError.error?.error || httpError.error?.message || fallback;
  }

  private clearYearlyResults(): void {
    this.yearlyResponse = null;
  }
}
