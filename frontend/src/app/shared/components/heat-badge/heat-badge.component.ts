import { Component, Input } from '@angular/core';
import { NgClass } from '@angular/common';
import { HeatLevel } from '../../../core/models/risk-score.model';

@Component({
  selector: 'app-heat-badge',
  standalone: true,
  imports: [NgClass],
  template: `<span class="badge" [ngClass]="heatLevel">{{ heatLevel }}</span>`,
  styles: [`
    .badge {
      padding: 2px 8px;
      border-radius: var(--radius-sm);
      font-size: 12px;
      font-weight: 600;
      text-transform: uppercase;

      &.green {
        background: var(--color-green);
        color: #000;
      }

      &.yellow {
        background: var(--color-yellow);
        color: #000;
      }

      &.red {
        background: var(--color-red);
        color: #fff;
      }
    }
  `],
})
export class HeatBadgeComponent {
  @Input() heatLevel: HeatLevel = 'green';
}
