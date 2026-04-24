import { Component, Input } from '@angular/core';
import { NgClass } from '@angular/common';
import { HeatLevel } from '../../../core/models/risk-score.model';

const LABELS: Record<HeatLevel, string> = {
  green: 'Healthy',
  yellow: 'Watch',
  red: 'At Risk',
};

@Component({
  selector: 'app-heat-badge',
  standalone: true,
  imports: [NgClass],
  template: `<span class="badge" [ngClass]="heatLevel">{{ label }}</span>`,
  styles: [`
    .badge {
      display: inline-block;
      padding: 3px 10px;
      border-radius: var(--radius-sm);
      font-size: 11px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      white-space: nowrap;

      &.green  { background: var(--color-green); color: #fff; }
      &.yellow { background: var(--color-yellow); color: #000; }
      &.red    { background: var(--color-red);    color: #fff; }
    }
  `],
})
export class HeatBadgeComponent {
  @Input() heatLevel: HeatLevel = 'green';

  get label(): string {
    return LABELS[this.heatLevel];
  }
}
