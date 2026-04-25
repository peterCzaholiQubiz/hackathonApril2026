import { Component, DestroyRef, inject, Input, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PortfolioService, EnergyHeatmapCell } from '../../../core/services/portfolio.service';
import { LoadingSkeletonComponent } from '../../../shared/components/loading-skeleton/loading-skeleton.component';

const MONTH_ABBREVS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

export type EnergyDirection = 'Consumption' | 'Production';
export type EnergyColorScheme = 'blue' | 'green';

@Component({
  selector: 'app-energy-heatmap',
  standalone: true,
  imports: [LoadingSkeletonComponent],
  template: `
    <div class="energy-heatmap">
      <div class="energy-heatmap__header">
        <h2 class="energy-heatmap__title">{{ title }}</h2>
        <div class="unit-selector">
          <button class="unit-btn" [class.unit-btn--active]="unit === 'kWh'" (click)="setUnit('kWh')">kWh</button>
          <button class="unit-btn" [class.unit-btn--active]="unit === 'm3'"  (click)="setUnit('m3')">m³</button>
        </div>
      </div>

      @if (loading) {
        <app-loading-skeleton type="chart" />
      } @else if (cells.length === 0) {
        <p class="energy-heatmap__empty">No {{ direction.toLowerCase() }} data available for the selected unit.</p>
      } @else {
        <div class="grid-wrap">
          <div class="grid-row grid-row--header">
            <div class="grid-month-label"></div>
            @for (year of years; track year) {
              <div class="grid-year-label">{{ year }}</div>
            }
          </div>
          @for (month of months; track month.num) {
            <div class="grid-row">
              <div class="grid-month-label">{{ month.label }}</div>
              @for (year of years; track year) {
                <div
                  class="grid-cell"
                  [style.background]="cellColor(year, month.num)"
                  [title]="cellTooltip(year, month.num)">
                </div>
              }
            </div>
          }
        </div>
        <div class="legend-bar">
          <span class="legend-bar__label">Low</span>
          <div class="legend-bar__gradient" [class.legend-bar__gradient--blue]="colorScheme === 'blue'" [class.legend-bar__gradient--green]="colorScheme === 'green'"></div>
          <span class="legend-bar__label">High</span>
          <span class="legend-bar__unit">{{ unit }}</span>
        </div>
      }
    </div>
  `,
  styles: [`
    .energy-heatmap {
      &__header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 16px;
        gap: 12px;
        flex-wrap: wrap;
      }

      &__title {
        font-size: 13px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
      }

      &__empty {
        font-size: 13px;
        color: var(--color-text-muted);
        text-align: center;
        padding: 40px 0;
      }
    }

    .unit-selector {
      display: flex;
      gap: 4px;
    }

    .unit-btn {
      font-size: 11px;
      font-weight: 600;
      padding: 3px 8px;
      border-radius: var(--radius-sm, 4px);
      border: 1px solid var(--color-border);
      background: transparent;
      color: var(--color-text-muted);
      cursor: pointer;
      transition: all var(--duration-fast, 150ms);

      &--active {
        background: var(--color-primary, #6366f1);
        border-color: var(--color-primary, #6366f1);
        color: #fff;
      }
    }

    .grid-wrap {
      overflow-x: auto;
    }

    .grid-row {
      display: flex;
      align-items: center;
      gap: 3px;
      margin-bottom: 3px;

      &--header {
        margin-bottom: 4px;
      }
    }

    .grid-month-label {
      font-size: 10px;
      font-weight: 600;
      color: var(--color-text-muted);
      min-width: 28px;
      text-align: right;
      padding-right: 4px;
    }

    .grid-year-label {
      font-size: 10px;
      font-weight: 600;
      color: var(--color-text-muted);
      min-width: 32px;
      text-align: center;
    }

    .grid-cell {
      min-width: 32px;
      height: 22px;
      border-radius: 3px;
      background: var(--color-border);
      cursor: default;
      transition: opacity 150ms;

      &:hover { opacity: 0.75; }
    }

    .legend-bar {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-top: 12px;

      &__label {
        font-size: 10px;
        color: var(--color-text-muted);
      }

      &__gradient {
        height: 8px;
        width: 120px;
        border-radius: 4px;

        &--blue  { background: linear-gradient(to right, #dbeafe, #1d4ed8); }
        &--green { background: linear-gradient(to right, #dcfce7, #15803d); }
      }

      &__unit {
        font-size: 10px;
        color: var(--color-text-muted);
        margin-left: 4px;
      }
    }
  `],
})
export class EnergyHeatmapComponent implements OnInit {
  @Input() direction: EnergyDirection = 'Consumption';
  @Input() colorScheme: EnergyColorScheme = 'blue';

  private readonly portfolioSvc = inject(PortfolioService);
  private readonly destroyRef = inject(DestroyRef);

  loading = true;
  unit: 'kWh' | 'm3' = 'kWh';
  cells: EnergyHeatmapCell[] = [];

  years: number[] = [];
  readonly months = MONTH_ABBREVS.map((label, i) => ({ label, num: i + 1 }));

  private cellMap = new Map<string, number>();
  private maxValue = 0;

  get title(): string {
    return this.direction === 'Consumption' ? 'Consumption Heatmap' : 'Production Heatmap';
  }

  ngOnInit(): void {
    this.load();
  }

  setUnit(unit: 'kWh' | 'm3'): void {
    this.unit = unit;
    this.load();
  }

  cellColor(year: number, month: number): string {
    const key = `${year}-${month}`;
    const value = this.cellMap.get(key);
    if (value == null || this.maxValue === 0) return 'var(--color-border)';

    const intensity = Math.min(value / this.maxValue, 1);
    return this.colorScheme === 'blue'
      ? this.interpolateBlue(intensity)
      : this.interpolateGreen(intensity);
  }

  cellTooltip(year: number, month: number): string {
    const key = `${year}-${month}`;
    const value = this.cellMap.get(key);
    if (value == null) return `${MONTH_ABBREVS[month - 1]} ${year}: no data`;
    return `${MONTH_ABBREVS[month - 1]} ${year}: ${value.toLocaleString(undefined, { maximumFractionDigits: 0 })} ${this.unit}`;
  }

  private load(): void {
    this.loading = true;
    this.portfolioSvc.getEnergyHeatmap(this.unit, this.direction)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.cells = res.data ?? [];
          this.buildGrid();
          this.loading = false;
        },
        error: () => { this.loading = false; },
      });
  }

  private buildGrid(): void {
    this.cellMap = new Map(this.cells.map(c => [`${c.year}-${c.month}`, c.total]));
    this.years = [...new Set(this.cells.map(c => c.year))].sort((a, b) => a - b);
    this.maxValue = this.cells.length > 0 ? Math.max(...this.cells.map(c => c.total)) : 0;
  }

  private interpolateBlue(t: number): string {
    const r = Math.round(219 - t * (219 - 29));
    const g = Math.round(234 - t * (234 - 78));
    const b = Math.round(254 - t * (254 - 216));
    return `rgb(${r},${g},${b})`;
  }

  private interpolateGreen(t: number): string {
    const r = Math.round(220 - t * (220 - 21));
    const g = Math.round(252 - t * (252 - 128));
    const b = Math.round(231 - t * (231 - 61));
    return `rgb(${r},${g},${b})`;
  }
}
