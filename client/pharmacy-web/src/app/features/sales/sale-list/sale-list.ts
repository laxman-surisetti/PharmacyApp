import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { describeApiError } from '../../../core/api-error';
import { formatMoney, formatTimestamp } from '../../../core/format';
import { Sale } from '../../../core/models';
import { SaleService } from '../../../core/sale.service';

/** FR-06 - the sale record. Read-only: a receipt that can be edited is not a receipt. */
@Component({
  selector: 'app-sale-list',
  imports: [RouterLink],
  templateUrl: './sale-list.html',
  styleUrl: './sale-list.css',
})
export class SaleListComponent implements OnInit {
  private readonly sales = inject(SaleService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly formatMoney = formatMoney;
  protected readonly formatTimestamp = formatTimestamp;

  protected readonly rows = signal<Sale[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly expandedId = signal<string | null>(null);

  protected readonly page = signal(1);
  protected readonly pageSize = 10;
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);

  protected readonly pageTotal = computed(() => this.rows().reduce((sum, sale) => sum + sale.totalAmount, 0));
  protected readonly canPrevious = computed(() => this.page() > 1);
  protected readonly canNext = computed(() => this.page() < this.totalPages());

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.sales
      .search(this.page(), this.pageSize)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.rows.set(result.items);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.error.set(describeApiError(err, 'Could not load the sales history.'));
          this.rows.set([]);
          this.loading.set(false);
        },
      });
  }

  protected toggle(id: string): void {
    this.expandedId.set(this.expandedId() === id ? null : id);
  }

  protected goToPage(page: number): void {
    if (page < 1 || (this.totalPages() > 0 && page > this.totalPages())) {
      return;
    }

    this.page.set(page);
    this.load();
  }

  protected lineSummary(sale: Sale): string {
    const units = sale.lines.reduce((sum, line) => sum + line.quantity, 0);
    const items = sale.lines.length;
    return `${items} item${items === 1 ? '' : 's'} · ${units} unit${units === 1 ? '' : 's'}`;
  }
}
