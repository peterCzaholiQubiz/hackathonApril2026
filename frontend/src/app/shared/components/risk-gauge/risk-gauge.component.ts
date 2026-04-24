import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-risk-gauge',
  standalone: true,
  template: `
    <div class="gauge">
      <div class="gauge__label">{{ label }}</div>
      <div class="gauge__arc-wrap">
        <svg viewBox="0 0 120 70" class="gauge__svg" aria-hidden="true">
          <!-- Background track -->
          <path
            d="M10,65 A50,50 0 0,1 110,65"
            fill="none"
            stroke="var(--color-surface-2)"
            stroke-width="10"
            stroke-linecap="round"
          />
          <!-- Colored fill -->
          <path
            d="M10,65 A50,50 0 0,1 110,65"
            fill="none"
            [attr.stroke]="arcColor"
            stroke-width="10"
            stroke-linecap="round"
            [style.stroke-dasharray]="dashArray"
            [style.stroke-dashoffset]="dashOffset"
          />
        </svg>
        <div class="gauge__value" [style.color]="arcColor">{{ score }}</div>
      </div>
    </div>
  `,
  styles: [`
    .gauge {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;

      &__label {
        font-size: 11px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
      }

      &__arc-wrap {
        position: relative;
        width: 120px;
      }

      &__svg {
        width: 100%;
        display: block;
      }

      &__value {
        position: absolute;
        bottom: 0;
        left: 50%;
        transform: translateX(-50%);
        font-size: 28px;
        font-weight: 800;
        line-height: 1;
      }
    }
  `],
})
export class RiskGaugeComponent {
  @Input() label: string = '';
  @Input() score: number = 0;

  private readonly arcLength = 157.08; // π * 50 (semicircle circumference)

  get arcColor(): string {
    if (this.score >= 70) return 'var(--color-red)';
    if (this.score >= 40) return 'var(--color-yellow)';
    return 'var(--color-green)';
  }

  get dashArray(): string {
    return `${this.arcLength} ${this.arcLength}`;
  }

  get dashOffset(): string {
    const filled = this.arcLength * (this.score / 100);
    return `${this.arcLength - filled}`;
  }
}
