/**
 * Wire contracts. These mirror the DTOs in Pharmacy.Api/Contracts one-for-one; the API
 * serialises enums as strings so they are modelled here as string unions rather than
 * numeric enums.
 */

export type ExpiryStatus = 'Ok' | 'ExpiringSoon' | 'Expired';
export type StockStatus = 'Ok' | 'Low' | 'OutOfStock';

/**
 * The colour band for a grid row, decided by the server so the 30-day / 10-unit rules
 * exist in exactly one place.
 */
export type RowSeverity = 'Normal' | 'Warning' | 'Critical';

/** One row of the medicine grid. Notes are intentionally absent - FR-02. */
export interface MedicineListItem {
  id: string;
  fullName: string;
  brand: string;
  /** ISO yyyy-MM-dd. */
  expiryDate: string;
  daysToExpiry: number;
  quantity: number;
  price: number;
  expiryStatus: ExpiryStatus;
  stockStatus: StockStatus;
  rowSeverity: RowSeverity;
  version: number;
}

export interface Medicine extends MedicineListItem {
  notes: string | null;
  createdUtc: string;
  modifiedUtc: string;
}

export interface SaveMedicineRequest {
  fullName: string;
  notes: string | null;
  expiryDate: string;
  quantity: number;
  price: number;
  brand: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface InventorySummary {
  totalMedicines: number;
  expiredCount: number;
  expiringSoonCount: number;
  lowStockCount: number;
  outOfStockCount: number;
  totalStockValue: number;
}

export type MedicineSortField = 'severity' | 'name' | 'brand' | 'expiryDate' | 'quantity' | 'price';
export type SortDirection = 'asc' | 'desc';

export interface MedicineQuery {
  search?: string;
  page: number;
  pageSize: number;
  sortBy: MedicineSortField;
  sortDir: SortDirection;
}

export interface SaleLine {
  medicineId: string;
  medicineName: string;
  brand: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Sale {
  id: string;
  saleNumber: string;
  soldAtUtc: string;
  soldBy: string | null;
  notes: string | null;
  lines: SaleLine[];
  totalAmount: number;
}

export interface CreateSaleRequest {
  lines: { medicineId: string; quantity: number }[];
  soldBy: string | null;
  notes: string | null;
}
