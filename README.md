# ABC Pharmacy — Medicine Tracking & Sales

A single page application for tracking pharmacy stock and recording sales.

- **API** — ASP.NET Core 8 Web API, data persisted as JSON documents on the server (no database, as the brief requires).
- **Client** — Angular 22 SPA (standalone components, signals, zoneless change detection).

---

## Running it

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download) (or newer) and [Node.js 22.22.3+](https://nodejs.org).

### 1. Start the API

```bash
dotnet run --project src/Pharmacy.Api
```

It listens on **http://localhost:5136** and opens Swagger UI at **http://localhost:5136/swagger**.

On first run it creates `src/Pharmacy.Api/App_Data/medicines.json` with 14 seeded medicines and an empty
`sales.json`. The seed dates are relative to the day you first run it, so the red, yellow and normal
colour bands are all visible immediately. Delete the `App_Data` folder to start over.

### 2. Start the Angular client

```bash
cd client/pharmacy-web
npm install
npm start
```

Open **http://localhost:4200**. The dev server proxies `/api` to the API (`proxy.conf.json`), so there is
no CORS configuration to fight with during development.

### Running tests

```bash
dotnet test                       # 46 xUnit tests
cd client/pharmacy-web && npm test  # 14 Vitest tests
```

### Optional: one URL for everything

```bash
cd client/pharmacy-web
npm run build
# copy dist/pharmacy-web/browser/* into src/Pharmacy.Api/wwwroot/
dotnet run --project src/Pharmacy.Api
```

The API serves the SPA from `wwwroot` when that folder exists, including a fallback for deep links such
as `/medicines/new`.

---

## What it does

| Requirement | Where it lives |
|---|---|
| FR-01 Display all medicines | `GET /api/v1/medicines` → `MedicineListComponent` |
| FR-02 Grid shows every attribute except Notes | `MedicineListItemDto` deliberately has no `notes` field |
| FR-03 Add medicine | `POST /api/v1/medicines` → `MedicineFormComponent` |
| FR-04 Red when expiry < 30 days | `MedicineStatusEvaluator` → `rowSeverity: Critical` → `.row-critical` |
| FR-05 Yellow when quantity < 10 | `MedicineStatusEvaluator` → `rowSeverity: Warning` → `.row-warning` |
| FR-06 Sale records | `POST /api/v1/sales`, `SaleNewComponent`, `SaleListComponent` |
| FR-07 Search by name *(good to have)* | `?search=` — server-side, case-insensitive, debounced in the client |

Beyond the brief: column sorting, paging, a dashboard summary strip, edit and remove, Swagger, and
sales history with per-line detail.

---

## Three decisions worth explaining

**1. The colour rules live on the server.**

The obvious implementation puts `expiryDate - today < 30` in the Angular template. This one computes it
in `MedicineStatusEvaluator` and returns `expiryStatus`, `stockStatus` and `rowSeverity` on every row;
the client only maps `rowSeverity` to a CSS class. The rule then exists once, is unit-testable at its
boundaries, cannot drift between clients, and cannot be broken by a workstation with a wrong clock.
Thresholds are configuration (`Pharmacy:ExpiryWarningDays`, `Pharmacy:LowStockThreshold`), not constants.

Red beats yellow when both apply, because expired stock is a safety problem and low stock is a
purchasing problem.

**2. JSON files need the guarantees a database would have given.**

`JsonFileStore<T>` replaces three of them:

- *Single writer* — a `SemaphoreSlim` serialises every read-modify-write, so two tills cannot both read
  "1 in stock" and both sell it. There is a test that fires 200 concurrent increments and asserts the
  count is 200, and one that fires 8 concurrent sales of the last unit and asserts exactly one succeeds.
- *Atomic replace* — writes go to `*.json.tmp` and are then moved over the real file, so a crash
  mid-write leaves the previous good document rather than a truncated one.
- *All-or-nothing mutations* — if the delegate throws (a stock check fails, say), nothing is written and
  the cache is dropped so the next read comes from the last good file.

A sale touches two stores and files give no cross-file transaction, so `SaleService` decrements stock
first, appends the sale second, and compensates the decrement if the append fails — logging loudly if
compensation itself fails. The design document's transactional outbox is the production answer; this is
the honest small-scale equivalent.

**3. Sale lines snapshot the price and name.**

A receipt must not change when the catalogue is repriced or renamed, so `SaleLine` copies
`medicineName`, `brand` and `unitPrice` at the moment of sale rather than referencing the medicine.
Sales are append-only: no edit, no delete.

---

## API

Base path `/api/v1`. Errors are RFC 9457 `application/problem+json` with a `correlationId`.

| Method | Route | Notes |
|---|---|---|
| GET | `/medicines` | `?search=&page=&pageSize=&sortBy=&sortDir=` — `sortBy` is one of `severity` (default), `name`, `brand`, `expiryDate`, `quantity`, `price` |
| GET | `/medicines/summary` | Counts for the dashboard tiles |
| GET | `/medicines/{id}` | Full record including notes |
| POST | `/medicines` | 201 + `Location`; 409 if name+brand already exists |
| PUT | `/medicines/{id}` | |
| DELETE | `/medicines/{id}` | 204 |
| GET | `/sales` | `?page=&pageSize=&from=&to=` — newest first |
| GET | `/sales/{id}` | |
| POST | `/sales` | 409 if a line exceeds available stock |

A sample grid row:

```json
{
  "id": "3f2a9c14-6b7e-4d21-9a8f-1c2d3e4f5a6b",
  "fullName": "Amoxicillin 500mg Capsule",
  "brand": "MediCore",
  "expiryDate": "2026-09-05",
  "daysToExpiry": 18,
  "quantity": 42,
  "price": 12.50,
  "expiryStatus": "ExpiringSoon",
  "stockStatus": "Ok",
  "rowSeverity": "Critical",
  "version": 7
}
```

---

## Layout

```
PharmacyApp.sln
src/Pharmacy.Api/
  Domain/          Medicine, Sale, SaleLine, the three status enums
  Contracts/       Request and response DTOs (validation attributes live here)
  Storage/         JsonFileStore<T>, seed data
  Services/        MedicineStatusEvaluator, MedicineService, SaleService, PharmacyClock
  Controllers/     MedicinesController, SalesController
  Infrastructure/  DomainException, RFC 9457 exception handler
  App_Data/        medicines.json, sales.json  (created on first run)
tests/Pharmacy.Api.Tests/
  MedicineStatusEvaluatorTests   colour rules at their boundaries (29/30 days, 9/10 units)
  JsonFileStoreTests             seeding, round-trip, rollback, concurrency, corrupt files
  MedicineServiceTests           search, paging, sorting, duplicates, rounding, summary
  SaleServiceTests               stock decrement, oversell, rollback, price snapshot, races
client/pharmacy-web/
  src/app/core/                  models, HTTP services, formatting, error mapping
  src/app/features/medicines/    grid + add/edit form
  src/app/features/sales/        record a sale, sales history
```

---

## Known limits

Deliberate omissions, and what each would take:

- **No authentication.** The design document specifies JWT + three roles; this build has no Identity
  service, so `soldBy` is free text rather than the signed-in user.
- **Write throughput does not scale past one process.** The single-writer lock is in-process. Two API
  instances over the same file would each hold their own lock. The store interface is the seam: swapping
  `JsonFileStore<T>` for a database-backed implementation changes no domain, controller or UI code.
- **No batch/lot numbers.** Real pharmacy stock usually carries several expiry dates per medicine. This
  is the first question to put to the client, and it changes the data model.
- **Optimistic concurrency is prepared but not enforced.** Every record carries a `version` and it is
  returned to the client; wiring it to `If-Match` / 412 is a small addition when a second user appears.
