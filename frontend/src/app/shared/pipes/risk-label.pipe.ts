import { Pipe, PipeTransform } from '@angular/core';

const LOW_MAX = 39;
const MEDIUM_MAX = 69;

@Pipe({
  name: 'riskLabel',
  standalone: true,
})
export class RiskLabelPipe implements PipeTransform {
  transform(score: number | null | undefined): string {
    if (score == null) return '—';
    if (score <= LOW_MAX) return 'Low';
    if (score <= MEDIUM_MAX) return 'Medium';
    return 'High';
  }
}
