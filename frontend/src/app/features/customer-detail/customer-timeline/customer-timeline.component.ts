import { Component, Input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Interaction, Complaint } from '../../../core/models/customer.model';

type TimelineEntry =
  | { kind: 'interaction'; date: Date; data: Interaction }
  | { kind: 'complaint'; date: Date; data: Complaint };

const CHANNEL_ICON: Record<string, string> = {
  email: '✉',
  phone: '📞',
  meeting: '🤝',
  chat: '💬',
};

const SENTIMENT_COLOR: Record<string, string> = {
  positive: 'var(--color-green)',
  neutral: 'var(--color-text-muted)',
  negative: 'var(--color-red)',
};

@Component({
  selector: 'app-customer-timeline',
  standalone: true,
  imports: [DatePipe],
  template: `
    <div class="timeline">
      <div class="timeline__header">
        <h2 class="timeline__title">Activity Timeline</h2>
        <button class="collapse-btn" (click)="collapsed = !collapsed" [attr.aria-expanded]="!collapsed">
          {{ collapsed ? '▸' : '▾' }}
        </button>
      </div>
      @if (!collapsed) {
        @if (entries.length === 0) {
          <p class="timeline__empty">No interactions or complaints recorded.</p>
        } @else {
          <div class="timeline__list">
          @for (entry of entries; track $index; let even = $even) {
            <div class="entry" [class.entry--right]="even">
              <div class="entry__dot"></div>
              <div class="entry__card">
                @if (entry.kind === 'interaction') {
                  <div class="entry__meta">
                    <span class="entry__icon">{{ channelIcon(entry.data.channel) }}</span>
                    <span class="entry__date">{{ entry.date | date:'d MMM yyyy' }}</span>
                    @if (entry.data.direction) {
                      <span class="entry__badge">{{ entry.data.direction }}</span>
                    }
                    @if (entry.data.sentiment) {
                      <span class="entry__sentiment" [style.color]="sentimentColor(entry.data.sentiment)">
                        {{ entry.data.sentiment }}
                      </span>
                    }
                  </div>
                  <p class="entry__text">{{ entry.data.summary ?? 'No summary.' }}</p>
                } @else {
                  <div class="entry__meta">
                    <span class="entry__icon">⚠</span>
                    <span class="entry__date">{{ entry.date | date:'d MMM yyyy' }}</span>
                    @if (entry.data.severity) {
                      <span class="entry__badge entry__badge--severity" [class]="'sev-' + entry.data.severity">
                        {{ entry.data.severity }}
                      </span>
                    }
                    @if (entry.data.resolvedDate) {
                      <span class="entry__resolved">Resolved</span>
                    } @else {
                      <span class="entry__open">Open</span>
                    }
                  </div>
                  @if (entry.data.category) {
                    <div class="entry__category">{{ entry.data.category }}</div>
                  }
                  <p class="entry__text">{{ entry.data.description ?? 'No description.' }}</p>
                }
              </div>
            </div>
          }
        </div>
        }
      }
    </div>
  `,
  styles: [`
    .timeline {
      &__header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 8px;
        margin-bottom: 20px;
      }

      &__title {
        font-size: 13px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
        margin: 0;
      }

      &__empty {
        color: var(--color-text-muted);
        font-size: 13px;
        padding: 16px 0;
      }

      &__list {
        position: relative;
        display: flex;
        flex-direction: column;
        gap: 16px;
        padding: 0 0 0 16px;
        border-left: 2px solid var(--color-border);
      }
    }

    .entry {
      position: relative;

      &__dot {
        position: absolute;
        left: -21px;
        top: 12px;
        width: 10px;
        height: 10px;
        border-radius: 50%;
        background: var(--color-border);
        border: 2px solid var(--color-surface);
      }

      &__card {
        background: var(--color-surface-2);
        border: 1px solid var(--color-border);
        border-radius: var(--radius-md);
        padding: 12px 14px;
      }

      &__meta {
        display: flex;
        align-items: center;
        gap: 8px;
        flex-wrap: wrap;
        margin-bottom: 6px;
      }

      &__icon { font-size: 14px; }

      &__date {
        font-size: 12px;
        color: var(--color-text-muted);
      }

      &__badge {
        font-size: 10px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        padding: 2px 7px;
        border-radius: var(--radius-sm);
        background: var(--color-surface);
        color: var(--color-text-muted);
        border: 1px solid var(--color-border);

        &--severity {
          &.sev-high { background: var(--color-red); color: #fff; }
          &.sev-medium { background: var(--color-yellow); color: #000; }
          &.sev-low { background: var(--color-surface); color: var(--color-text-muted); }
        }
      }

      &__sentiment {
        font-size: 11px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.04em;
      }

      &__resolved { font-size: 11px; font-weight: 700; color: var(--color-green); text-transform: uppercase; }
      &__open     { font-size: 11px; font-weight: 700; color: var(--color-red); text-transform: uppercase; }

      &__category {
        font-size: 11px;
        font-weight: 600;
        color: var(--color-text-muted);
        text-transform: uppercase;
        letter-spacing: 0.05em;
        margin-bottom: 4px;
      }

      &__text {
        font-size: 13px;
        color: var(--color-text);
        line-height: 1.5;
      }
    }
  `],
})
export class CustomerTimelineComponent {
  @Input() set interactions(val: Interaction[]) {
    this._interactions = val;
    this.buildEntries();
  }

  @Input() set complaints(val: Complaint[]) {
    this._complaints = val;
    this.buildEntries();
  }

  collapsed = false;
  entries: TimelineEntry[] = [];
  private _interactions: Interaction[] = [];
  private _complaints: Complaint[] = [];

  private buildEntries(): void {
    const all: TimelineEntry[] = [
      ...this._interactions.map(i => ({
        kind: 'interaction' as const,
        date: new Date(i.interactionDate ?? 0),
        data: i,
      })),
      ...this._complaints.map(c => ({
        kind: 'complaint' as const,
        date: new Date(c.createdDate ?? 0),
        data: c,
      })),
    ];
    this.entries = all.sort((a, b) => b.date.getTime() - a.date.getTime());
  }

  channelIcon(ch: string | null): string {
    return CHANNEL_ICON[ch ?? ''] ?? '•';
  }

  sentimentColor(s: string): string {
    return SENTIMENT_COLOR[s] ?? 'var(--color-text-muted)';
  }
}
