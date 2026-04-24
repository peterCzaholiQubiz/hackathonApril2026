import { Component, DestroyRef, Input, OnChanges, SimpleChanges, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { BaseChartDirective } from 'ng2-charts';
import { ChartData, ChartOptions, TooltipItem } from 'chart.js';
import { CustomerConsumptionPoint } from '../../../core/models/customer-consumption.model';
import { CustomerService } from '../../../core/services/customer.service';
import { LoadingSkeletonComponent } from '../../../shared/components/loading-skeleton/loading-skeleton.component';

type RangePreset = '6m' | '12m' | '24m' | 'custom';

@Component({
  selector: 'app-customer-consumption-card',
  standalone: true,
  imports: [FormsModule, DecimalPipe, BaseChartDirective, LoadingSkeletonComponent],
  template: `
    <section class="consumption-card">
      <header class="consumption-card__header">
        <div>
          <p class="consumption-card__eyebrow">Consumption</p>
          <h2 class="consumption-card__title">Monthly meter reads</h2>
        </div>
        @if (selectedUnit) {
          <span class="consumption-card__unit">Unit: {{ selectedUnit }}</span>
        }
      </header>

      <div class="consumption-card__controls">
        <div class="consumption-card__presets">
          @for (preset of presets; track preset.value) {
            <button
              type="button"
              class="preset-btn"
              [class.preset-btn--active]="selectedPreset === preset.value"
              (click)="selectPreset(preset.value)">
              {{ preset.label }}
            </button>
          }
        </div>

        <div class="consumption-card__filters">
          <label class="field">
            <span class="field__label">From</span>
            <input class="field__input" type="date" [(ngModel)]="fromDate" (ngModelChange)="selectedPreset = 'custom'" />
          </label>

          <label class="field">
            <span class="field__label">To</span>
            <input class="field__input" type="date" [(ngModel)]="toDate" (ngModelChange)="selectedPreset = 'custom'" />
          </label>

          @if (availableUnits.length > 1) {
            <label class="field field--unit">
              <span class="field__label">Unit</span>
              <select class="field__input" [(ngModel)]="selectedUnit" (ngModelChange)="onUnitChanged()">
                @for (unit of availableUnits; track unit) {
                  <option [value]="unit">{{ unit }}</option>
                }
              </select>
            </label>
          }

          <button type="button" class="apply-btn" (click)="applyCustomRange()">Apply</button>
        </div>
      </div>

      @if (error) {
        <div class="consumption-card__message consumption-card__message--error">{{ error }}</div>
      } @else if (loading) {
        <app-loading-skeleton type="chart" />
      } @else if (points.length === 0) {
        <div class="consumption-card__message">No consumption data in the selected interval.</div>
      } @else {
        <div class="consumption-card__summary">
          <div class="summary-stat">
            <span class="summary-stat__label">Total consumption</span>
            <span class="summary-stat__value">{{ totalConsumption | number:'1.0-2' }} {{ selectedUnitLabel }}</span>
          </div>
          <div class="summary-stat">
            <span class="summary-stat__label">Points</span>
            <span class="summary-stat__value">{{ points.length }}</span>
          </div>
          <div class="summary-stat">
            <span class="summary-stat__label">Interval</span>
            <span class="summary-stat__value">{{ fromDate }} to {{ toDate }}</span>
          </div>
        </div>

        <div class="consumption-card__chart">
          <canvas baseChart [data]="chartData" [options]="chartOptions" [type]="'line'"></canvas>
        </div>
      }
    </section>
  `,
  styles: [`
    .consumption-card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 24px 28px;
      display: flex;
      flex-direction: column;
      gap: 18px;
    }

    .consumption-card__header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
    }

    .consumption-card__eyebrow {
      margin: 0 0 4px;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--color-text-muted);
    }

    .consumption-card__title {
      margin: 0;
      font-size: 22px;
      font-weight: 800;
    }

    .consumption-card__unit {
      display: inline-flex;
      align-items: center;
      min-height: 32px;
      padding: 0 12px;
      border-radius: 999px;
      background: var(--color-surface-2);
      border: 1px solid var(--color-border);
      font-size: 12px;
      font-weight: 700;
      color: var(--color-text-muted);
    }

    .consumption-card__controls {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .consumption-card__presets,
    .consumption-card__filters,
    .consumption-card__summary {
      display: flex;
      flex-wrap: wrap;
      gap: 10px;
    }

    .preset-btn,
    .apply-btn {
      min-height: 38px;
      padding: 0 14px;
      border-radius: var(--radius-sm);
      border: 1px solid var(--color-border);
      background: var(--color-surface-2);
      color: var(--color-text);
      font-size: 13px;
      font-weight: 600;
      cursor: pointer;
      transition: background var(--duration-fast), border-color var(--duration-fast);
    }

    .preset-btn--active {
      background: var(--color-border);
      border-color: var(--color-text-muted);
    }

    .field {
      display: flex;
      flex-direction: column;
      gap: 6px;
      min-width: 160px;
    }

    .field--unit {
      min-width: 110px;
    }

    .field__label {
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--color-text-muted);
    }

    .field__input {
      min-height: 38px;
      padding: 0 12px;
      border-radius: var(--radius-sm);
      border: 1px solid var(--color-border);
      background: var(--color-surface-2);
      color: var(--color-text);
      font-size: 13px;
    }

    .apply-btn {
      align-self: flex-end;
      background: var(--color-text);
      color: var(--color-surface);
    }

    .summary-stat {
      min-width: 140px;
      padding: 12px 14px;
      border-radius: var(--radius-md);
      border: 1px solid var(--color-border);
      background: var(--color-surface-2);
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .summary-stat__label {
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.05em;
      text-transform: uppercase;
      color: var(--color-text-muted);
    }

    .summary-stat__value {
      font-size: 16px;
      font-weight: 700;
    }

    .consumption-card__chart {
      position: relative;
      min-height: 320px;
    }

    .consumption-card__message {
      padding: 32px 16px;
      text-align: center;
      border-radius: var(--radius-md);
      background: var(--color-surface-2);
      color: var(--color-text-muted);
      font-size: 14px;
      font-weight: 600;
    }

    .consumption-card__message--error {
      background: color-mix(in srgb, var(--color-red) 10%, var(--color-surface));
      color: var(--color-red);
    }

    @media (max-width: 900px) {
      .consumption-card {
        padding: 20px;
      }

      .consumption-card__header {
        flex-direction: column;
      }

      .apply-btn {
        align-self: stretch;
      }

      .field {
        min-width: 100%;
      }
    }
  `],
})
export class CustomerConsumptionCardComponent implements OnChanges {
  private readonly customerService = inject(CustomerService);
  private readonly destroyRef = inject(DestroyRef);

  @Input({ required: true }) customerId = '';

  readonly presets: Array<{ value: RangePreset; label: string }> = [
    { value: '6m', label: '6M' },
    { value: '12m', label: '12M' },
    { value: '24m', label: '24M' },
    { value: 'custom', label: 'Custom' },
  ];

  selectedPreset: RangePreset = '12m';
  fromDate = '';
  toDate = '';
  selectedUnit = '';
  availableUnits: string[] = [];
  loading = false;
  error: string | null = null;
  points: CustomerConsumptionPoint[] = [];

  chartData: ChartData<'line'> = { labels: [], datasets: [] };
  chartOptions: ChartOptions<'line'> = this.createChartOptions('');

  constructor() {
    this.setRangeFromPreset('12m');
  }

  get selectedUnitLabel(): string {
    return this.selectedUnit;
  }

  get totalConsumption(): number {
    return this.points.reduce((total, point) => total + point.consumption, 0);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['customerId']?.currentValue) {
      this.loadConsumption();
    }
  }

  selectPreset(preset: RangePreset): void {
    this.selectedPreset = preset;
    if (preset === 'custom') return;

    this.setRangeFromPreset(preset);
    this.loadConsumption();
  }

  applyCustomRange(): void {
    this.selectedPreset = 'custom';
    this.loadConsumption();
  }

  onUnitChanged(): void {
    this.loadConsumption();
  }

  private loadConsumption(): void {
    if (!this.customerId) return;

    if (this.fromDate && this.toDate && this.fromDate > this.toDate) {
      this.error = 'From date must be on or before the to date.';
      this.loading = false;
      this.points = [];
      this.availableUnits = [];
      this.buildChart([]);
      return;
    }

    this.loading = true;
    this.error = null;

    this.customerService.getConsumption(this.customerId, {
      from: this.fromDate || undefined,
      to: this.toDate || undefined,
      unit: this.selectedUnit || undefined,
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => {
        const data = response.data;
        this.availableUnits = data?.availableUnits ?? [];
        this.selectedUnit = data?.selectedUnit ?? '';
        this.points = data?.points ?? [];

        if (data?.from) this.fromDate = data.from;
        if (data?.to) this.toDate = data.to;

        this.buildChart(this.points);
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message ?? 'Failed to load consumption.';
        this.points = [];
        this.availableUnits = [];
        this.buildChart([]);
        this.loading = false;
      },
    });
  }

  private setRangeFromPreset(preset: Exclude<RangePreset, 'custom'>): void {
    const now = new Date();
    const monthOffset = preset === '6m' ? 5 : preset === '24m' ? 23 : 11;
    const from = new Date(now.getFullYear(), now.getMonth() - monthOffset, 1);

    this.fromDate = this.formatInputDate(from);
    this.toDate = this.formatInputDate(now);
  }

  private buildChart(points: CustomerConsumptionPoint[]): void {
    this.chartData = {
      labels: points.map((point) => this.formatMonth(point.month)),
      datasets: [
        {
          label: 'Consumption',
          data: points.map((point) => point.consumption),
          borderColor: '#0f766e',
          backgroundColor: 'rgba(15, 118, 110, 0.16)',
          pointBackgroundColor: '#115e59',
          pointBorderColor: '#ffffff',
          pointHoverBackgroundColor: '#0f766e',
          pointHoverBorderColor: '#ffffff',
          pointRadius: 4,
          pointHoverRadius: 6,
          borderWidth: 3,
          tension: 0.25,
          fill: true,
        },
      ],
    };

    this.chartOptions = this.createChartOptions(this.selectedUnitLabel);
  }

  private createChartOptions(unitLabel: string): ChartOptions<'line'> {
    return {
      responsive: true,
      maintainAspectRatio: false,
      interaction: {
        mode: 'nearest',
        intersect: false,
      },
      plugins: {
        legend: {
          display: false,
        },
        tooltip: {
          callbacks: {
            label: (item) => this.buildTooltipLabel(item),
            afterBody: (items) => this.buildTooltipAfterBody(items),
          },
        },
      },
      scales: {
        x: {
          grid: {
            display: false,
          },
        },
        y: {
          beginAtZero: true,
          title: {
            display: true,
            text: unitLabel ? `Consumption (${unitLabel})` : 'Consumption',
          },
        },
      },
    };
  }

  private buildTooltipLabel(item: TooltipItem<'line'>): string {
    const point = this.points[item.dataIndex];
    if (!point) return '';

    return `Consumption: ${point.consumption.toFixed(2)} ${point.unit}`;
  }

  private buildTooltipAfterBody(items: TooltipItem<'line'>[]): string[] {
    const point = this.points[items[0]?.dataIndex ?? -1];
    if (!point) return [];

    const lines = [`Quality: ${point.quality}`];
    if (point.quality === 'Mixed') {
      lines.push(
        ...point.qualityBreakdown.map((entry) =>
          `${entry.quality}: ${entry.consumption.toFixed(2)} ${point.unit} (${entry.readCount} reads)`
        )
      );
    }

    return lines;
  }

  private formatMonth(value: string): string {
    const [year, month] = value.split('-').map(Number);
    return new Date(year, month - 1, 1).toLocaleDateString(undefined, {
      month: 'short',
      year: 'numeric',
    });
  }

  private formatInputDate(value: Date): string {
    const year = value.getFullYear();
    const month = `${value.getMonth() + 1}`.padStart(2, '0');
    const day = `${value.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}