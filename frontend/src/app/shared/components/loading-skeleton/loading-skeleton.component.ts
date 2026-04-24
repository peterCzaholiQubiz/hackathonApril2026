import { Component, Input } from '@angular/core';

type SkeletonType = 'card' | 'table' | 'chart' | 'text';

@Component({
  selector: 'app-loading-skeleton',
  standalone: true,
  template: `
    @if (type === 'card') {
      <div class="skeleton-card">
        <div class="shimmer" style="height:16px;width:40%;margin-bottom:12px;border-radius:4px"></div>
        <div class="shimmer" style="height:48px;width:70%;margin-bottom:8px;border-radius:4px"></div>
        <div class="shimmer" style="height:12px;width:55%;border-radius:4px"></div>
      </div>
    }
    @if (type === 'table') {
      <div class="skeleton-table">
        @for (row of rows; track $index) {
          <div class="skeleton-row">
            <div class="shimmer" style="height:14px;width:8%;border-radius:4px"></div>
            <div class="shimmer" style="height:14px;width:20%;border-radius:4px"></div>
            <div class="shimmer" style="height:14px;width:12%;border-radius:4px"></div>
            <div class="shimmer" style="height:14px;width:16%;border-radius:4px"></div>
            <div class="shimmer" style="height:14px;width:16%;border-radius:4px"></div>
          </div>
        }
      </div>
    }
    @if (type === 'chart') {
      <div class="shimmer skeleton-chart"></div>
    }
    @if (type === 'text') {
      <div class="skeleton-text">
        <div class="shimmer" style="height:14px;width:100%;border-radius:4px"></div>
        <div class="shimmer" style="height:14px;width:80%;border-radius:4px"></div>
        <div class="shimmer" style="height:14px;width:90%;border-radius:4px"></div>
      </div>
    }
  `,
  styles: [`
    .shimmer {
      background: linear-gradient(90deg,
        var(--color-surface-2) 25%,
        var(--color-border) 50%,
        var(--color-surface-2) 75%);
      background-size: 200% 100%;
      animation: shimmer 1.5s infinite;
    }

    @keyframes shimmer {
      0%   { background-position: 200% 0; }
      100% { background-position: -200% 0; }
    }

    .skeleton-card {
      padding: 20px;
      background: var(--color-surface);
      border-radius: var(--radius-md);
      border: 1px solid var(--color-border);
    }

    .skeleton-table {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .skeleton-row {
      display: flex;
      gap: 24px;
      align-items: center;
      padding: 12px 0;
      border-bottom: 1px solid var(--color-border);
    }

    .skeleton-chart {
      height: 220px;
      border-radius: var(--radius-md);
    }

    .skeleton-text {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
  `],
})
export class LoadingSkeletonComponent {
  @Input() type: SkeletonType = 'text';
  readonly rows = Array(6).fill(null);
}
