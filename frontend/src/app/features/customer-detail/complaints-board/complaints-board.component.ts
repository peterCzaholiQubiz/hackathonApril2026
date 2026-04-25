import { DatePipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { Complaint } from '../../../core/models/customer.model';

type ComplaintSeverity = 'high' | 'medium' | 'low';

interface ComplaintLane {
  severity: ComplaintSeverity;
  label: string;
  tone: string;
  complaints: Complaint[];
  openCount: number;
}

const SEVERITY_CONFIG: Record<ComplaintSeverity, { label: string; tone: string }> = {
  high: { label: 'High', tone: 'Escalate now' },
  medium: { label: 'Medium', tone: 'Watch closely' },
  low: { label: 'Low', tone: 'Background noise' },
};

export function normalizeComplaintSeverity(severity: string | null | undefined): ComplaintSeverity {
  const normalized = severity?.trim().toLowerCase();

  if (normalized === 'high' || normalized === 'critical') {
    return 'high';
  }

  if (normalized === 'medium') {
    return 'medium';
  }

  return 'low';
}

function parseComplaintDate(value: string | null): number {
  return value ? new Date(value).getTime() : 0;
}

export function buildComplaintLanes(complaints: Complaint[]): ComplaintLane[] {
  const grouped: Record<ComplaintSeverity, Complaint[]> = {
    high: [],
    medium: [],
    low: [],
  };

  for (const complaint of complaints) {
    const severity = normalizeComplaintSeverity(complaint.severity);
    grouped[severity] = [...grouped[severity], complaint];
  }

  return (Object.keys(SEVERITY_CONFIG) as ComplaintSeverity[]).map((severity) => {
    const laneComplaints = [...grouped[severity]].sort(
      (left, right) => parseComplaintDate(right.createdDate) - parseComplaintDate(left.createdDate),
    );

    return {
      severity,
      label: SEVERITY_CONFIG[severity].label,
      tone: SEVERITY_CONFIG[severity].tone,
      complaints: laneComplaints,
      openCount: laneComplaints.filter((complaint) => !complaint.resolvedDate).length,
    };
  });
}

@Component({
  selector: 'app-complaints-board',
  standalone: true,
  imports: [DatePipe],
  template: `
    <div class="complaints-board">
      <div class="complaints-board__hero">
        <div>
          <p class="complaints-board__eyebrow">Complaint Radar</p>
          <h2 class="complaints-board__title">Severity lanes for fast triage</h2>
          @if (!collapsed) {
            <p class="complaints-board__lede">
              Open issues float to the top, resolved cases stay visible, and the color grouping makes escalation pressure obvious at a glance.
            </p>
          }
        </div>

        <div class="complaints-board__hero-right">
          @if (!collapsed) {
            <div class="complaints-board__stats">
              <article class="stat-card stat-card--total">
                <span class="stat-card__label">Total</span>
                <strong class="stat-card__value">{{ complaints.length }}</strong>
              </article>
              <article class="stat-card stat-card--open">
                <span class="stat-card__label">Open</span>
                <strong class="stat-card__value">{{ openCount }}</strong>
              </article>
              <article class="stat-card stat-card--resolved">
                <span class="stat-card__label">Resolved</span>
                <strong class="stat-card__value">{{ resolvedCount }}</strong>
              </article>
            </div>
          }
          <button class="collapse-btn" (click)="collapsed = !collapsed" [attr.aria-expanded]="!collapsed">
            {{ collapsed ? '▸' : '▾' }}
          </button>
        </div>
      </div>

      @if (!collapsed) {
        @if (complaints.length === 0) {
        <div class="complaints-board__empty">
          <h3>No complaints on record</h3>
          <p>This customer has a clean slate right now. Severity lanes will populate as soon as complaints arrive from the API.</p>
        </div>
      } @else {
        <div class="complaints-board__lanes">
          @for (lane of lanes; track lane.severity) {
            <section class="lane" [attr.data-severity]="lane.severity">
              <header class="lane__header">
                <div>
                  <p class="lane__eyebrow">{{ lane.tone }}</p>
                  <h3 class="lane__title">{{ lane.label }}</h3>
                </div>
                <div class="lane__metrics">
                  <span class="lane__count">{{ lane.complaints.length }}</span>
                  <span class="lane__open">{{ lane.openCount }} open</span>
                </div>
              </header>

              @if (lane.complaints.length === 0) {
                <p class="lane__empty">No {{ lane.label.toLowerCase() }} severity complaints for this customer.</p>
              } @else {
                <div class="lane__list">
                  @for (complaint of lane.complaints; track complaint.id) {
                    <article class="complaint-card" [class.complaint-card--resolved]="!!complaint.resolvedDate">
                      <div class="complaint-card__meta">
                        <span class="complaint-card__date">{{ complaint.createdDate | date:'d MMM yyyy' }}</span>
                        <span class="complaint-card__status" [class.complaint-card__status--resolved]="!!complaint.resolvedDate">
                          {{ complaint.resolvedDate ? 'Resolved' : 'Open' }}
                        </span>
                      </div>

                      <h4 class="complaint-card__category">{{ complaint.category || 'General issue' }}</h4>
                      <p class="complaint-card__description">{{ complaint.description || 'No complaint description provided.' }}</p>

                      <div class="complaint-card__footer">
                        @if (complaint.resolvedDate) {
                          <span>Closed {{ complaint.resolvedDate | date:'d MMM yyyy' }}</span>
                        } @else {
                          <span>Needs follow-up</span>
                        }
                        <span class="complaint-card__severity">{{ lane.label }} severity</span>
                      </div>
                    </article>
                  }
                </div>
              }
            </section>
          }
        </div>
      }
      }
    </div>
  `,
  styles: [`
    :host {
      --_faint-border: rgba(255, 255, 255, 0.08);
      --_surface-xs: rgba(15, 17, 23, 0.38);
      --_surface-sm: rgba(15, 17, 23, 0.42);
      --_surface-md: rgba(15, 17, 23, 0.60);
      --_surface-lg: rgba(15, 17, 23, 0.86);
      --_empty-gradient-stop: rgba(26, 29, 39, 0.40);
      --_eyebrow-on-dark: rgba(255, 255, 255, 0.72);
      --_inset-highlight: rgba(255, 255, 255, 0.03);
      --_status-open-text: #fecaca;
      --_status-resolved-text: #bbf7d0;
    }

    :host-context([data-theme='light']) {
      --_faint-border: var(--color-border);
      --_surface-xs: rgba(0, 0, 0, 0.04);
      --_surface-sm: rgba(0, 0, 0, 0.04);
      --_surface-md: rgba(0, 0, 0, 0.05);
      --_surface-lg: rgba(0, 0, 0, 0.06);
      --_empty-gradient-stop: rgba(0, 0, 0, 0.04);
      --_eyebrow-on-dark: var(--color-text-muted);
      --_inset-highlight: transparent;
      --_status-open-text: #b91c1c;
      --_status-resolved-text: #15803d;
    }

    .complaints-board {
      display: flex;
      flex-direction: column;
      gap: 20px;

      &__hero {
        display: grid;
        grid-template-columns: minmax(0, 1.8fr) minmax(280px, 1fr);
        gap: 16px;
        padding: 20px;
        border: 1px solid var(--_faint-border);
        border-radius: var(--radius-lg);
        background:
          radial-gradient(circle at top left, rgba(245, 158, 11, 0.18), transparent 35%),
          linear-gradient(135deg, rgba(34, 197, 94, 0.08), rgba(239, 68, 68, 0.12));

        @media (max-width: 960px) {
          grid-template-columns: 1fr;
        }
      }

      &__hero-right {
        display: flex;
        flex-direction: column;
        align-items: flex-end;
        gap: 12px;
      }

      &__eyebrow {
        margin: 0 0 8px;
        font-size: 11px;
        font-weight: 800;
        letter-spacing: 0.14em;
        text-transform: uppercase;
        color: var(--_eyebrow-on-dark);
      }

      &__title {
        margin: 0;
        font-size: 24px;
        line-height: 1.1;
      }

      &__lede {
        margin: 10px 0 0;
        max-width: 60ch;
        font-size: 14px;
        line-height: 1.7;
        color: var(--color-text-muted);
      }

      &__stats {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: 12px;
      }

      &__lanes {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: 16px;

        @media (max-width: 1100px) {
          grid-template-columns: 1fr;
        }
      }

      &__empty {
        padding: 32px 24px;
        border: 1px dashed var(--color-border);
        border-radius: var(--radius-lg);
        background: linear-gradient(180deg, rgba(34, 197, 94, 0.08), var(--_empty-gradient-stop));

        h3 {
          margin: 0 0 8px;
          font-size: 18px;
        }

        p {
          margin: 0;
          color: var(--color-text-muted);
          line-height: 1.6;
        }
      }
    }

    .stat-card {
      padding: 16px;
      border-radius: var(--radius-md);
      border: 1px solid var(--_faint-border);
      background: var(--_surface-sm);

      &__label {
        display: block;
        margin-bottom: 8px;
        font-size: 11px;
        font-weight: 700;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        color: var(--color-text-muted);
      }

      &__value {
        font-size: 28px;
        line-height: 1;
      }

      &--open .stat-card__value { color: var(--color-red); }
      &--resolved .stat-card__value { color: var(--color-green); }
    }

    .lane {
      --lane-accent: var(--color-border);
      --lane-bg: rgba(34, 38, 58, 0.72);

      display: flex;
      flex-direction: column;
      gap: 14px;
      min-width: 0;
      padding: 18px;
      border-radius: var(--radius-lg);
      border: 1px solid var(--_faint-border);
      background: linear-gradient(180deg, var(--lane-bg), var(--_surface-lg));
      box-shadow: inset 0 1px 0 var(--_inset-highlight);

      &[data-severity='high'] {
        --lane-accent: var(--color-red);
        --lane-bg: rgba(239, 68, 68, 0.12);
      }

      &[data-severity='medium'] {
        --lane-accent: var(--color-yellow);
        --lane-bg: rgba(245, 158, 11, 0.12);
      }

      &[data-severity='low'] {
        --lane-accent: var(--color-green);
        --lane-bg: rgba(34, 197, 94, 0.12);
      }

      &__header {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: 12px;
        padding-bottom: 14px;
        border-bottom: 1px solid var(--_faint-border);
      }

      &__eyebrow {
        margin: 0 0 6px;
        font-size: 10px;
        font-weight: 800;
        letter-spacing: 0.14em;
        text-transform: uppercase;
        color: var(--lane-accent);
      }

      &__title {
        margin: 0;
        font-size: 20px;
      }

      &__metrics {
        display: flex;
        flex-direction: column;
        align-items: flex-end;
        gap: 2px;
      }

      &__count {
        font-size: 26px;
        font-weight: 800;
        line-height: 1;
      }

      &__open {
        font-size: 12px;
        color: var(--color-text-muted);
      }

      &__list {
        display: flex;
        flex-direction: column;
        gap: 12px;
      }

      &__empty {
        margin: 0;
        padding: 18px 16px;
        border-radius: var(--radius-md);
        background: var(--_surface-xs);
        color: var(--color-text-muted);
        line-height: 1.6;
      }
    }

    .complaint-card {
      display: flex;
      flex-direction: column;
      gap: 10px;
      padding: 14px;
      border-radius: var(--radius-md);
      border: 1px solid var(--_faint-border);
      background: var(--_surface-md);

      &--resolved {
        opacity: 0.82;
      }

      &__meta,
      &__footer {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 12px;
        flex-wrap: wrap;
      }

      &__date,
      &__footer {
        font-size: 12px;
        color: var(--color-text-muted);
      }

      &__status {
        padding: 4px 10px;
        border-radius: 999px;
        background: rgba(239, 68, 68, 0.18);
        color: var(--_status-open-text);
        font-size: 10px;
        font-weight: 800;
        letter-spacing: 0.08em;
        text-transform: uppercase;

        &--resolved {
          background: rgba(34, 197, 94, 0.18);
          color: var(--_status-resolved-text);
        }
      }

      &__category {
        margin: 0;
        font-size: 16px;
        line-height: 1.3;
      }

      &__description {
        margin: 0;
        font-size: 13px;
        line-height: 1.6;
        color: var(--color-text-muted);
      }

      &__severity {
        color: var(--color-text);
        font-weight: 700;
      }
    }
  `],
})
export class ComplaintsBoardComponent {
  @Input() set complaints(value: Complaint[]) {
    this._complaints = [...value];
    this.lanes = buildComplaintLanes(this._complaints);
  }

  collapsed = false;
  lanes: ComplaintLane[] = buildComplaintLanes([]);
  private _complaints: Complaint[] = [];

  get complaints(): Complaint[] {
    return this._complaints;
  }

  get openCount(): number {
    return this._complaints.filter((complaint) => !complaint.resolvedDate).length;
  }

  get resolvedCount(): number {
    return this._complaints.length - this.openCount;
  }
}