import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { RiskService, TopAtRiskItem } from '../../core/services/risk.service';
import { HeatBadgeComponent } from '../../shared/components/heat-badge/heat-badge.component';
import { ScoreBarComponent } from '../../shared/components/score-bar/score-bar.component';
import { LoadingSkeletonComponent } from '../../shared/components/loading-skeleton/loading-skeleton.component';

interface RiskColumn {
  label: string;
  dim: 'churn' | 'payment' | 'margin';
  items: TopAtRiskItem[];
  avg: number;
}

@Component({
  selector: 'app-risk-groups',
  standalone: true,
  imports: [RouterLink, DecimalPipe, HeatBadgeComponent, ScoreBarComponent, LoadingSkeletonComponent],
  template: `
    <div class="groups-page">
      <header class="groups-page__header">
        <h1 class="groups-page__title">Risk Groups</h1>
        <p class="groups-page__sub">Top 10 customers by risk dimension</p>
      </header>

      @if (loading) {
        <div class="columns">
          @for (_ of [1,2,3]; track $index) {
            <div class="col card"><app-loading-skeleton type="table" /></div>
          }
        </div>
      } @else if (error) {
        <div class="error-msg">{{ error }}</div>
      } @else {
        <div class="columns">
          @for (col of columns; track col.dim) {
            <div class="col card">
              <div class="col__header">
                <h2 class="col__title">{{ col.label }}</h2>
                <span class="col__avg" [class.col__avg--red]="col.avg >= 70" [class.col__avg--yellow]="col.avg >= 40 && col.avg < 70">
                  avg {{ col.avg | number:'1.0-0' }}
                </span>
              </div>
              <div class="col__list">
                @if (col.items.length === 0) {
                  <p class="col__empty">No data available.</p>
                }
                @for (item of col.items; track item.customerId; let i = $index) {
                  <div class="row" [routerLink]="['/customers', item.customerId]">
                    <span class="row__rank">{{ i + 1 }}</span>
                    <app-heat-badge [heatLevel]="item.heatLevel" />
                    <div class="row__info">
                      <span class="row__name">{{ item.name }}</span>
                      @if (item.companyName) {
                        <span class="row__company">{{ item.companyName }}</span>
                      }
                    </div>
                    <div class="row__score">
                      <app-score-bar [score]="dimScore(item, col.dim)" />
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .groups-page {
      &__header {
        margin-bottom: 24px;
      }

      &__title {
        font-size: 28px;
        font-weight: 800;
      }

      &__sub {
        font-size: 13px;
        color: var(--color-text-muted);
        margin-top: 4px;
      }
    }

    .columns {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 16px;

      @media (max-width: 1024px) {
        grid-template-columns: 1fr;
      }
    }

    .card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 20px 24px;
    }

    .col {
      &__header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 16px;
        padding-bottom: 12px;
        border-bottom: 1px solid var(--color-border);
      }

      &__title {
        font-size: 13px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
      }

      &__avg {
        font-size: 22px;
        font-weight: 800;
        color: var(--color-green);

        &--yellow { color: var(--color-yellow); }
        &--red { color: var(--color-red); }
      }

      &__list {
        display: flex;
        flex-direction: column;
        gap: 6px;
      }

      &__empty {
        font-size: 13px;
        color: var(--color-text-muted);
        padding: 16px 0;
        text-align: center;
      }
    }

    .row {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 10px;
      border-radius: var(--radius-sm);
      cursor: pointer;
      transition: background var(--duration-fast);

      &:hover { background: var(--color-surface-2); }

      &__rank {
        font-size: 11px;
        font-weight: 700;
        color: var(--color-text-muted);
        min-width: 16px;
        text-align: right;
      }

      &__info {
        flex: 1;
        min-width: 0;
        display: flex;
        flex-direction: column;
      }

      &__name {
        font-size: 13px;
        font-weight: 600;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      &__company {
        font-size: 11px;
        color: var(--color-text-muted);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      &__score {
        min-width: 80px;
      }
    }

    .error-msg {
      color: var(--color-red);
      padding: 32px;
      text-align: center;
    }
  `],
})
export class RiskGroupsComponent implements OnInit {
  private readonly riskSvc = inject(RiskService);
  private readonly destroyRef = inject(DestroyRef);

  columns: RiskColumn[] = [];
  loading = true;
  error: string | null = null;

  ngOnInit(): void {
    forkJoin({
      churn: this.riskSvc.getTopAtRisk('churn', 10),
      payment: this.riskSvc.getTopAtRisk('payment', 10),
      margin: this.riskSvc.getTopAtRisk('margin', 10),
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ churn, payment, margin }) => {
        this.columns = [
          { label: 'Churn Risk', dim: 'churn', items: churn.data ?? [], avg: this.average(churn.data ?? [], 'churn') },
          { label: 'Payment Risk', dim: 'payment', items: payment.data ?? [], avg: this.average(payment.data ?? [], 'payment') },
          { label: 'Margin Risk', dim: 'margin', items: margin.data ?? [], avg: this.average(margin.data ?? [], 'margin') },
        ];
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message ?? 'Failed to load risk groups.';
        this.loading = false;
      },
    });
  }

  dimScore(item: TopAtRiskItem, dim: 'churn' | 'payment' | 'margin'): number {
    const map = { churn: item.churnScore, payment: item.paymentScore, margin: item.marginScore };
    return map[dim];
  }

  private average(items: TopAtRiskItem[], dim: 'churn' | 'payment' | 'margin'): number {
    if (items.length === 0) return 0;
    const sum = items.reduce((acc, i) => acc + this.dimScore(i, dim), 0);
    return Math.round(sum / items.length);
  }
}
