import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { describeApiError } from '../../../core/api-error';
import { formatIsoDate, formatMoney, formatTimestamp } from '../../../core/format';
import { MedicineService } from '../../../core/medicine.service';
import { MedicineListItem, Sale } from '../../../core/models';
import { SaleService } from '../../../core/sale.service';

interface BasketLine {
  medicineId: string;
  name: string;
  brand: string;
  unitPrice: number;
  quantity: number;
  available: number;
}

/**
 * FR-06 - record a sale.
 *
 * The basket is built client-side, but nothing about stock is decided here: the quantity
 * check shown while typing is a courtesy, and the server re-checks and rejects with 409 if
 * another till got there first. Client-side validation makes the UI pleasant; the server
 * is what makes it correct.
 */
@Component({
  selector: 'app-sale-new',
  imports: [FormsModule, RouterLink],
  templateUrl: './sale-new.html',
  styleUrl: './sale-new.css',
})
export class SaleNewComponent implements OnInit {
  private readonly medicines = inject(MedicineService);
  private readonly sales = inject(SaleService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly formatMoney = formatMoney;
  protected readonly formatIsoDate = formatIsoDate;
  protected readonly formatTimestamp = formatTimestamp;

  protected readonly catalogue = signal<MedicineListItem[]>([]);
  protected readonly basket = signal<BasketLine[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly lineError = signal<string | null>(null);
  protected readonly lastSale = signal<Sale | null>(null);

  protected selectedId = '';
  protected quantity = 1;
  protected soldBy = '';
  protected notes = '';

  /** Mirror of the ngModel value, so `selected` can be a computed signal. */
  private readonly selectedIdSignal = signal('');

  protected readonly total = computed(() =>
    this.basket().reduce((sum, line) => sum + line.unitPrice * line.quantity, 0),
  );

  protected readonly selected = computed(
    () => this.catalogue().find((m) => m.id === this.selectedIdSignal()) ?? null,
  );

  ngOnInit(): void {
    this.loadCatalogue(() => {
      const preselected = this.route.snapshot.queryParamMap.get('medicineId');
      if (preselected && this.catalogue().some((m) => m.id === preselected)) {
        this.selectedId = preselected;
        this.selectedIdSignal.set(preselected);
      }
    });
  }

  protected onSelectionChange(value: string): void {
    this.selectedId = value;
    this.selectedIdSignal.set(value);
    this.lineError.set(null);
  }

  private loadCatalogue(afterLoad?: () => void): void {
    this.loading.set(true);

    this.medicines
      .search({ page: 1, pageSize: 200, sortBy: 'name', sortDir: 'asc' })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.catalogue.set(result.items);
          this.loading.set(false);
          afterLoad?.();
        },
        error: (err: unknown) => {
          this.error.set(describeApiError(err, 'Could not load the medicine list.'));
          this.loading.set(false);
        },
      });
  }

  /** Units of a medicine already sitting in the basket. */
  private inBasket(medicineId: string): number {
    return this.basket().find((line) => line.medicineId === medicineId)?.quantity ?? 0;
  }

  protected addLine(): void {
    this.lineError.set(null);

    const medicine = this.catalogue().find((m) => m.id === this.selectedId);
    if (!medicine) {
      this.lineError.set('Choose a medicine first.');
      return;
    }

    const quantity = Number(this.quantity);
    if (!Number.isInteger(quantity) || quantity < 1) {
      this.lineError.set('Quantity must be a whole number of at least 1.');
      return;
    }

    const remaining = medicine.quantity - this.inBasket(medicine.id);
    if (quantity > remaining) {
      this.lineError.set(
        remaining <= 0
          ? `All ${medicine.quantity} unit(s) of ${medicine.fullName} are already in this sale.`
          : `Only ${remaining} more unit(s) of ${medicine.fullName} are available.`,
      );
      return;
    }

    this.basket.update((lines) => {
      const existing = lines.find((line) => line.medicineId === medicine.id);
      if (existing) {
        return lines.map((line) =>
          line.medicineId === medicine.id ? { ...line, quantity: line.quantity + quantity } : line,
        );
      }

      return [
        ...lines,
        {
          medicineId: medicine.id,
          name: medicine.fullName,
          brand: medicine.brand,
          unitPrice: medicine.price,
          quantity,
          available: medicine.quantity,
        },
      ];
    });

    this.quantity = 1;
    this.lastSale.set(null);
  }

  protected removeLine(medicineId: string): void {
    this.basket.update((lines) => lines.filter((line) => line.medicineId !== medicineId));
  }

  protected submit(): void {
    this.error.set(null);

    if (this.basket().length === 0) {
      this.error.set('Add at least one medicine before recording the sale.');
      return;
    }

    this.saving.set(true);

    this.sales
      .create({
        lines: this.basket().map((line) => ({ medicineId: line.medicineId, quantity: line.quantity })),
        soldBy: this.soldBy.trim() === '' ? null : this.soldBy.trim(),
        notes: this.notes.trim() === '' ? null : this.notes.trim(),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (sale) => {
          this.saving.set(false);
          this.lastSale.set(sale);
          this.basket.set([]);
          this.notes = '';
          // Stock has moved, so the dropdown's "in stock" figures are now stale.
          this.loadCatalogue();
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.error.set(describeApiError(err, 'Could not record the sale.'));
          // The server may have rejected because another till sold the same unit.
          this.loadCatalogue();
        },
      });
  }
}
