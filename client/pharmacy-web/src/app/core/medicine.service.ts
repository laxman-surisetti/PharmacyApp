import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  InventorySummary,
  Medicine,
  MedicineListItem,
  MedicineQuery,
  PagedResult,
  SaveMedicineRequest,
} from './models';

export const API_BASE = '/api/v1';

@Injectable({ providedIn: 'root' })
export class MedicineService {
  private readonly http = inject(HttpClient);

  /** FR-01 / FR-02 / FR-07 - one page of the grid, filtered and sorted server-side. */
  search(query: MedicineQuery): Observable<PagedResult<MedicineListItem>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize)
      .set('sortBy', query.sortBy)
      .set('sortDir', query.sortDir);

    const term = query.search?.trim();
    if (term) {
      params = params.set('search', term);
    }

    return this.http.get<PagedResult<MedicineListItem>>(`${API_BASE}/medicines`, { params });
  }

  summary(): Observable<InventorySummary> {
    return this.http.get<InventorySummary>(`${API_BASE}/medicines/summary`);
  }

  get(id: string): Observable<Medicine> {
    return this.http.get<Medicine>(`${API_BASE}/medicines/${id}`);
  }

  /** FR-03. */
  create(request: SaveMedicineRequest): Observable<Medicine> {
    return this.http.post<Medicine>(`${API_BASE}/medicines`, request);
  }

  update(id: string, request: SaveMedicineRequest): Observable<Medicine> {
    return this.http.put<Medicine>(`${API_BASE}/medicines/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${API_BASE}/medicines/${id}`);
  }
}
