import { DecimalPipe, NgClass } from '@angular/common';
import { Component, DestroyRef, Input, OnChanges, SimpleChanges, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { ApiMeta } from '../../../core/models/api-response.model';
import { CustomerPayment, CustomerPayments, PaymentSeverity } from '../../../core/models/customer-payment.model';
import { CustomerService } from '../../../core/services/customer.service';
import { LoadingSkeletonComponent } from '../../../shared/components/loading-skeleton/loading-skeleton.component';

type PaymentBucket = {
  key: PaymentSeverity;
  label: string;
  description: string;
  tone: PaymentSeverity;
  count: number;
};

@Component({
  selector: 'app-customer-payments-card',
  standalone: true,
  imports: [DecimalPipe, NgClass, LoadingSkeletonComponent],
  template: `
    <section class="payments-card">
      <header class="payments-card__header">
        <div>
          <p class="payments-card__eyebrow">Payments</p>
          <h2 class="payments-card__title">Payment behaviour</h2>
        </div>
        <span class="payments-card__page-size">12 rows per page</span>
      </header>

      @if (error) {
        <div class="payments-card__message payments-card__message--error">{{ error }}</div>
      } @else if (loading) {
        <app-loading-skeleton type="table" />
      } @else {
        <div class="payments-card__summary">
          @for (bucket of buckets; track bucket.key) {
            <button
              type="button"
              class="summary-chip"
              [ngClass]="[
                'summary-chip--' + bucket.tone,
                activeSeverity === bucket.key ? 'summary-chip--active' : ''
              ]"
              (click)="toggleSeverity(bucket.key)">
              <span class="summary-chip__label">{{ bucket.label }}</span>
              <span class="summary-chip__count">{{ bucket.count }}</span>
              <span class="summary-chip__description">{{ bucket.description }}</span>
            </button>
          }
        </div>

        <div class="payments-card__toolbar">
          <span class="payments-card__filter">
            @if (activeSeverity) {
              Showing {{ activeSeverity }} payments only
            } @else {
              Showing all payment severities
            }
          </span>
          @if (activeSeverity) {
            <button type="button" class="clear-btn" (click)="clearSeverity()">Clear filter</button>
          }
        </div>

        @if (payments.length === 0) {
          <div class="payments-card__message">No payments match the current filter.</div>
        } @else {
          <div class="payments-card__table-wrap">
            <table class="payments-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Amount</th>
                  <th>Days Late</th>
                  <th>Severity</th>
                </tr>
              </thead>
              <tbody>
                @for (payment of payments; track payment.id) {
                  <tr>
                    <td>{{ payment.paymentDate ?? '—' }}</td>
                    <td>
                      @if (payment.amount == null) {
                        —
                      } @else {
                        {{ payment.amount | number:'1.2-2' }}
                      }
                    </td>
                    <td>{{ payment.daysLate }}</td>
                    <td>
                      <span class="severity-pill" [ngClass]="'severity-pill--' + payment.severity">
                        {{ severityLabel(payment.severity) }}
                      </span>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <div class="pagination">
            <button type="button" class="page-btn" [disabled]="page <= 1" (click)="goTo(page - 1)">← Prev</button>
            <span class="page-info">Page {{ page }} of {{ totalPages }}</span>
            <button type="button" class="page-btn" [disabled]="page >= totalPages" (click)="goTo(page + 1)">Next →</button>
          </div>
        }
      }
    </section>
  `,
  styles: [`
    .payments-card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 24px 28px;
      display: flex;
      flex-direction: column;
      gap: 18px;
    }

    .payments-card__header,
    .payments-card__toolbar,
    .pagination {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }

    .payments-card__eyebrow {
      margin: 0 0 4px;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--color-text-muted);
    }

    .payments-card__title {
      margin: 0;
      font-size: 22px;
      font-weight: 800;
    }

    .payments-card__page-size,
    .payments-card__filter,
    .page-info {
      font-size: 13px;
      color: var(--color-text-muted);
    }

    .payments-card__summary {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 10px;

      @media (max-width: 720px) {
        grid-template-columns: 1fr;
      }
    }

    .summary-chip {
      --chip-accent: var(--color-text);
      text-align: left;
      padding: 14px 16px;
      border-radius: var(--radius-md);
      border: 1px solid var(--color-border);
      background: var(--color-surface-2);
      color: var(--color-text);
      cursor: pointer;
      transition: border-color var(--duration-fast), transform var(--duration-fast), background var(--duration-fast);
      display: flex;
      flex-direction: column;
      gap: 4px;

      &:hover {
        transform: translateY(-1px);
      }

      &--active {
        background: color-mix(in srgb, var(--chip-accent) 14%, var(--color-surface));
        border-color: currentColor;
        box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--chip-accent) 10%, transparent);
      }

      &--low {
        --chip-accent: var(--color-green);
      }

      &--low.summary-chip--active {
        color: var(--color-green);
      }

      &--medium {
        --chip-accent: var(--color-yellow);
      }

      &--medium.summary-chip--active {
        color: #8a6b00;
      }

      &--high {
        --chip-accent: var(--color-red);
      }

      &--high.summary-chip--active {
        color: var(--color-red);
      }
    }

    .summary-chip__label {
      font-size: 12px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .summary-chip__count {
      font-size: 24px;
      font-weight: 800;
      line-height: 1;
    }

    .summary-chip__description {
      font-size: 12px;
      color: var(--color-text-muted);
    }

    .clear-btn,
    .page-btn {
      min-height: 36px;
      padding: 0 12px;
      border-radius: var(--radius-sm);
      border: 1px solid var(--color-border);
      background: var(--color-surface-2);
      color: var(--color-text);
      font-size: 13px;
      font-weight: 600;
      cursor: pointer;
      transition: background var(--duration-fast);

      &:hover:not(:disabled) {
        background: var(--color-border);
      }

      &:disabled {
        opacity: 0.45;
        cursor: not-allowed;
      }
    }

    .payments-card__message {
      padding: 24px;
      border-radius: var(--radius-md);
      background: var(--color-surface-2);
      color: var(--color-text-muted);
      text-align: center;

      &--error {
        color: var(--color-red);
      }
    }

    .payments-card__table-wrap {
      overflow-x: auto;
    }

    .payments-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;

      th {
        text-align: left;
        font-size: 10px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--color-text-muted);
        padding: 0 12px 10px 0;
        border-bottom: 1px solid var(--color-border);
      }

      td {
        padding: 12px 12px 12px 0;
        border-bottom: 1px solid var(--color-border);
        vertical-align: middle;
      }
    }

    .severity-pill {
      display: inline-flex;
      align-items: center;
      min-height: 28px;
      padding: 0 10px;
      border-radius: 999px;
      font-size: 11px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;

      &--low {
        background: color-mix(in srgb, var(--color-green) 16%, var(--color-surface));
        color: var(--color-green);
      }

      &--medium {
        background: color-mix(in srgb, var(--color-yellow) 28%, var(--color-surface));
        color: #8a6b00;
      }

      &--high {
        background: color-mix(in srgb, var(--color-red) 16%, var(--color-surface));
        color: var(--color-red);
      }
    }

    @media (max-width: 720px) {
      .payments-card__header,
      .payments-card__toolbar,
      .pagination {
        flex-direction: column;
        align-items: flex-start;
      }
    }
  `],
})
export class CustomerPaymentsCardComponent implements OnChanges {
  @Input({ required: true }) customerId = '';

  private readonly customerService = inject(CustomerService);
  private readonly destroyRef = inject(DestroyRef);
  private loadSubscription: Subscription | null = null;

  readonly pageSize = 12;

  payments: CustomerPayment[] = [];
  summary: CustomerPayments['summary'] = { low: 0, medium: 0, high: 0 };
  loading = false;
  error: string | null = null;
  page = 1;
  total = 0;
  activeSeverity: PaymentSeverity | null = null;

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  get buckets(): PaymentBucket[] {
    return [
      { key: 'low', label: 'Low', description: '0 to 15 days', tone: 'low', count: this.summary.low },
      { key: 'medium', label: 'Medium', description: '16 to 30 days', tone: 'medium', count: this.summary.medium },
      { key: 'high', label: 'High', description: '31+ days', tone: 'high', count: this.summary.high },
    ];
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['customerId'] || !this.customerId) {
      return;
    }

    this.page = 1;
    this.activeSeverity = null;
    this.load();
  }

  goTo(page: number): void {
    this.page = page;
    this.load();
  }

  toggleSeverity(severity: PaymentSeverity): void {
    this.activeSeverity = this.activeSeverity === severity ? null : severity;
    this.page = 1;
    this.load();
  }

  clearSeverity(): void {
    this.activeSeverity = null;
    this.page = 1;
    this.load();
  }

  severityLabel(severity: PaymentSeverity): string {
    return severity.charAt(0).toUpperCase() + severity.slice(1);
  }

  private load(): void {
    this.loadSubscription?.unsubscribe();
    this.loading = true;
    this.error = null;

    this.loadSubscription = this.customerService.getPayments(this.customerId, {
      severity: this.activeSeverity ?? undefined,
      page: this.page,
      pageSize: this.pageSize,
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => {
        this.applyResponse(response.data, response.meta);
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message ?? 'Failed to load payments.';
        this.loading = false;
      },
    });
  }

  private applyResponse(data: CustomerPayments | null, meta: ApiMeta | null): void {
    this.payments = data?.payments ?? [];
    this.summary = data?.summary ?? { low: 0, medium: 0, high: 0 };
    this.activeSeverity = data?.activeSeverity ?? this.activeSeverity;
    this.total = meta?.total ?? this.payments.length;
    const totalPages = Math.max(1, Math.ceil(this.total / this.pageSize));
    this.page = Math.min(meta?.page ?? 1, totalPages);
  }
}