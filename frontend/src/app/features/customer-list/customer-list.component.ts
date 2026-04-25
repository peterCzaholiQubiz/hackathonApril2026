import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { CustomerService, CustomerListParams } from '../../core/services/customer.service';
import { Customer } from '../../core/models/customer.model';
import { HeatBadgeComponent } from '../../shared/components/heat-badge/heat-badge.component';
import { ScoreBarComponent } from '../../shared/components/score-bar/score-bar.component';
import { LoadingSkeletonComponent } from '../../shared/components/loading-skeleton/loading-skeleton.component';
import { CustomerFiltersComponent } from './customer-filters/customer-filters.component';

@Component({
  selector: 'app-customer-list',
  standalone: true,
  imports: [
    RouterLink,
    HeatBadgeComponent,
    ScoreBarComponent,
    LoadingSkeletonComponent,
    CustomerFiltersComponent,
  ],
  template: `
    <div class="list-page">
      <header class="list-page__header">
        <h1 class="list-page__title">Customers</h1>
        @if (totalCount > 0) {
          <span class="list-page__count">{{ totalCount }} total</span>
        }
      </header>

      <app-customer-filters (filtersChanged)="onFiltersChanged($event)" />

      @if (loading) {
        <div class="card"><app-loading-skeleton type="table" /></div>
      } @else if (error) {
        <div class="error-msg">{{ error }}</div>
      } @else {
        <div class="card">
          <table class="ctable">
            <thead>
              <tr>
                <th></th>
                <th>Name</th>
                <th>Company</th>
                <th>Segment</th>
                <th>Energy</th>
                <th>Churn</th>
                <th>Payment</th>
                <th>Margin</th>
                <th>Overall</th>
              </tr>
            </thead>
            <tbody>
              @if (customers.length === 0) {
                <tr><td colspan="9" class="ctable__empty">No customers match the current filters.</td></tr>
              }
              @for (c of customers; track c.id) {
                <tr class="ctable__row" [routerLink]="['/customers', c.id]">
                  <td><app-heat-badge [heatLevel]="latestHeat(c)" /></td>
                  <td class="ctable__name">{{ c.name }}</td>
                  <td class="ctable__muted">{{ c.companyName ?? '—' }}</td>
                  <td class="ctable__muted">{{ c.segment ?? '—' }}</td>
                  <td class="ctable__energy">
                    @for (part of energyParts(c); track part.label) {
                      <span class="energy-badge energy-badge--{{ part.type }}" [title]="part.label">{{ part.icon }}</span>
                    }
                  </td>
                  <td><app-score-bar [score]="latestScore(c, 'churn')" /></td>
                  <td><app-score-bar [score]="latestScore(c, 'payment')" /></td>
                  <td><app-score-bar [score]="latestScore(c, 'margin')" /></td>
                  <td><app-score-bar [score]="latestScore(c, 'overall')" /></td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <div class="pagination">
          <button class="page-btn" [disabled]="page <= 1" (click)="goTo(page - 1)">← Prev</button>
          <span class="page-info">Page {{ page }} of {{ totalPages }}</span>
          <button class="page-btn" [disabled]="page >= totalPages" (click)="goTo(page + 1)">Next →</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .list-page {
      &__header {
        display: flex;
        align-items: baseline;
        gap: 12px;
        margin-bottom: 20px;
      }

      &__title {
        font-size: 28px;
        font-weight: 800;
      }

      &__count {
        font-size: 13px;
        color: var(--color-text-muted);
      }
    }

    .card {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: 20px 24px;
      margin-bottom: 16px;
    }

    .ctable {
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

      &__row {
        cursor: pointer;
        td {
          padding: 10px 12px 10px 0;
          border-bottom: 1px solid var(--color-border);
          vertical-align: middle;
          transition: background var(--duration-fast);
        }
        &:hover td { background: var(--color-surface-2); }
      }

      &__name { font-weight: 600; min-width: 140px; }
      &__muted { color: var(--color-text-muted); }
      &__energy { white-space: nowrap; }
      &__empty { padding: 32px 0; text-align: center; color: var(--color-text-muted); }
    }

    .energy-badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 24px;
      height: 24px;
      border-radius: 6px;
      font-size: 13px;
      line-height: 1;

      & + & { margin-left: 4px; }

      &--elec-consumption { background: rgba(239, 68, 68, 0.18); }
      &--elec-production  { background: rgba(34, 197, 94, 0.18); }
      &--gas              { background: rgba(245, 158, 11, 0.18); }
      &--none             { color: var(--color-text-muted); background: transparent; }
    }

    .pagination {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 16px;
      padding-top: 8px;
    }

    .page-btn {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-sm);
      color: var(--color-text);
      font-size: 13px;
      font-weight: 600;
      padding: 7px 14px;
      cursor: pointer;
      transition: background var(--duration-fast);

      &:hover:not(:disabled) { background: var(--color-surface-2); }
      &:disabled { opacity: 0.4; cursor: not-allowed; }
    }

    .page-info {
      font-size: 13px;
      color: var(--color-text-muted);
    }

    .error-msg {
      color: var(--color-red);
      padding: 24px;
      text-align: center;
    }
  `],
})
export class CustomerListComponent implements OnInit {
  private readonly customerSvc = inject(CustomerService);
  private readonly destroyRef = inject(DestroyRef);

  customers: Customer[] = [];
  loading = true;
  error: string | null = null;
  page = 1;
  totalCount = 0;
  readonly pageSize = 20;

  private activeFilters: CustomerListParams = { sortBy: 'overallScore', sortDir: 'desc' };

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  ngOnInit(): void {
    this.load();
  }

  onFiltersChanged(filters: CustomerListParams): void {
    this.activeFilters = filters;
    this.page = 1;
    this.load();
  }

  goTo(p: number): void {
    this.page = p;
    this.load();
  }

  latestHeat(c: Customer) {
    return c.latestRisk?.heatLevel ?? 'green';
  }

  energyParts(c: Customer): { icon: string; type: string; label: string }[] {
    const types = c.energyTypes ?? [];
    const parts: { icon: string; type: string; label: string }[] = [];
    const hasElecConsumption = types.some(t => /electricity:consumption/i.test(t) || t.toLowerCase() === 'electricity');
    const hasElecProduction  = types.some(t => /electricity:production/i.test(t));
    const hasGas             = types.some(t => /^gas/i.test(t));
    if (hasElecConsumption) parts.push({ icon: '⚡', type: 'elec-consumption', label: 'Electricity (Consumption)' });
    if (hasElecProduction)  parts.push({ icon: '⚡', type: 'elec-production',  label: 'Electricity (Production / Solar)' });
    if (hasGas)             parts.push({ icon: '🔥', type: 'gas',              label: 'Gas' });
    if (parts.length === 0) parts.push({ icon: '—',  type: 'none',             label: 'No connections' });
    return parts;
  }

  latestScore(c: Customer, dim: 'churn' | 'payment' | 'margin' | 'overall'): number {
    const s = c.latestRisk;
    if (!s) return 0;
    const map = { churn: s.churnScore, payment: s.paymentScore, margin: s.marginScore, overall: s.overallScore };
    return map[dim];
  }

  private load(): void {
    this.loading = true;
    this.error = null;

    this.customerSvc.getList({ ...this.activeFilters, page: this.page, pageSize: this.pageSize })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.customers = res.data ?? [];
          this.totalCount = res.meta?.total ?? 0;
          this.loading = false;
        },
        error: (err) => {
          this.error = err.message ?? 'Failed to load customers.';
          this.loading = false;
        },
      });
  }
}


