import { UpperCasePipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { SuggestedAction, ActionType, Priority } from '../../../core/models/suggested-action.model';

const ACTION_ICONS: Record<ActionType, string> = {
  outreach: '📞',
  discount: '🏷',
  review: '🔍',
  escalate: '⚠',
  upsell: '📈',
};

const PRIORITY_COLOR: Record<Priority, string> = {
  high: 'var(--color-red)',
  medium: 'var(--color-yellow)',
  low: 'var(--color-green)',
};

const PRIORITY_ORDER: Record<Priority, number> = { high: 0, medium: 1, low: 2 };

@Component({
  selector: 'app-actions-panel',
  standalone: true,
  imports: [UpperCasePipe],
  template: `
    <div class="actions">
      <h2 class="actions__title">Suggested Actions</h2>
      @if (sorted.length === 0) {
        <p class="actions__empty">No suggested actions available.</p>
      } @else {
        <div class="actions__list">
          @for (a of sorted; track a.id) {
            <div class="action-card">
              <div class="action-card__header">
                <span class="action-card__priority" [style.color]="priorityColor(a.priority)">
                  {{ a.priority | uppercase }}
                </span>
                <span class="action-card__icon">{{ actionIcon(a.actionType) }}</span>
                <span class="action-card__type">{{ a.actionType }}</span>
              </div>
              <div class="action-card__title">{{ a.title }}</div>
              @if (a.description) {
                <p class="action-card__desc">{{ a.description }}</p>
              }
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .actions {
      &__title {
        font-size: 13px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
        margin-bottom: 16px;
      }

      &__empty {
        color: var(--color-text-muted);
        font-size: 13px;
        padding: 16px 0;
      }

      &__list {
        display: flex;
        flex-direction: column;
        gap: 10px;
      }
    }

    .action-card {
      background: var(--color-surface-2);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: 14px 16px;

      &__header {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 6px;
      }

      &__priority {
        font-size: 10px;
        font-weight: 800;
        text-transform: uppercase;
        letter-spacing: 0.06em;
      }

      &__icon {
        font-size: 14px;
      }

      &__type {
        font-size: 11px;
        color: var(--color-text-muted);
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }

      &__title {
        font-size: 14px;
        font-weight: 700;
        color: var(--color-text);
        margin-bottom: 4px;
      }

      &__desc {
        font-size: 13px;
        color: var(--color-text-muted);
        line-height: 1.5;
      }
    }
  `],
})
export class ActionsPanelComponent {
  @Input() actions: SuggestedAction[] = [];

  get sorted(): SuggestedAction[] {
    return [...this.actions].sort(
      (a, b) => PRIORITY_ORDER[a.priority] - PRIORITY_ORDER[b.priority]
    );
  }

  actionIcon(type: ActionType): string {
    return ACTION_ICONS[type] ?? '•';
  }

  priorityColor(p: Priority): string {
    return PRIORITY_COLOR[p];
  }
}
