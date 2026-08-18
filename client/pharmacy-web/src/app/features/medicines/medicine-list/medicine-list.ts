import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';

import { describeApiError } from '../../../core/api-error';
import { describeDaysToExpiry, formatIsoDate, formatMoney } from '../../../core/format';
import { MedicineService } from '../../../core/medicine.service';
import {
  InventorySummary,
  MedicineListItem,
  MedicineSortField,
  RowSeverity,
  SortDirection,
} from '../../../core/models';

/**
 * FR-01 / FR-02 - the stock grid.
 *
 * Note what this component does *not* do: it does not decide which rows are red or yellow.
 * The server returns `rowSeverity` per row and this component only maps it to a CSS class.
 * That keeps one copy of the 30-day / 10-unit rule, and means a workstation with a wrong
 * clock cannot mis-colour a row.
 */
@Component({
  selector: 'app-medicine-list',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './medicine-list.html',
  styleUrl: './medicine-list.css',
})
export class MedicineListComponent implements OnInit {
  private readonly medicines = inject(MedicineService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly formatIsoDate = formatIsoDate;
  protected readonly formatMoney = formatMoney;
  protected readonly describeDaysToExpiry = describeDaysToExpiry;

  /** FR-07 - debounced so a five letter search is one request, not five. */
  protected readonly searchControl = new FormControl('', { nonNullable: true });

  protected readonly rows = signal<MedicineListItem[]>([]);
  protected readonly summary = signal<InventorySummary | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly page = signal(1);
  protected readonly pageSize = signal(10);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);

  protected readonly sortBy = signal<MedicineSortField>('severity');
  protected readonly sortDir = signal<SortDirection>('desc');

  /** Id of the row whose delete is awaiting an inline confirm. */
  protected readonly pendingDeleteId = signal<string | null>(null);

  protected readonly rangeLabel = computed(() => {
    const total = this.totalCount();
    if (total === 0) {
      return 'No medicines';
    }

    const first = (this.page() - 1) * this.pageSize() + 1;
    const last = Math.min(this.page() * this.pageSize(), total);
    return `${first}-${last} of ${total}`;
  });

  protected readonly canPrevious = computed(() => this.page() > 1);
  protected readonly canNext = computed(() => this.page() < this.totalPages());

  ngOnInit(): void {
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.page.set(1);
        this.load();
      });

    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.medicines
      .search({
        search: this.searchControl.value,
        page: this.page(),
        pageSize: this.pageSize(),
        sortBy: this.sortBy(),
        sortDir: this.sortDir(),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.rows.set(result.items);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);

          // A delete or a filter change can leave us past the end of the list.
          if (result.totalPages > 0 && this.page() > result.totalPages) {
            this.page.set(result.totalPages);
            this.load();
            return;
          }

          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.error.set(describeApiError(err, 'Could not load the medicine list.'));
          this.rows.set([]);
          this.loading.set(false);
        },
      });

    this.medicines
      .summary()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (summary) => this.summary.set(summary),
        error: () => this.summary.set(null),
      });
  }

  protected sort(field: MedicineSortField): void {
    if (this.sortBy() === field) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortBy.set(field);
      // Severity reads best worst-first; everything else reads best A-Z / smallest first.
      this.sortDir.set(field === 'severity' ? 'desc' : 'asc');
    }

    this.page.set(1);
    this.load();
  }

  protected sortIndicator(field: MedicineSortField): string {
    if (this.sortBy() !== field) {
      return '';
    }

    return this.sortDir() === 'asc' ? '▲' : '▼';
  }

  protected ariaSort(field: MedicineSortField): 'ascending' | 'descending' | 'none' {
    if (this.sortBy() !== field) {
      return 'none';
    }

    return this.sortDir() === 'asc' ? 'ascending' : 'descending';
  }

  protected goToPage(page: number): void {
    if (page < 1 || (this.totalPages() > 0 && page > this.totalPages())) {
      return;
    }

    this.page.set(page);
    this.load();
  }

  protected changePageSize(value: string): void {
    this.pageSize.set(Number(value) || 10);
    this.page.set(1);
    this.load();
  }

  protected clearSearch(): void {
    this.searchControl.setValue('');
  }

  protected confirmDelete(id: string): void {
    this.pendingDeleteId.set(id);
  }

  protected cancelDelete(): void {
    this.pendingDeleteId.set(null);
  }

  protected remove(row: MedicineListItem): void {
    this.medicines
      .remove(row.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.pendingDeleteId.set(null);
          this.notice.set(`Removed "${row.fullName}" from the catalogue.`);
          this.load();
        },
        error: (err: unknown) => {
          this.pendingDeleteId.set(null);
          this.error.set(describeApiError(err, 'Could not remove that medicine.'));
        },
      });
  }

  /** The only place row severity turns into presentation. */
  protected rowClass(severity: RowSeverity): string {
    switch (severity) {
      case 'Critical':
        return 'row-critical';
      case 'Warning':
        return 'row-warning';
      default:
        return '';
    }
  }

  /**
   * Colour alone is not an accessible signal (WCAG 2.2 AA), so every coloured row also
   * carries a text badge saying why it is coloured.
   */
  protected statusLabel(row: MedicineListItem): string {
    if (row.expiryStatus === 'Expired') {
      return 'Expired';
    }

    if (row.expiryStatus === 'ExpiringSoon') {
      return 'Expiring soon';
    }

    if (row.stockStatus === 'OutOfStock') {
      return 'Out of stock';
    }

    if (row.stockStatus === 'Low') {
      return 'Low stock';
    }

    return 'OK';
  }

  protected badgeClass(row: MedicineListItem): string {
    switch (row.rowSeverity) {
      case 'Critical':
        return 'badge critical';
      case 'Warning':
        return 'badge warning';
      default:
        return 'badge ok';
    }
  }
}
