import { Component, EventEmitter, Output, OnInit, OnDestroy, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { CustomerListParams, CustomerService } from '../../../core/services/customer.service';

@Component({
  selector: 'app-customer-filters',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="filters">
      <div class="filters__group">
        <label class="filters__label">Search</label>
        <input
          class="filters__input"
          type="text"
          placeholder="Name or company..."
          [ngModel]="searchValue"
          (ngModelChange)="onSearchChange($event)"
        />
      </div>

      <div class="filters__group">
        <label class="filters__label">Segment</label>
        <select class="filters__select" [(ngModel)]="segment" (ngModelChange)="emit()">
          <option value="">All Segments</option>
          @for (seg of segments; track seg) {
            <option [value]="seg">{{ seg }}</option>
          }
        </select>
      </div>

      <div class="filters__group">
        <label class="filters__label">Heat Level</label>
        <div class="filters__toggles">
          @for (level of heatLevels; track level.value) {
            <button
              class="toggle"
              [class.toggle--active]="heatLevel === level.value"
              [class]="'toggle toggle--' + (level.value || 'all')"
              (click)="setHeatLevel(level.value)"
            >{{ level.label }}</button>
          }
        </div>
      </div>

      <div class="filters__group">
        <label class="filters__label">Sort By</label>
        <div class="filters__row">
          <select class="filters__select" [(ngModel)]="sortBy" (ngModelChange)="emit()">
            <option value="overallScore">Overall Score</option>
            <option value="churnScore">Churn</option>
            <option value="paymentScore">Payment</option>
            <option value="marginScore">Margin</option>
            <option value="name">Name</option>
          </select>
          <button class="filters__dir-btn" (click)="toggleDir()">
            {{ sortDir === 'desc' ? '↓ DESC' : '↑ ASC' }}
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .filters {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
      align-items: flex-end;
      padding: 16px 20px;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      margin-bottom: 20px;

      &__group {
        display: flex;
        flex-direction: column;
        gap: 6px;
        min-width: 160px;
      }

      &__label {
        font-size: 10px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: var(--color-text-muted);
      }

      &__input, &__select {
        background: var(--color-surface-2);
        border: 1px solid var(--color-border);
        border-radius: var(--radius-sm);
        color: var(--color-text);
        font-size: 13px;
        padding: 7px 10px;
        outline: none;
        transition: border-color var(--duration-fast);

        &:focus { border-color: var(--color-text-muted); }
      }

      &__toggles {
        display: flex;
        gap: 4px;
      }

      &__row {
        display: flex;
        gap: 6px;
      }

      &__dir-btn {
        background: var(--color-surface-2);
        border: 1px solid var(--color-border);
        border-radius: var(--radius-sm);
        color: var(--color-text);
        font-size: 12px;
        font-weight: 600;
        padding: 7px 10px;
        cursor: pointer;
        white-space: nowrap;
        transition: background var(--duration-fast);

        &:hover { background: var(--color-border); }
      }
    }

    .toggle {
      padding: 6px 10px;
      border-radius: var(--radius-sm);
      border: 1px solid var(--color-border);
      background: var(--color-surface-2);
      color: var(--color-text-muted);
      font-size: 12px;
      font-weight: 600;
      cursor: pointer;
      transition: all var(--duration-fast);

      &--active, &:hover { border-color: currentColor; }

      &--all.toggle--active   { color: var(--color-text); background: var(--color-border); }
      &--green.toggle--active { color: #fff; background: var(--color-green); border-color: var(--color-green); }
      &--yellow.toggle--active{ color: #000; background: var(--color-yellow); border-color: var(--color-yellow); }
      &--red.toggle--active   { color: #fff; background: var(--color-red); border-color: var(--color-red); }
    }
  `],
})
export class CustomerFiltersComponent implements OnInit, OnDestroy {
  @Output() filtersChanged = new EventEmitter<CustomerListParams>();

  private readonly customerSvc = inject(CustomerService);

  searchValue = '';
  segment = '';
  heatLevel = '';
  sortBy = 'overallScore';
  sortDir = 'desc';
  segments: string[] = [];

  readonly heatLevels = [
    { value: '', label: 'All' },
    { value: 'green', label: 'Healthy' },
    { value: 'yellow', label: 'Watch' },
    { value: 'red', label: 'At Risk' },
  ];

  private readonly searchSubject = new Subject<string>();
  private readonly destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntil(this.destroy$),
    ).subscribe(() => this.emit());

    this.customerSvc.getSegments()
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: (res) => { this.segments = res.data ?? []; } });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSearchChange(val: string): void {
    this.searchValue = val;
    this.searchSubject.next(val);
  }

  setHeatLevel(level: string): void {
    this.heatLevel = level;
    this.emit();
  }

  toggleDir(): void {
    this.sortDir = this.sortDir === 'desc' ? 'asc' : 'desc';
    this.emit();
  }

  emit(): void {
    this.filtersChanged.emit({
      search: this.searchValue || undefined,
      segment: this.segment || undefined,
      heatLevel: this.heatLevel || undefined,
      sortBy: this.sortBy,
      sortDir: this.sortDir,
    });
  }
}
