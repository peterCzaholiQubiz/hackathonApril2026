import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { DatePipe, DecimalPipe } from '@angular/common';
import { PortfolioService } from '../../core/services/portfolio.service';
import { RiskService, TopAtRiskItem } from '../../core/services/risk.service';
import { PortfolioSnapshot } from '../../core/models/portfolio-snapshot.model';
import { LoadingSkeletonComponent } from '../../shared/components/loading-skeleton/loading-skeleton.component';
import { PortfolioHeatmapComponent } from './portfolio-heatmap/portfolio-heatmap.component';
import { SegmentBreakdownComponent } from './segment-breakdown/segment-breakdown.component';
import { TopAtRiskComponent } from './top-at-risk/top-at-risk.component';
import { AllCustomerHeatmapComponent } from './all-customer-heatmap/all-customer-heatmap.component';
import { EnergyHeatmapComponent } from './energy-heatmap/energy-heatmap.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    LoadingSkeletonComponent,
    PortfolioHeatmapComponent,
    SegmentBreakdownComponent,
    TopAtRiskComponent,
    AllCustomerHeatmapComponent,
    EnergyHeatmapComponent,
  ],
  template: `
    @if (loading) {
      <div class="dashboard">
        <div class="dashboard__header">
          <app-loading-skeleton type="text" />
        </div>
        <div class="dashboard__grid">
          <div class="card"><app-loading-skeleton type="chart" /></div>
          <div class="card"><app-loading-skeleton type="chart" /></div>
          <div class="card" style="grid-column:span 2"><app-loading-skeleton type="table" /></div>
        </div>
      </div>
    } @else if (error) {
      <div class="dashboard__error">{{ error }}</div>
    } @else if (snapshot) {
      <div class="dashboard">
        <header class="dashboard__header">
          <div>
            <h1 class="dashboard__title">Portfolio Overview</h1>
            <p class="dashboard__subtitle">{{ snapshot.totalCustomers }} active customers · {{ snapshot.createdAt | date:'medium' }}</p>
          </div>
          <div class="dashboard__avg-scores">
            <div class="avg-score">
              <span class="avg-score__label">Avg Churn</span>
              <span class="avg-score__value" style="color:var(--color-red)">{{ snapshot.avgChurnScore | number:'1.0-0' }}</span>
            </div>
            <div class="avg-score">
              <span class="avg-score__label">Avg Payment</span>
              <span class="avg-score__value" style="color:var(--color-yellow)">{{ snapshot.avgPaymentScore | number:'1.0-0' }}</span>
            </div>
            <div class="avg-score">
              <span class="avg-score__label">Avg Margin</span>
              <span class="avg-score__value" style="color:var(--color-green)">{{ snapshot.avgMarginScore | number:'1.0-0' }}</span>
            </div>
          </div>
        </header>

        <div class="dashboard__grid">
          <div class="card">
            <app-portfolio-heatmap [snapshot]="snapshot" />
          </div>
          <div class="card">
            <app-segment-breakdown [snapshot]="snapshot" />
          </div>
          <div class="card dashboard__full">
            <app-top-at-risk [customers]="topAtRisk" />
          </div>
          <div class="card dashboard__full">
            <app-all-customer-heatmap />
          </div>
          <div class="card">
            <app-energy-heatmap direction="Consumption" colorScheme="blue" />
          </div>
          <div class="card">
            <app-energy-heatmap direction="Production" colorScheme="green" />
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .dashboard {
      &__header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        margin-bottom: 24px;
        gap: 16px;
        flex-wrap: wrap;
      }

      &__title {
        font-size: 28px;
        font-weight: 800;
        color: var(--color-text);
        margin-bottom: 4px;
      }

      &__subtitle {
        font-size: 13px;
        color: var(--color-text-muted);
      }

      &__avg-scores {
        display: flex;
        gap: 24px;
        align-items: center;
      }

      &__divider {
        width: 1px;
        height: 40px;
        background: var(--color-border);
        margin: 0 4px;
      }

      &__grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 20px;
      }

      &__full {
        grid-column: span 2;
      }

      &__error {
        color: var(--color-red);
        padding: 24px;
        text-align: center;
      }
    }

    .card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 24px;
    }

    .avg-score {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 2px;
      min-width: 72px;

      &__label {
        font-size: 10px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
      }

      &__value {
        font-size: 32px;
        font-weight: 800;
        line-height: 1;
      }
    }
  `],
})
export class DashboardComponent implements OnInit {
  private readonly portfolioSvc = inject(PortfolioService);
  private readonly riskSvc = inject(RiskService);
  private readonly destroyRef = inject(DestroyRef);

  loading = true;
  error: string | null = null;
  snapshot: PortfolioSnapshot | null = null;
  topAtRisk: TopAtRiskItem[] = [];

  ngOnInit(): void {
    forkJoin({
      current: this.portfolioSvc.getCurrent(),
      topAtRisk: this.riskSvc.getTopAtRisk('overall', 10),
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ current, topAtRisk }) => {
        this.snapshot = current.data;
        this.topAtRisk = topAtRisk.data ?? [];
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message ?? 'Failed to load dashboard data.';
        this.loading = false;
      },
    });
  }
}
