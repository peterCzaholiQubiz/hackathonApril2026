import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HeatBadgeComponent } from '../../../shared/components/heat-badge/heat-badge.component';
import { ScoreBarComponent } from '../../../shared/components/score-bar/score-bar.component';
import { TopAtRiskItem } from '../../../core/services/risk.service';

@Component({
  selector: 'app-top-at-risk',
  standalone: true,
  imports: [RouterLink, HeatBadgeComponent, ScoreBarComponent],
  template: `
    <div class="top-risk">
      <h2 class="top-risk__title">Top At-Risk Customers</h2>
      @if (customers.length === 0) {
        <p class="top-risk__empty">No customers to display.</p>
      } @else {
        <table class="risk-table">
          <thead>
            <tr>
              <th></th>
              <th>Customer</th>
              <th>Segment</th>
              <th>Churn</th>
              <th>Payment</th>
              <th>Margin</th>
              <th>Overall</th>
            </tr>
          </thead>
          <tbody>
            @for (c of customers; track c.customerId) {
              <tr class="risk-table__row" [routerLink]="['/customers', c.customerId]">
                <td><app-heat-badge [heatLevel]="c.heatLevel" /></td>
                <td class="risk-table__name">{{ c.name }}</td>
                <td class="risk-table__segment">{{ c.segment ?? '—' }}</td>
                <td><app-score-bar [score]="c.churnScore" /></td>
                <td><app-score-bar [score]="c.paymentScore" /></td>
                <td><app-score-bar [score]="c.marginScore" /></td>
                <td><app-score-bar [score]="c.overallScore" /></td>
              </tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: [`
    .top-risk {
      &__title {
        font-size: 13px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
        margin-bottom: 16px;
      }

      &__empty {
        color: var(--color-text-muted);
        font-size: 13px;
        padding: 24px 0;
        text-align: center;
      }
    }

    .risk-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;

      th {
        text-align: left;
        font-size: 11px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--color-text-muted);
        padding: 0 12px 10px 0;
        border-bottom: 1px solid var(--color-border);
      }

      &__row {
        cursor: pointer;
        transition: background var(--duration-fast);

        &:hover td { background: var(--color-surface-2); }

        td {
          padding: 10px 12px 10px 0;
          border-bottom: 1px solid var(--color-border);
          vertical-align: middle;
          transition: background var(--duration-fast);
        }
      }

      &__name {
        font-weight: 600;
        color: var(--color-text);
        min-width: 120px;
      }

      &__segment {
        color: var(--color-text-muted);
        min-width: 80px;
      }
    }
  `],
})
export class TopAtRiskComponent {
  @Input() customers: TopAtRiskItem[] = [];
}
