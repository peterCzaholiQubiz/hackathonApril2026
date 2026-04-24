import { Component, Input, OnChanges } from '@angular/core';
import { BaseChartDirective } from 'ng2-charts';
import type { ChartData, ChartOptions } from 'chart.js';
import { PortfolioSnapshot } from '../../../core/models/portfolio-snapshot.model';

@Component({
  selector: 'app-risk-trend',
  standalone: true,
  imports: [BaseChartDirective],
  template: `
    <div class="trend">
      <h2 class="trend__title">Risk Trend</h2>
      @if (hasEnoughData) {
        <div class="trend__chart">
          <canvas baseChart
            [data]="chartData"
            [options]="chartOptions"
            type="line">
          </canvas>
        </div>
      } @else {
        <p class="trend__empty">At least two snapshots are needed to show a trend.</p>
      }
    </div>
  `,
  styles: [`
    .trend {
      &__title {
        font-size: 13px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
        margin-bottom: 16px;
      }

      &__chart {
        height: 200px;
        position: relative;
      }

      &__empty {
        color: var(--color-text-muted);
        font-size: 13px;
        padding: 24px 0;
        text-align: center;
      }
    }
  `],
})
export class RiskTrendComponent implements OnChanges {
  @Input() history: PortfolioSnapshot[] = [];

  chartData: ChartData<'line'> = { datasets: [] };
  hasEnoughData = false;

  readonly chartOptions: ChartOptions<'line'> = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: {
        grid: { color: 'rgba(255,255,255,0.05)' },
        ticks: { color: '#94a3b8', maxTicksLimit: 6 },
      },
      y: {
        min: 0,
        max: 100,
        grid: { color: 'rgba(255,255,255,0.05)' },
        ticks: { color: '#94a3b8' },
      },
    },
    plugins: {
      legend: {
        position: 'bottom',
        labels: { color: '#94a3b8', boxWidth: 12, padding: 16 },
      },
    },
    elements: {
      point: { radius: 3 },
      line: { tension: 0.3 },
    },
  };

  ngOnChanges(): void {
    const sorted = [...this.history].sort(
      (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
    );
    this.hasEnoughData = sorted.length >= 2;

    const labels = sorted.map(s =>
      new Date(s.createdAt).toLocaleDateString('en-GB', { month: 'short', day: 'numeric' })
    );

    this.chartData = {
      labels,
      datasets: [
        {
          label: 'Churn',
          data: sorted.map(s => Number(s.avgChurnScore)),
          borderColor: '#ef4444',
          backgroundColor: 'rgba(239,68,68,0.1)',
          fill: false,
        },
        {
          label: 'Payment',
          data: sorted.map(s => Number(s.avgPaymentScore)),
          borderColor: '#f59e0b',
          backgroundColor: 'rgba(245,158,11,0.1)',
          fill: false,
        },
        {
          label: 'Margin',
          data: sorted.map(s => Number(s.avgMarginScore)),
          borderColor: '#22c55e',
          backgroundColor: 'rgba(34,197,94,0.1)',
          fill: false,
        },
      ],
    };
  }
}
