import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of } from 'rxjs';

import { MedicineService } from '../../../core/medicine.service';
import { InventorySummary, MedicineListItem, PagedResult } from '../../../core/models';
import { MedicineListComponent } from './medicine-list';

function row(overrides: Partial<MedicineListItem>): MedicineListItem {
  return {
    id: crypto.randomUUID(),
    fullName: 'Paracetamol 500mg Tablet',
    brand: 'HealWell',
    expiryDate: '2027-11-30',
    daysToExpiry: 400,
    quantity: 250,
    price: 3.4,
    expiryStatus: 'Ok',
    stockStatus: 'Ok',
    rowSeverity: 'Normal',
    version: 1,
    ...overrides,
  };
}

class FakeMedicineService {
  rows: MedicineListItem[] = [];

  search(): Observable<PagedResult<MedicineListItem>> {
    return of({
      items: this.rows,
      page: 1,
      pageSize: 10,
      totalCount: this.rows.length,
      totalPages: 1,
    });
  }

  summary(): Observable<InventorySummary> {
    return of({
      totalMedicines: this.rows.length,
      expiredCount: 0,
      expiringSoonCount: 0,
      lowStockCount: 0,
      outOfStockCount: 0,
      totalStockValue: 0,
    });
  }
}

describe('MedicineListComponent', () => {
  let fake: FakeMedicineService;

  beforeEach(async () => {
    fake = new FakeMedicineService();

    await TestBed.configureTestingModule({
      imports: [MedicineListComponent],
      providers: [provideRouter([]), { provide: MedicineService, useValue: fake }],
    }).compileComponents();
  });

  async function render() {
    const fixture = TestBed.createComponent(MedicineListComponent);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('paints a red row for a medicine the server flagged as Critical', async () => {
    fake.rows = [
      row({
        fullName: 'Amoxicillin 500mg Capsule',
        daysToExpiry: 18,
        expiryStatus: 'ExpiringSoon',
        rowSeverity: 'Critical',
      }),
    ];

    const element = await render();
    const tableRow = element.querySelector('tbody tr');

    expect(tableRow?.classList.contains('row-critical')).toBe(true);
    expect(tableRow?.textContent).toContain('Expiring soon');
  });

  it('paints a yellow row for low stock', async () => {
    fake.rows = [row({ quantity: 6, stockStatus: 'Low', rowSeverity: 'Warning' })];

    const element = await render();
    const tableRow = element.querySelector('tbody tr');

    expect(tableRow?.classList.contains('row-warning')).toBe(true);
    expect(tableRow?.textContent).toContain('Low stock');
  });

  it('leaves a healthy row uncoloured', async () => {
    fake.rows = [row({})];

    const element = await render();
    const tableRow = element.querySelector('tbody tr');

    expect(tableRow?.classList.contains('row-critical')).toBe(false);
    expect(tableRow?.classList.contains('row-warning')).toBe(false);
  });

  it('does not render the notes attribute in the grid (FR-02)', async () => {
    fake.rows = [row({})];

    const element = await render();
    const headers = Array.from(element.querySelectorAll('thead th')).map((th) =>
      (th.textContent ?? '').trim().toLowerCase(),
    );

    expect(headers.some((header) => header.includes('note'))).toBe(false);
  });

  it('shows an empty state when nothing matches', async () => {
    fake.rows = [];

    const element = await render();
    expect(element.querySelector('.empty')?.textContent).toContain('No medicines yet');
  });
});
