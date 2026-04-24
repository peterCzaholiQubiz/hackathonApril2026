import { Pipe, PipeTransform } from '@angular/core';
import { HeatLevel } from '../../core/models/risk-score.model';

const HEAT_COLOR_MAP: Record<HeatLevel, string> = {
  green: 'var(--color-green)',
  yellow: 'var(--color-yellow)',
  red: 'var(--color-red)',
};

@Pipe({
  name: 'heatColor',
  standalone: true,
})
export class HeatColorPipe implements PipeTransform {
  transform(value: HeatLevel | string | null | undefined): string {
    if (!value) return 'var(--color-text-muted)';
    return HEAT_COLOR_MAP[value as HeatLevel] ?? 'var(--color-text-muted)';
  }
}
