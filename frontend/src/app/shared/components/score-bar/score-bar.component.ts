import { Component, Input } from '@angular/core';
import { NgStyle } from '@angular/common';

@Component({
  selector: 'app-score-bar',
  standalone: true,
  imports: [NgStyle],
  template: `
    <div class="score-bar">
      @if (label) {
        <div class="score-bar__label">{{ label }}</div>
      }
      <div class="score-bar__track">
        <div
          class="score-bar__fill"
          [ngStyle]="{ width: score + '%', background: barColor }"
        ></div>
      </div>
      <div class="score-bar__value">{{ score }}</div>
    </div>
  `,
  styles: [`
    .score-bar {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .score-bar__label {
      font-size: 12px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--color-text-muted);
      min-width: 64px;
    }

    .score-bar__track {
      flex: 1;
      height: 8px;
      background: var(--color-surface-2);
      border-radius: var(--radius-sm);
      overflow: hidden;
    }

    .score-bar__fill {
      height: 100%;
      border-radius: var(--radius-sm);
      transition: width var(--duration-normal) ease;
    }

    .score-bar__value {
      font-size: 13px;
      font-weight: 700;
      color: var(--color-text);
      min-width: 28px;
      text-align: right;
    }
  `],
})
export class ScoreBarComponent {
  @Input() label: string = '';
  @Input() score: number = 0;

  get barColor(): string {
    if (this.score >= 70) return 'var(--color-red)';
    if (this.score >= 40) return 'var(--color-yellow)';
    return 'var(--color-green)';
  }
}
