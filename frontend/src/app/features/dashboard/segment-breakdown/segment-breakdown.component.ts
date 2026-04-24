import { Component, Input, OnChanges } from '@angular/core';
import { BaseChartDirective } from 'ng2-charts';
import type { ChartData, ChartOptions } from 'chart.js';
import { PortfolioSnapshot } from '../../../core/models/portfolio-snapshot.model';

@Component({
  selector: 'app-segment-breakdown',
  standalone: true,
  imports: [BaseChartDirective],
  template: `
    <div class="segment">
      <h2 class="segment__title">Segment Breakdown</h2>
      @if (hasData) {
        <div class="segment__chart">
          <canvas baseChart
            [data]="chartData"
            [options]="chartOptions"
            type="bar">
          </canvas>
        </div>
      } @else {
        <p class="segment__empty">No segment data available.</p>
      }
    </div>
  `,
  styles: [`
    .segment {
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
export class SegmentBreakdownComponent implements OnChanges {
  @Input() snapshot!: PortfolioSnapshot;

  chartData: ChartData<'bar'> = { datasets: [] };
  hasData = false;

  readonly chartOptions: ChartOptions<'bar'> = {
    responsive: true,
    maintainAspectRatio: false,
    indexAxis: 'y',
    scales: {
      x: {
        stacked: true,
        grid: { color: 'rgba(255,255,255,0.05)' },
        ticks: { color: '#94a3b8', stepSize: 1 },
      },
      y: {
        stacked: true,
        grid: { display: false },
        ticks: { color: '#94a3b8' },
      },
    },
    plugins: {
      legend: {
        position: 'bottom',
        labels: { color: '#94a3b8', boxWidth: 12, padding: 16 },
      },
    },
  };

  ngOnChanges(): void {
    const bd = this.parseBreakdown();
    const labels = Object.keys(bd);
    this.hasData = labels.length > 0;

    this.chartData = {
      labels: labels.map(l => l.charAt(0).toUpperCase() + l.slice(1)),
      datasets: [
        {
          label: 'Healthy',
          data: labels.map(l => bd[l].green ?? 0),
          backgroundColor: '#22c55e',
        },
        {
          label: 'Watch',
          data: labels.map(l => bd[l].yellow ?? 0),
          backgroundColor: '#f59e0b',
        },
        {
          label: 'At Risk',
          data: labels.map(l => bd[l].red ?? 0),
          backgroundColor: '#ef4444',
        },
      ],
    };
  }

  private parseBreakdown(): Record<string, { green: number; yellow: number; red: number }> {
    if (!this.snapshot?.segmentBreakdown) return {};
    if (typeof this.snapshot.segmentBreakdown === 'string') {
      try { return JSON.parse(this.snapshot.segmentBreakdown); } catch { return {}; }
    }
    return this.snapshot.segmentBreakdown as unknown as Record<string, { green: number; yellow: number; red: number }>;
  }
}
