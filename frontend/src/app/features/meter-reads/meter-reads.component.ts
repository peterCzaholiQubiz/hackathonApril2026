import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import {
  Chart,
  ChartData,
  ChartOptions,
  registerables,
} from 'chart.js';
import { MeterReadService } from '../../core/services/meter-read.service';
import { CustomerService } from '../../core/services/customer.service';
import { LoadingSkeletonComponent } from '../../shared/components/loading-skeleton/loading-skeleton.component';
import {
  ConsumptionProfile,
  DailyMeterReadSummary,
  GenerateMeterReadsResponse,
  GenerationPeriod,
} from '../../core/models/meter-read.model';
import { Customer } from '../../core/models/customer.model';

Chart.register(...registerables);

@Component({
  selector: 'app-meter-reads',
  standalone: true,
  imports: [FormsModule, DecimalPipe, BaseChartDirective, LoadingSkeletonComponent],
  template: `
    <div class="meter-reads">
      <header class="meter-reads__header">
        <h1 class="meter-reads__title">Meter Read Simulator</h1>
        <p class="meter-reads__subtitle">Generate hourly consumption data for a customer connection profile</p>
      </header>

      <div class="card meter-reads__controls">
        <div class="controls-row">
          <div class="control-group">
            <label class="control-label">Customer</label>
            <select class="control-select" [(ngModel)]="selectedCustomerId" [disabled]="loadingCustomers">
              <option value="">— select customer —</option>
              @for (c of customers; track c.id) {
                <option [value]="c.id">{{ c.companyName || c.name }}</option>
              }
            </select>
          </div>

          <div class="control-group">
            <label class="control-label">Consumption Profile</label>
            <select class="control-select" [(ngModel)]="selectedProfile">
              @for (p of meterReadService.profileOptions; track p.value) {
                <option [value]="p.value">{{ p.label }}</option>
              }
            </select>
            <span class="control-hint">{{ profileDescription }}</span>
          </div>

          <div class="control-group">
            <label class="control-label">Period</label>
            <select class="control-select" [(ngModel)]="selectedPeriod">
              @for (p of meterReadService.periodOptions; track p.value) {
                <option [value]="p.value">{{ p.label }}</option>
              }
            </select>
          </div>

          <div class="control-group control-group--action">
            <button
              class="btn btn--primary"
              (click)="generate()"
              [disabled]="!selectedCustomerId || generating">
              @if (generating) { Generating… } @else { Generate }
            </button>
          </div>
        </div>
      </div>

      @if (error) {
        <div class="meter-reads__error">{{ error }}</div>
      }

      @if (generating && !response) {
        <div class="card">
          <app-loading-skeleton type="chart" />
        </div>
      }

      @if (response) {
        <div class="card meter-reads__chart-card">
          <canvas baseChart
            [data]="chartData"
            [options]="chartOptions"
            [type]="'bar'"
            style="max-height: 380px">
          </canvas>
        </div>

        <div class="meter-reads__summary">
          <div class="stat-card">
            <span class="stat-card__label">Total kWh</span>
            <span class="stat-card__value">{{ totalConsumption | number:'1.0-0' }}</span>
          </div>
          <div class="stat-card">
            <span class="stat-card__label">Daily Avg</span>
            <span class="stat-card__value">{{ dailyAvg | number:'1.1-1' }}</span>
          </div>
          <div class="stat-card">
            <span class="stat-card__label">Peak Share</span>
            <span class="stat-card__value">{{ peakShare | number:'1.0-0' }}%</span>
          </div>
          @if (response.profile === 'SolarProducer') {
            <div class="stat-card">
              <span class="stat-card__label">Net Consumption</span>
              <span class="stat-card__value">{{ netConsumption | number:'1.0-0' }} kWh</span>
            </div>
          }
        </div>

        <p class="meter-reads__row-count">
          {{ response.totalHourlyRowsGenerated | number }} hourly rows stored in database
        </p>
      }
    </div>
  `,
  styles: [`
    .meter-reads {
      padding: 2rem;
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .meter-reads__header { display: flex; flex-direction: column; gap: .25rem; }
    .meter-reads__title { font-size: 1.5rem; font-weight: 700; margin: 0; }
    .meter-reads__subtitle { margin: 0; color: var(--color-text-muted, #6b7280); font-size: .875rem; }
    .meter-reads__controls { padding: 1.25rem 1.5rem; }
    .controls-row { display: flex; flex-wrap: wrap; gap: 1rem; align-items: flex-end; }
    .control-group { display: flex; flex-direction: column; gap: .375rem; min-width: 180px; }
    .control-group--action { justify-content: flex-end; }
    .control-label { font-size: .75rem; font-weight: 600; text-transform: uppercase; letter-spacing: .05em; color: var(--color-text-muted, #6b7280); }
    .control-select { padding: .5rem .75rem; border: 1px solid var(--color-border, #d1d5db); border-radius: .375rem; background: var(--color-surface, #fff); font-size: .875rem; }
    .control-hint { font-size: .75rem; color: var(--color-text-muted, #6b7280); min-height: 1rem; }
    .btn { padding: .5rem 1.25rem; border: none; border-radius: .375rem; font-size: .875rem; font-weight: 600; cursor: pointer; }
    .btn--primary { background: var(--color-accent, #3b82f6); color: #fff; }
    .btn--primary:disabled { opacity: .5; cursor: not-allowed; }
    .meter-reads__error { padding: .75rem 1rem; background: #fef2f2; color: #dc2626; border-radius: .375rem; font-size: .875rem; }
    .meter-reads__chart-card { padding: 1.5rem; }
    .meter-reads__summary { display: flex; flex-wrap: wrap; gap: 1rem; }
    .stat-card { flex: 1; min-width: 140px; padding: 1rem 1.25rem; background: var(--color-surface, #fff); border: 1px solid var(--color-border, #e5e7eb); border-radius: .5rem; display: flex; flex-direction: column; gap: .25rem; }
    .stat-card__label { font-size: .75rem; font-weight: 600; text-transform: uppercase; letter-spacing: .05em; color: var(--color-text-muted, #6b7280); }
    .stat-card__value { font-size: 1.5rem; font-weight: 700; }
    .meter-reads__row-count { font-size: .75rem; color: var(--color-text-muted, #6b7280); margin: 0; }
  `],
})
export class MeterReadsComponent implements OnInit {
  readonly meterReadService = inject(MeterReadService);
  private readonly customerService = inject(CustomerService);

  customers: Customer[] = [];
  loadingCustomers = false;

  selectedCustomerId = '';
  selectedProfile: ConsumptionProfile = 'LowConsumer';
  selectedPeriod: GenerationPeriod = 'OneYear';

  generating = false;
  error: string | null = null;
  response: GenerateMeterReadsResponse | null = null;

  chartData: ChartData<'bar'> = { labels: [], datasets: [] };
  chartOptions: ChartOptions<'bar'> = {
    responsive: true,
    plugins: {
      legend: { position: 'top' },
      tooltip: { mode: 'index', intersect: false },
    },
    scales: {
      x: { stacked: true, ticks: { maxTicksLimit: 30, maxRotation: 45 } },
      y: { stacked: true, title: { display: true, text: 'kWh' } },
    },
  };

  get profileDescription(): string {
    return this.meterReadService.profileOptions.find(p => p.value === this.selectedProfile)?.description ?? '';
  }

  get totalConsumption(): number {
    return this.response?.dailySummary.reduce((s, d) => s + d.totalConsumption, 0) ?? 0;
  }

  get dailyAvg(): number {
    const days = this.response?.dailySummary.length ?? 0;
    return days > 0 ? this.totalConsumption / days : 0;
  }

  get peakShare(): number {
    if (!this.response) return 0;
    const totalHigh = this.response.dailySummary.reduce((s, d) => s + d.consumptionHigh, 0);
    return this.totalConsumption > 0 ? (totalHigh / this.totalConsumption) * 100 : 0;
  }

  get netConsumption(): number {
    if (!this.response) return 0;
    const totalProd = this.response.dailySummary.reduce((s, d) => s + d.production, 0);
    return this.totalConsumption - totalProd;
  }

  ngOnInit(): void {
    this.loadingCustomers = true;
    this.customerService.getList({ pageSize: 200 }).subscribe({
      next: r => {
        this.customers = r.data ?? [];
        this.loadingCustomers = false;
      },
      error: () => { this.loadingCustomers = false; },
    });
  }

  generate(): void {
    if (!this.selectedCustomerId || this.generating) return;

    this.generating = true;
    this.error = null;

    this.meterReadService.generate({
      customerId: this.selectedCustomerId,
      profile: this.selectedProfile,
      period: this.selectedPeriod,
    }).subscribe({
      next: r => {
        this.response = r;
        this.buildChart(r.dailySummary);
        this.generating = false;
      },
      error: err => {
        this.error = err?.error?.message ?? 'Generation failed. Please try again.';
        this.generating = false;
      },
    });
  }

  private buildChart(days: DailyMeterReadSummary[]): void {
    const labels = days.map(d => {
      const date = new Date(d.date);
      return `${date.toLocaleString('default', { month: 'short' })} ${date.getDate()}`;
    });

    const datasets: ChartData<'bar'>['datasets'] = [
      {
        label: 'Peak (UsageHigh)',
        data: days.map(d => d.consumptionHigh),
        backgroundColor: '#3b82f6',
        stack: 'consumption',
      },
      {
        label: 'Off-Peak (UsageLow)',
        data: days.map(d => d.consumptionLow),
        backgroundColor: '#14b8a6',
        stack: 'consumption',
      },
    ];

    if (this.selectedProfile === 'SolarProducer') {
      datasets.push({
        label: 'Production',
        data: days.map(d => d.production),
        backgroundColor: '#f59e0b',
        stack: 'production',
      });
    }

    this.chartData = { labels, datasets };
  }
}
