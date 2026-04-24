import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-risk-gauge',
  standalone: true,
  template: `
    <div class="gauge">
      <div class="gauge__label">{{ label }}</div>
      <div class="gauge__score">{{ score }}</div>
    </div>
  `,
  styles: [`
    .gauge {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      padding: 16px;
      background: var(--color-surface-2);
      border-radius: var(--radius-md);
      min-width: 80px;
    }

    .gauge__label {
      font-size: 11px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      color: var(--color-text-muted);
    }

    .gauge__score {
      font-size: 28px;
      font-weight: 700;
      color: var(--color-text);
    }
  `],
})
export class RiskGaugeComponent {
  @Input() label: string = '';
  @Input() score: number = 0;
}
