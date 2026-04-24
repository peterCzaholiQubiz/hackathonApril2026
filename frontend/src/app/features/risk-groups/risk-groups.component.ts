import { Component } from '@angular/core';

@Component({
  selector: 'app-risk-groups',
  standalone: true,
  template: `
    <div class="page">
      <h1>Risk Groups</h1>
      <p class="coming-soon">— coming soon</p>
    </div>
  `,
  styles: [`
    .page { padding: 8px 0; }
    h1 { font-size: 24px; font-weight: 700; margin-bottom: 8px; }
    .coming-soon { color: var(--color-text-muted); font-size: 14px; }
  `],
})
export class RiskGroupsComponent {}
