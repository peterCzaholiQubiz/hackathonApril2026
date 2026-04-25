import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BaseChartDirective } from 'ng2-charts';
import type { ChartData, ChartOptions } from 'chart.js';
import { RiskService, CustomerScatterPoint } from '../../../core/services/risk.service';
import { LoadingSkeletonComponent } from '../../../shared/components/loading-skeleton/loading-skeleton.component';

type Dimension = 'churn' | 'payment' | 'margin' | 'overall';

const DIMENSION_LABELS: Record<Dimension, string> = {
  churn: 'Churn',
  payment: 'Payment',
  margin: 'Margin',
  overall: 'Overall',
};

const MIN_R = 5;
const MAX_R = 14;

@Component({
  selector: 'app-all-customer-heatmap',
  standalone: true,
  imports: [BaseChartDirective, LoadingSkeletonComponent],
  template: `
    <div class="scatter">
      <div class="scatter__header">
        <h2 class="scatter__title">All Customers — Risk Scatter</h2>
        <div class="scatter__controls">
          <div class="axis-selector">
            <span class="axis-selector__label">X</span>
            @for (d of dimensions; track d) {
              <button class="axis-btn" [class.axis-btn--active]="xDim === d" (click)="setX(d)">
                {{ dimensionLabel(d) }}
              </button>
            }
          </div>
          <div class="axis-selector">
            <span class="axis-selector__label">Y</span>
            @for (d of dimensions; track d) {
              <button class="axis-btn" [class.axis-btn--active]="yDim === d" (click)="setY(d)">
                {{ dimensionLabel(d) }}
              </button>
            }
          </div>
          <button class="size-toggle" [class.size-toggle--active]="sizeByValue" (click)="toggleSize()">
            Size by contract value
          </button>
        </div>
      </div>

      @if (loading) {
        <app-loading-skeleton type="chart" />
      } @else if (points.length === 0) {
        <p class="scatter__empty">No risk scores available yet. Trigger a risk scoring run to populate this chart.</p>
      } @else {
        <div class="scatter__chart-wrap">
          <canvas baseChart
            [data]="chartData"
            [options]="chartOptions"
            type="bubble">
          </canvas>
        </div>
        <div class="scatter__legend">
          <span class="legend-dot legend-dot--green"></span><span class="legend-label">Healthy</span>
          <span class="legend-dot legend-dot--yellow"></span><span class="legend-label">Watch</span>
          <span class="legend-dot legend-dot--red"></span><span class="legend-label">At Risk</span>
          <span class="legend-count">{{ points.length }} customers</span>
        </div>
      }
    </div>
  `,
  styles: [`
    .scatter {
      &__header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        flex-wrap: wrap;
        gap: 12px;
        margin-bottom: 16px;
      }

      &__title {
        font-size: 13px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
      }

      &__controls {
        display: flex;
        align-items: center;
        gap: 16px;
        flex-wrap: wrap;
      }

      &__chart-wrap {
        position: relative;
        height: 320px;
      }

      &__legend {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-top: 12px;
        font-size: 12px;
      }

      &__empty {
        font-size: 13px;
        color: var(--color-text-muted);
        text-align: center;
        padding: 40px 0;
      }
    }

    .axis-selector {
      display: flex;
      align-items: center;
      gap: 4px;

      &__label {
        font-size: 11px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--color-text-muted);
        margin-right: 4px;
        min-width: 12px;
      }
    }

    .axis-btn {
      font-size: 11px;
      font-weight: 600;
      padding: 3px 8px;
      border-radius: var(--radius-sm, 4px);
      border: 1px solid var(--color-border);
      background: transparent;
      color: var(--color-text-muted);
      cursor: pointer;
      transition: all var(--duration-fast, 150ms);

      &--active {
        background: var(--color-primary, #6366f1);
        border-color: var(--color-primary, #6366f1);
        color: #fff;
      }

      &:hover:not(.axis-btn--active) {
        border-color: var(--color-primary, #6366f1);
        color: var(--color-text);
      }
    }

    .size-toggle {
      font-size: 11px;
      font-weight: 600;
      padding: 3px 8px;
      border-radius: var(--radius-sm, 4px);
      border: 1px solid var(--color-border);
      background: transparent;
      color: var(--color-text-muted);
      cursor: pointer;
      transition: all var(--duration-fast, 150ms);

      &--active {
        background: var(--color-surface-2, #f1f5f9);
        border-color: var(--color-primary, #6366f1);
        color: var(--color-text);
      }
    }

    .legend-dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      display: inline-block;

      &--green  { background: #22c55e; }
      &--yellow { background: #f59e0b; }
      &--red    { background: #ef4444; }
    }

    .legend-label {
      color: var(--color-text-muted);
      margin-right: 8px;
    }

    .legend-count {
      margin-left: auto;
      font-size: 11px;
      color: var(--color-text-muted);
    }
  `],
})
export class AllCustomerHeatmapComponent implements OnInit {
  private readonly riskSvc = inject(RiskService);
  private readonly destroyRef = inject(DestroyRef);

  readonly dimensions: Dimension[] = ['churn', 'payment', 'margin', 'overall'];

  loading = true;
  points: CustomerScatterPoint[] = [];
  xDim: Dimension = 'churn';
  yDim: Dimension = 'payment';
  sizeByValue = true;

  chartData: ChartData<'bubble'> = { datasets: [] };

  readonly chartOptions: ChartOptions<'bubble'> = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: {
        min: 0,
        max: 100,
        ticks: { stepSize: 20, color: 'var(--color-text-muted)' },
        grid: { color: 'var(--color-border)' },
        title: { display: true, text: '', color: 'var(--color-text-muted)', font: { size: 11, weight: 'bold' } },
      },
      y: {
        min: 0,
        max: 100,
        ticks: { stepSize: 20, color: 'var(--color-text-muted)' },
        grid: { color: 'var(--color-border)' },
        title: { display: true, text: '', color: 'var(--color-text-muted)', font: { size: 11, weight: 'bold' } },
      },
    },
    plugins: {
      legend: { display: false },
      tooltip: {
        callbacks: {
          label: (ctx) => {
            const p = this.points.find(
              pt => this.score(pt, this.xDim) === ctx.parsed.x &&
                    this.score(pt, this.yDim) === ctx.parsed.y
            );
            if (!p) return '';
            return [
              ` ${p.name}${p.segment ? ` (${p.segment})` : ''}`,
              ` Churn: ${p.churnScore}  Payment: ${p.paymentScore}  Margin: ${p.marginScore}`,
            ];
          },
        },
      },
    },
  };

  ngOnInit(): void {
    this.riskSvc.getScatterData()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.points = res.data ?? [];
          this.buildChart();
          this.loading = false;
        },
        error: () => { this.loading = false; },
      });
  }

  dimensionLabel(d: Dimension): string {
    return DIMENSION_LABELS[d];
  }

  setX(d: Dimension): void {
    this.xDim = d;
    this.buildChart();
  }

  setY(d: Dimension): void {
    this.yDim = d;
    this.buildChart();
  }

  toggleSize(): void {
    this.sizeByValue = !this.sizeByValue;
    this.buildChart();
  }

  private score(p: CustomerScatterPoint, dim: Dimension): number {
    switch (dim) {
      case 'churn':   return p.churnScore;
      case 'payment': return p.paymentScore;
      case 'margin':  return p.marginScore;
      default:        return p.overallScore;
    }
  }

  private normalizedRadius(value: number, maxValue: number): number {
    if (!this.sizeByValue || maxValue === 0) return MIN_R;
    return MIN_R + ((value / maxValue) * (MAX_R - MIN_R));
  }

  private buildChart(): void {
    const maxValue = this.sizeByValue
      ? Math.max(...this.points.map(p => p.monthlyContractValue), 1)
      : 1;

    const green  = this.points.filter(p => p.heatLevel === 'green');
    const yellow = this.points.filter(p => p.heatLevel === 'yellow');
    const red    = this.points.filter(p => p.heatLevel === 'red');

    const toDataset = (pts: CustomerScatterPoint[], color: string, label: string) => ({
      label,
      data: pts.map(p => ({
        x: this.score(p, this.xDim),
        y: this.score(p, this.yDim),
        r: this.normalizedRadius(p.monthlyContractValue, maxValue),
      })),
      backgroundColor: color + 'b3',
      borderColor: color,
      borderWidth: 1,
    });

    this.chartData = {
      datasets: [
        toDataset(green,  '#22c55e', 'Healthy'),
        toDataset(yellow, '#f59e0b', 'Watch'),
        toDataset(red,    '#ef4444', 'At Risk'),
      ],
    };

    if (this.chartOptions.scales?.['x']?.title) {
      (this.chartOptions.scales['x'].title as { text: string }).text = DIMENSION_LABELS[this.xDim] + ' Score';
    }
    if (this.chartOptions.scales?.['y']?.title) {
      (this.chartOptions.scales['y'].title as { text: string }).text = DIMENSION_LABELS[this.yDim] + ' Score';
    }
  }
}
