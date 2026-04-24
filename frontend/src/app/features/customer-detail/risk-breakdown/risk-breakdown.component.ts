import { Component, Input } from '@angular/core';
import { CustomerRisk } from '../../../core/models/customer-risk.model';
import { RiskGaugeComponent } from '../../../shared/components/risk-gauge/risk-gauge.component';

@Component({
  selector: 'app-risk-breakdown',
  standalone: true,
  imports: [RiskGaugeComponent],
  template: `
    <div class="breakdown">
      <h2 class="breakdown__title">Risk Dimensions</h2>
      <div class="breakdown__gauges">
        <app-risk-gauge label="Churn Risk" [score]="risk.churnScore" />
        <app-risk-gauge label="Payment Risk" [score]="risk.paymentScore" />
        <app-risk-gauge label="Margin Risk" [score]="risk.marginScore" />
      </div>
    </div>
  `,
  styles: [`
    .breakdown {
      &__title {
        font-size: 13px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
        margin-bottom: 20px;
      }

      &__gauges {
        display: flex;
        justify-content: space-around;
        gap: 16px;
        flex-wrap: wrap;
      }
    }
  `],
})
export class RiskBreakdownComponent {
  @Input() risk!: CustomerRisk;
}
