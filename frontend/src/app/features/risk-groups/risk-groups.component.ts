import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import {
  RiskService,
  RiskDimensionGroup,
  RiskDimensionItem,
  RiskDimensionGroupsResponse,
  HeatSummary,
} from '../../core/services/risk.service';
import { HeatBadgeComponent } from '../../shared/components/heat-badge/heat-badge.component';
import { ScoreBarComponent } from '../../shared/components/score-bar/score-bar.component';
import { LoadingSkeletonComponent } from '../../shared/components/loading-skeleton/loading-skeleton.component';

@Component({
  selector: 'app-risk-groups',
  standalone: true,
  imports: [RouterLink, DecimalPipe, CurrencyPipe, NgClass, HeatBadgeComponent, ScoreBarComponent, LoadingSkeletonComponent],
  template: `
    <div class="groups-page">
      <header class="groups-page__header">
        <h1 class="groups-page__title">Risk Groups</h1>
        <p class="groups-page__sub">Portfolio intelligence — who is at risk, why, and what to do</p>
      </header>

      @if (loading) {
        <div class="heat-summary">
          @for (_ of [1,2,3]; track $index) {
            <div class="heat-card card"><app-loading-skeleton type="text" /></div>
          }
        </div>
        <div class="dimensions">
          @for (_ of [1,2,3]; track $index) {
            <div class="card"><app-loading-skeleton type="table" /></div>
          }
        </div>
      } @else if (error) {
        <div class="error-msg">{{ error }}</div>
      } @else if (!data) {
        <div class="empty-msg">No risk data available yet. Trigger a risk scoring run first.</div>
      } @else {
        <!-- Portfolio heat summary -->
        <section class="heat-summary">
          <div class="heat-card heat-card--green card">
            <div class="heat-card__label">Healthy</div>
            <div class="heat-card__count">{{ data.heatSummary.green.count }}</div>
            <div class="heat-card__pct">{{ data.heatSummary.green.pct | number:'1.0-1' }}% of portfolio</div>
            @if (data.heatSummary.green.totalMonthlyValue > 0) {
              <div class="heat-card__value">{{ data.heatSummary.green.totalMonthlyValue | currency:'EUR':'symbol':'1.0-0' }}/mo</div>
            }
          </div>
          <div class="heat-card heat-card--yellow card">
            <div class="heat-card__label">Watch</div>
            <div class="heat-card__count">{{ data.heatSummary.yellow.count }}</div>
            <div class="heat-card__pct">{{ data.heatSummary.yellow.pct | number:'1.0-1' }}% of portfolio</div>
            @if (data.heatSummary.yellow.totalMonthlyValue > 0) {
              <div class="heat-card__value">{{ data.heatSummary.yellow.totalMonthlyValue | currency:'EUR':'symbol':'1.0-0' }}/mo</div>
            }
          </div>
          <div class="heat-card heat-card--red card">
            <div class="heat-card__label">At Risk</div>
            <div class="heat-card__count">{{ data.heatSummary.red.count }}</div>
            <div class="heat-card__pct">{{ data.heatSummary.red.pct | number:'1.0-1' }}% of portfolio</div>
            @if (data.heatSummary.red.totalMonthlyValue > 0) {
              <div class="heat-card__value">{{ data.heatSummary.red.totalMonthlyValue | currency:'EUR':'symbol':'1.0-0' }}/mo</div>
            }
          </div>
        </section>

        <!-- Dimension groups -->
        @for (group of data.dimensions; track group.dimension) {
          <section class="dim-section card">
            <div class="dim-section__header">
              <div class="dim-section__title-row">
                <h2 class="dim-section__title">{{ group.label }}</h2>
                <span class="dim-section__avg" [ngClass]="avgClass(group.avgScore)">
                  avg {{ group.avgScore | number:'1.0-0' }}
                </span>
              </div>
              <div class="dim-section__meta">
                <span>{{ group.totalFlagged }} customers flagged in portfolio</span>
                @if (group.totalMonthlyValue > 0) {
                  <span class="dim-section__value">{{ group.totalMonthlyValue | currency:'EUR':'symbol':'1.0-0' }}/mo at risk (all flagged)</span>
                }
              </div>
            </div>

            <div class="item-list">
              @for (item of group.items; track item.customerId; let i = $index) {
                <div class="item" [class.item--expanded]="isExpanded(group.dimension, item.customerId)">
                  <div class="item__row" (click)="toggle(group.dimension, item.customerId)">
                    <span class="item__rank">{{ i + 1 }}</span>
                    <app-heat-badge [heatLevel]="item.heatLevel" />
                    <div class="item__info">
                      <span class="item__name">{{ item.name }}</span>
                      @if (item.companyName) {
                        <span class="item__company">{{ item.companyName }}</span>
                      }
                    </div>
                    @if (item.segment) {
                      <span class="item__segment">{{ item.segment }}</span>
                    }
                    <div class="item__score">
                      <app-score-bar [score]="dimScore(item, group.dimension)" />
                      <span class="item__score-num" [ngClass]="scoreClass(dimScore(item, group.dimension))">{{ dimScore(item, group.dimension) }}</span>
                    </div>
                    @if (item.monthlyContractValue > 0) {
                      <span class="item__value">{{ item.monthlyContractValue | currency:'EUR':'symbol':'1.0-0' }}/mo</span>
                    }
                    <span class="item__chevron" [class.item__chevron--open]="isExpanded(group.dimension, item.customerId)">›</span>
                  </div>

                  @if (isExpanded(group.dimension, item.customerId)) {
                    <div class="item__panel">
                      @if (item.explanation) {
                        <div class="panel-block panel-block--explanation">
                          <div class="panel-block__header">
                            <span class="panel-block__icon">💡</span>
                            <span class="panel-block__title">Why flagged</span>
                            @if (item.confidence) {
                              <span class="confidence" [ngClass]="'confidence--' + item.confidence">{{ item.confidence }}</span>
                            }
                          </div>
                          <p class="panel-block__text">{{ item.explanation }}</p>
                        </div>
                      }

                      @if (item.topAction) {
                        <div class="panel-block panel-block--action">
                          <div class="panel-block__header">
                            <span class="panel-block__icon">{{ actionIcon(item.topAction.actionType) }}</span>
                            <span class="panel-block__title">{{ item.topAction.title }}</span>
                            <span class="priority" [ngClass]="'priority--' + item.topAction.priority">{{ item.topAction.priority }}</span>
                          </div>
                          @if (item.topAction.description) {
                            <p class="panel-block__text">{{ item.topAction.description }}</p>
                          }
                        </div>
                      }

                      <a class="panel-link" [routerLink]="['/customers', item.customerId]">
                        View full profile →
                      </a>
                    </div>
                  }
                </div>
              }
            </div>
          </section>
        }
      }
    </div>
  `,
  styles: [`
    .groups-page {
      &__header { margin-bottom: 24px; }
      &__title { font-size: 28px; font-weight: 800; }
      &__sub { font-size: 13px; color: var(--color-text-muted); margin-top: 4px; }
    }

    .card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 20px 24px;
    }

    /* Heat summary */
    .heat-summary {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 16px;
      margin-bottom: 24px;

      @media (max-width: 768px) { grid-template-columns: 1fr; }
    }

    .heat-card {
      text-align: center;
      border-top: 4px solid transparent;

      &--green  { border-top-color: var(--color-green); }
      &--yellow { border-top-color: var(--color-yellow); }
      &--red    { border-top-color: var(--color-red); }

      &__label {
        font-size: 11px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
        margin-bottom: 8px;
      }

      &__count {
        font-size: 48px;
        font-weight: 900;
        line-height: 1;
        color: var(--color-text);
      }

      &__pct {
        font-size: 13px;
        color: var(--color-text-muted);
        margin-top: 4px;
      }

      &__value {
        font-size: 13px;
        font-weight: 600;
        color: var(--color-text);
        margin-top: 8px;
        padding-top: 8px;
        border-top: 1px solid var(--color-border);
      }
    }

    /* Dimension sections */
    .dimensions { display: flex; flex-direction: column; gap: 16px; }

    .dim-section {
      margin-bottom: 16px;

      &__header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        flex-wrap: wrap;
        gap: 8px;
        padding-bottom: 14px;
        margin-bottom: 8px;
        border-bottom: 1px solid var(--color-border);
      }

      &__title-row {
        display: flex;
        align-items: center;
        gap: 12px;
      }

      &__title {
        font-size: 15px;
        font-weight: 700;
      }

      &__avg {
        font-size: 22px;
        font-weight: 800;
        color: var(--color-green);

        &.avg--yellow { color: var(--color-yellow); }
        &.avg--red    { color: var(--color-red); }
      }

      &__meta {
        display: flex;
        flex-direction: column;
        align-items: flex-end;
        gap: 2px;
        font-size: 12px;
        color: var(--color-text-muted);
        text-align: right;
      }

      &__value {
        font-weight: 600;
        color: var(--color-text);
      }
    }

    /* Item rows */
    .item-list { display: flex; flex-direction: column; }

    .item {
      border-bottom: 1px solid var(--color-border);

      &:last-child { border-bottom: none; }

      &--expanded > .item__row { background: var(--color-surface-2); }

      &__row {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 10px 8px;
        cursor: pointer;
        border-radius: var(--radius-sm);
        transition: background var(--duration-fast);

        &:hover { background: var(--color-surface-2); }
      }

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

      &__segment {
        font-size: 11px;
        font-weight: 600;
        padding: 2px 8px;
        background: var(--color-surface-2);
        border-radius: var(--radius-sm);
        color: var(--color-text-muted);
        white-space: nowrap;
      }

      &__score {
        display: flex;
        align-items: center;
        gap: 6px;
        min-width: 110px;
      }

      &__score-num {
        font-size: 13px;
        font-weight: 700;
        min-width: 26px;
        text-align: right;
        color: var(--color-green);

        &.score--yellow { color: var(--color-yellow); }
        &.score--red    { color: var(--color-red); }
      }

      &__value {
        font-size: 12px;
        color: var(--color-text-muted);
        white-space: nowrap;
        min-width: 70px;
        text-align: right;
      }

      &__chevron {
        font-size: 18px;
        font-weight: 300;
        color: var(--color-text-muted);
        transform: rotate(0deg);
        transition: transform var(--duration-fast);
        line-height: 1;
        min-width: 16px;
        text-align: center;

        &--open { transform: rotate(90deg); }
      }

      &__panel {
        padding: 0 8px 14px 40px;
        display: flex;
        flex-direction: column;
        gap: 10px;
      }
    }

    /* Panel blocks */
    .panel-block {
      background: var(--color-surface-2);
      border-radius: var(--radius-sm);
      padding: 12px 14px;
      border-left: 3px solid var(--color-border);

      &--explanation { border-left-color: var(--color-yellow); }
      &--action      { border-left-color: var(--color-green); }

      &__header {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 6px;
      }

      &__icon { font-size: 14px; }

      &__title {
        font-size: 12px;
        font-weight: 700;
        flex: 1;
      }

      &__text {
        font-size: 12px;
        color: var(--color-text-muted);
        line-height: 1.5;
        margin: 0;
      }

      &__empty {
        font-size: 12px;
        color: var(--color-text-muted);
        font-style: italic;
        padding: 6px 0;
      }
    }

    .confidence {
      font-size: 10px;
      font-weight: 700;
      text-transform: uppercase;
      padding: 2px 6px;
      border-radius: var(--radius-sm);

      &--high   { background: var(--color-green); color: #fff; }
      &--medium { background: var(--color-yellow); color: #000; }
      &--low    { background: var(--color-border); color: var(--color-text-muted); }
    }

    .priority {
      font-size: 10px;
      font-weight: 700;
      text-transform: uppercase;
      padding: 2px 6px;
      border-radius: var(--radius-sm);

      &--high   { background: var(--color-red); color: #fff; }
      &--medium { background: var(--color-yellow); color: #000; }
      &--low    { background: var(--color-border); color: var(--color-text-muted); }
    }

    .panel-link {
      font-size: 12px;
      font-weight: 600;
      color: var(--color-primary, var(--color-green));
      text-decoration: none;
      align-self: flex-start;
      padding-left: 4px;
      padding-top: 4px;

      &:hover { text-decoration: underline; }
    }

    .error-msg, .empty-msg {
      color: var(--color-text-muted);
      padding: 32px;
      text-align: center;
    }

    .error-msg { color: var(--color-red); }
  `],
})
export class RiskGroupsComponent implements OnInit {
  private readonly riskSvc = inject(RiskService);
  private readonly destroyRef = inject(DestroyRef);

  loading = true;
  error: string | null = null;
  data: RiskDimensionGroupsResponse | null = null;

  private expanded = new Set<string>();

  ngOnInit(): void {
    this.riskSvc.getRiskDimensionGroups(10)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (resp) => {
          this.data = resp.data ?? null;
          this.loading = false;
        },
        error: (err) => {
          this.error = err.message ?? 'Failed to load risk groups.';
          this.loading = false;
        },
      });
  }

  toggle(dim: string, customerId: string): void {
    const key = `${dim}:${customerId}`;
    if (this.expanded.has(key)) {
      this.expanded.delete(key);
    } else {
      this.expanded.add(key);
    }
  }

  isExpanded(dim: string, customerId: string): boolean {
    return this.expanded.has(`${dim}:${customerId}`);
  }

  dimScore(item: RiskDimensionItem, dim: string): number {
    const map: Record<string, number> = {
      churn: item.churnScore,
      payment: item.paymentScore,
      margin: item.marginScore,
    };
    return map[dim] ?? item.overallScore;
  }

  avgClass(score: number): string {
    if (score >= 70) return 'avg--red';
    if (score >= 40) return 'avg--yellow';
    return '';
  }

  scoreClass(score: number): string {
    if (score >= 70) return 'score--red';
    if (score >= 40) return 'score--yellow';
    return '';
  }

  actionIcon(actionType: string): string {
    const icons: Record<string, string> = {
      outreach: '📞',
      discount: '💰',
      review:   '🔍',
      escalate: '⚠️',
      upsell:   '📈',
    };
    return icons[actionType] ?? '📋';
  }
}
