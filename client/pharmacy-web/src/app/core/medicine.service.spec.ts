import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { MedicineService } from './medicine.service';
import { PagedResult, MedicineListItem } from './models';

describe('MedicineService', () => {
  let service: MedicineService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(MedicineService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sends paging and sorting as query parameters', () => {
    service
      .search({ page: 2, pageSize: 25, sortBy: 'expiryDate', sortDir: 'asc' })
      .subscribe();

    const request = httpMock.expectOne(
      (candidate) => candidate.url === '/api/v1/medicines',
    );

    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('sortBy')).toBe('expiryDate');
    expect(request.request.params.get('sortDir')).toBe('asc');
    // No search term was supplied, so the parameter must be absent rather than empty -
    // an empty `search=` would still be a filter as far as the server is concerned.
    expect(request.request.params.has('search')).toBe(false);

    request.flush(emptyPage());
  });

  it('trims the search term and omits it when it is only whitespace', () => {
    service.search({ search: '  amox ', page: 1, pageSize: 10, sortBy: 'name', sortDir: 'asc' }).subscribe();
    const withTerm = httpMock.expectOne((candidate) => candidate.url === '/api/v1/medicines');
    expect(withTerm.request.params.get('search')).toBe('amox');
    withTerm.flush(emptyPage());

    service.search({ search: '   ', page: 1, pageSize: 10, sortBy: 'name', sortDir: 'asc' }).subscribe();
    const withoutTerm = httpMock.expectOne((candidate) => candidate.url === '/api/v1/medicines');
    expect(withoutTerm.request.params.has('search')).toBe(false);
    withoutTerm.flush(emptyPage());
  });

  it('posts a new medicine to the collection endpoint', () => {
    service
      .create({
        fullName: 'Paracetamol 500mg Tablet',
        brand: 'HealWell',
        expiryDate: '2027-11-30',
        quantity: 250,
        price: 3.4,
        notes: null,
      })
      .subscribe();

    const request = httpMock.expectOne('/api/v1/medicines');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.fullName).toBe('Paracetamol 500mg Tablet');
    request.flush({});
  });

  function emptyPage(): PagedResult<MedicineListItem> {
    return { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 };
  }
});
