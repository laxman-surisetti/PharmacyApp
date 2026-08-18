import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE } from './medicine.service';
import { CreateSaleRequest, PagedResult, Sale } from './models';

@Injectable({ providedIn: 'root' })
export class SaleService {
  private readonly http = inject(HttpClient);

  /** FR-06 - sales history, newest first. */
  search(page: number, pageSize: number): Observable<PagedResult<Sale>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Sale>>(`${API_BASE}/sales`, { params });
  }

  get(id: string): Observable<Sale> {
    return this.http.get<Sale>(`${API_BASE}/sales/${id}`);
  }

  /** Records the sale and decrements stock in the same server-side operation. */
  create(request: CreateSaleRequest): Observable<Sale> {
    return this.http.post<Sale>(`${API_BASE}/sales`, request);
  }
}
