import { Component, Input, OnChanges } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import type { ChartData, ChartOptions } from 'chart.js';
import { PortfolioSnapshot } from '../../../core/models/portfolio-snapshot.model';

@Component({
  selector: 'app-portfolio-heatmap',
  standalone: true,
  imports: [BaseChartDirective, DecimalPipe],
  template: `
    <div class="heatmap">
      <h2 class="heatmap__title">Portfolio Health</h2>
      <div class="heatmap__chart-wrap">
        <canvas baseChart
          [data]="chartData"
          [options]="chartOptions"
          type="doughnut">
        </canvas>
        <div class="heatmap__center">
          <div class="heatmap__total">{{ snapshot.totalCustomers }}</div>
          <div class="heatmap__total-label">customers</div>
        </div>
      </div>
      <div class="heatmap__legend">
        <div class="legend-item legend-item--green">
          <span class="legend-dot"></span>
          <span class="legend-label">Healthy</span>
          <span class="legend-count">{{ snapshot.greenCount }}</span>
          <span class="legend-pct">({{ snapshot.greenPct | number:'1.0-0' }}%)</span>
        </div>
        <div class="legend-item legend-item--yellow">
          <span class="legend-dot"></span>
          <span class="legend-label">Watch</span>
          <span class="legend-count">{{ snapshot.yellowCount }}</span>
          <span class="legend-pct">({{ snapshot.yellowPct | number:'1.0-0' }}%)</span>
        </div>
        <div class="legend-item legend-item--red">
          <span class="legend-dot"></span>
          <span class="legend-label">At Risk</span>
          <span class="legend-count">{{ snapshot.redCount }}</span>
          <span class="legend-pct">({{ snapshot.redPct | number:'1.0-0' }}%)</span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .heatmap {
      &__title {
        font-size: 13px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
        margin-bottom: 16px;
      }

      &__chart-wrap {
        position: relative;
        width: 200px;
        height: 200px;
        margin: 0 auto 16px;
      }

      &__center {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
        text-align: center;
        pointer-events: none;
      }

      &__total {
        font-size: 36px;
        font-weight: 800;
        color: var(--color-text);
        line-height: 1;
      }

      &__total-label {
        font-size: 11px;
        font-weight: 600;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--color-text-muted);
        margin-top: 2px;
      }

      &__legend {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }
    }

    .legend-item {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 13px;
    }

    .legend-dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      flex-shrink: 0;
    }

    .legend-item--green .legend-dot  { background: var(--color-green); }
    .legend-item--yellow .legend-dot { background: var(--color-yellow); }
    .legend-item--red .legend-dot    { background: var(--color-red); }

    .legend-label {
      flex: 1;
      color: var(--color-text-muted);
    }

    .legend-count {
      font-weight: 700;
      color: var(--color-text);
    }

    .legend-pct {
      color: var(--color-text-muted);
      font-size: 12px;
    }
  `],
})
export class PortfolioHeatmapComponent implements OnChanges {
  @Input() snapshot!: PortfolioSnapshot;

  chartData: ChartData<'doughnut'> = { datasets: [] };

  readonly chartOptions: ChartOptions<'doughnut'> = {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '72%',
    plugins: {
      legend: { display: false },
      tooltip: {
        callbacks: {
          label: (ctx) => ` ${ctx.label}: ${ctx.parsed} customers`,
        },
      },
    },
  };

  ngOnChanges(): void {
    this.chartData = {
      labels: ['Healthy', 'Watch', 'At Risk'],
      datasets: [{
        data: [this.snapshot.greenCount, this.snapshot.yellowCount, this.snapshot.redCount],
        backgroundColor: ['#22c55e', '#f59e0b', '#ef4444'],
        borderWidth: 0,
        hoverOffset: 6,
      }],
    };
  }
}
