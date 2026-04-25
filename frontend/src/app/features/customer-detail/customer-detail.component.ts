import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { CustomerService } from '../../core/services/customer.service';
import { CustomerDetail, Interaction, Complaint } from '../../core/models/customer.model';
import { CustomerRisk } from '../../core/models/customer-risk.model';
import { HeatBadgeComponent } from '../../shared/components/heat-badge/heat-badge.component';
import { LoadingSkeletonComponent } from '../../shared/components/loading-skeleton/loading-skeleton.component';
import { RiskBreakdownComponent } from './risk-breakdown/risk-breakdown.component';
import { ExplanationPanelComponent } from './explanation-panel/explanation-panel.component';
import { ActionsPanelComponent } from './actions-panel/actions-panel.component';
import { CustomerTimelineComponent } from './customer-timeline/customer-timeline.component';
import { CustomerConsumptionCardComponent } from './customer-consumption-card/customer-consumption-card.component';
import { CustomerPaymentsCardComponent } from './customer-payments-card/customer-payments-card.component';
import { ComplaintsBoardComponent } from './complaints-board/complaints-board.component';

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [
    RouterLink,
    SlicePipe,
    HeatBadgeComponent,
    LoadingSkeletonComponent,
    RiskBreakdownComponent,
    ExplanationPanelComponent,
    ActionsPanelComponent,
    CustomerTimelineComponent,
    CustomerConsumptionCardComponent,
    CustomerPaymentsCardComponent,
    ComplaintsBoardComponent,
  ],
  template: `
    <div class="detail-page">
      @if (loading) {
        <div class="skeleton-wrap">
          <app-loading-skeleton type="card" />
          <app-loading-skeleton type="chart" />
          <app-loading-skeleton type="text" />
        </div>
      } @else if (error) {
        <div class="error-msg">{{ error }}</div>
      } @else if (customer) {
        <header class="detail-page__header">
          <a class="back-link" routerLink="/customers">← Customers</a>
          <div class="detail-page__identity">
            <app-heat-badge [heatLevel]="heatLevel" />
            <h1 class="detail-page__name">{{ customer.name }}</h1>
          </div>
          <div class="detail-page__meta">
            @if (customer.companyName) {
              <span class="meta-chip">{{ customer.companyName }}</span>
            }
            @if (customer.segment) {
              <span class="meta-chip meta-chip--segment">{{ customer.segment }}</span>
            }
            @if (customer.accountManager) {
              <span class="meta-chip meta-chip--muted">AM: {{ customer.accountManager }}</span>
            }
            @if (customer.onboardingDate) {
              <span class="meta-chip meta-chip--muted">Since {{ customer.onboardingDate | slice:0:10 }}</span>
            }
            <button
              class="btn-calculate"
              [disabled]="calculating"
              (click)="calculateRisk()"
            >
              {{ calculating ? 'Calculating...' : 'Calculate Risk' }}
            </button>
          </div>
        </header>

        <div class="detail-page__body">
          @if (risk) {
            <section class="card">
              <app-risk-breakdown [risk]="risk" />
            </section>

            <section class="card">
              <app-explanation-panel
                [explanations]="risk.riskExplanations"
                [overallScore]="risk.overallScore"
              />
            </section>
          } @else {
            <section class="card card--empty-state">
              <h2 class="empty-state__title">Risk analysis not available</h2>
              <p class="empty-state__text">
                This customer has not been scored yet, so risk gauges, AI explanations, and suggested actions are not available.
              </p>
            </section>
          }

          <app-customer-consumption-card [customerId]="customer.id" />

          <app-customer-payments-card [customerId]="customer.id" />

          <section class="card card--complaints">
            <app-complaints-board [complaints]="complaints" />
          </section>

          <div class="detail-page__bottom">
            <section class="card card--actions">
              @if (risk) {
                <app-actions-panel
                  [actions]="risk.suggestedActions"
                  [generating]="generatingActions"
                  [error]="actionsError"
                  (generateRequested)="generateActions()"
                />
              } @else {
                <div class="empty-state empty-state--compact">
                  <h2 class="empty-state__title">Suggested actions unavailable</h2>
                  <p class="empty-state__text">Actions will appear here once a risk score is generated for this customer.</p>
                </div>
              }
            </section>
            <section class="card card--timeline">
              <app-customer-timeline
                [interactions]="interactions"
                [complaints]="complaints"
              />
            </section>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .detail-page {
      &__header {
        margin-bottom: 28px;
      }

      &__identity {
        display: flex;
        align-items: center;
        gap: 12px;
        margin: 12px 0 10px;
      }

      &__name {
        font-size: 28px;
        font-weight: 800;
        line-height: 1.1;
      }

      &__meta {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
      }

      &__body {
        display: flex;
        flex-direction: column;
        gap: 16px;
      }

      &__bottom {
        display: grid;
        grid-template-columns: 1fr 2fr;
        gap: 16px;

        @media (max-width: 900px) {
          grid-template-columns: 1fr;
        }
      }
    }

    .back-link {
      font-size: 13px;
      color: var(--color-text-muted);
      text-decoration: none;
      font-weight: 600;

      &:hover { color: var(--color-text); }
    }

    .meta-chip {
      font-size: 12px;
      font-weight: 600;
      padding: 3px 10px;
      border-radius: var(--radius-sm);
      background: var(--color-surface-2);
      border: 1px solid var(--color-border);
      color: var(--color-text);

      &--segment {
        background: var(--color-surface);
        color: var(--color-text-muted);
        text-transform: uppercase;
        letter-spacing: 0.04em;
        font-size: 11px;
      }

      &--muted {
        color: var(--color-text-muted);
      }
    }

    .card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 24px 28px;

      &--complaints { min-width: 0; }
      &--actions { min-width: 0; }
      &--timeline { min-width: 0; }
      &--empty-state { min-width: 0; }
    }

    .empty-state {
      display: flex;
      flex-direction: column;
      gap: 8px;

      &--compact {
        min-height: 100%;
        justify-content: center;
      }

      &__title {
        margin: 0;
        font-size: 16px;
        font-weight: 700;
      }

      &__text {
        margin: 0;
        font-size: 14px;
        line-height: 1.6;
        color: var(--color-text-muted);
      }
    }

    .btn-calculate {
      font-size: 12px;
      font-weight: 700;
      padding: 4px 14px;
      border-radius: var(--radius-sm);
      border: 1px solid var(--color-border);
      background: var(--color-surface-2);
      color: var(--color-text);
      cursor: pointer;
      transition: background 150ms;

      &:hover:not(:disabled) { background: var(--color-surface-3, #e5e7eb); }
      &:disabled { opacity: 0.5; cursor: not-allowed; }
    }

    .skeleton-wrap {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .error-msg {
      color: var(--color-red);
      padding: 32px;
      text-align: center;
    }
  `],
})
export class CustomerDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly customerSvc = inject(CustomerService);
  private readonly destroyRef = inject(DestroyRef);

  customer: CustomerDetail | null = null;
  risk: CustomerRisk | null = null;
  interactions: Interaction[] = [];
  complaints: Complaint[] = [];
  loading = true;
  calculating = false;
  generatingActions = false;
  actionsError: string | null = null;
  error: string | null = null;

  get heatLevel() {
    if (!this.risk) return 'green' as const;
    const s = this.risk.overallScore;
    if (s >= 70) return 'red' as const;
    if (s >= 40) return 'yellow' as const;
    return 'green' as const;
  }

  generateActions(): void {
    if (!this.customer || this.generatingActions) return;
    this.generatingActions = true;
    this.actionsError = null;
    this.customerSvc.generateActions(this.customer.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (this.risk && res.data) {
            this.risk = { ...this.risk, suggestedActions: res.data };
          }
          this.generatingActions = false;
        },
        error: (err) => {
          this.actionsError = err?.error?.error ?? err?.message ?? 'Failed to generate actions. Please try again.';
          this.generatingActions = false;
        },
      });
  }

  calculateRisk(): void {
    if (!this.customer || this.calculating) return;
    this.calculating = true;
    this.customerSvc.calculateRisk(this.customer.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.risk = res.data ?? null;
          this.calculating = false;
        },
        error: () => {
          this.calculating = false;
        },
      });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error = 'Customer ID missing.';
      this.loading = false;
      return;
    }

    forkJoin({
      customer: this.customerSvc.getById(id),
      risk: this.customerSvc.getRisk(id),
      interactions: this.customerSvc.getInteractions(id),
      complaints: this.customerSvc.getComplaints(id),
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ customer, risk, interactions, complaints }) => {
        this.customer = customer.data ?? null;
        this.risk = risk.data ?? null;
        this.interactions = interactions.data ?? [];
        this.complaints = complaints.data ?? [];
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message ?? 'Failed to load customer.';
        this.loading = false;
      },
    });
  }
}
