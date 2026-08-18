import { Routes } from '@angular/router';

/**
 * Every screen is lazily loaded, so the initial bundle carries the shell and the
 * stock grid only - the screen a pharmacist actually opens first.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'medicines' },
  {
    path: 'medicines',
    title: 'Stock - ABC Pharmacy',
    loadComponent: () =>
      import('./features/medicines/medicine-list/medicine-list').then((m) => m.MedicineListComponent),
  },
  {
    path: 'medicines/new',
    title: 'Add medicine - ABC Pharmacy',
    loadComponent: () =>
      import('./features/medicines/medicine-form/medicine-form').then((m) => m.MedicineFormComponent),
  },
  {
    path: 'medicines/:id/edit',
    title: 'Edit medicine - ABC Pharmacy',
    loadComponent: () =>
      import('./features/medicines/medicine-form/medicine-form').then((m) => m.MedicineFormComponent),
  },
  {
    path: 'sales',
    title: 'Sales - ABC Pharmacy',
    loadComponent: () => import('./features/sales/sale-list/sale-list').then((m) => m.SaleListComponent),
  },
  {
    path: 'sales/new',
    title: 'Record a sale - ABC Pharmacy',
    loadComponent: () => import('./features/sales/sale-new/sale-new').then((m) => m.SaleNewComponent),
  },
  { path: '**', redirectTo: 'medicines' },
];
