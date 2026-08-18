# pharmacy-web

Angular 22 client for the ABC Pharmacy medicine tracker. See the [root README](../../README.md) for the
full picture; this file covers the client only.

```bash
npm install
npm start     # http://localhost:4200, proxies /api to http://localhost:5136
npm test      # Vitest
npm run build # dist/pharmacy-web/browser
```

The API must be running (`dotnet run --project ../../src/Pharmacy.Api`) or every screen shows
"Cannot reach the API".

## Where things are

```
src/app/core/                  wire models, HTTP services, formatting and error mapping
src/app/features/medicines/    the stock grid (medicine-list) and the add/edit form
src/app/features/sales/        record a sale (sale-new) and sales history (sale-list)
src/styles.css                 the shared design system, including the red/yellow row bands
proxy.conf.json                dev-server proxy to the API
```

Row colours are **not** decided here. The API returns `rowSeverity` (`Normal` | `Warning` | `Critical`)
on every row and `MedicineListComponent.rowClass()` maps it to a CSS class — one rule, one place.
