# Dashboard Charts & Reports API

This document describes which backend endpoints power the dashboard statistics and charts, and the expected query parameters.

## Default station behaviour

- **HQ users**: Dashboard filter defaults to `stationId = 'all'` (show values for all stations). The station dropdown is for drill-down.
- **Non-HQ users**: Dashboard filter defaults to the **current user's assigned station** (from auth/token). The station field is read-only for them.
- **Effective station on backend**: When the frontend sends no `stationId` (or "all"), the backend uses: `(stationId == null && hasGlobalRead) ? null : (stationId ?? tenantContext.StationId)` so HQ with no selection sees all stations; others see their tenant station or the selected station.

## Statistics endpoints

All statistics endpoints accept optional query params `dateFrom`, `dateTo`, and `stationId` (GUID). The frontend builds params via `buildParams(filters)` in `dashboard.ts`; when `stationId` is `'all'` it is omitted so the backend applies the effective-station rule above.

| Chart / Widget | Endpoint | Params | Notes |
|----------------|----------|--------|--------|
| Weighing stats | `GET /api/v1/weighing-transactions/statistics` | dateFrom, dateTo, stationId | Uses MV for past days + **live query for today**. |
| Case statistics | `GET /api/v1/case/cases/statistics` | dateFrom, dateTo, stationId | Filters by case date and station (via Weighing). |
| Hearing statistics | `GET /api/v1/hearings/statistics` | dateFrom, dateTo, stationId | Filters by hearing date and station (via CaseRegister.Weighing). |
| Prosecution statistics | `GET /api/v1/prosecutions/statistics` | dateFrom, dateTo, stationId | |
| Invoice statistics | `GET /api/v1/invoices/statistics` | dateFrom, dateTo, stationId | |
| Receipt statistics | `GET /api/v1/receipts/statistics` | dateFrom, dateTo, stationId | |
| Yard statistics | `GET /api/v1/yard-entries/statistics` | dateFrom, dateTo, stationId | Filters by EnteredAt date range. |
| Vehicle tag statistics | `GET /api/v1/vehicle-tags/statistics` | dateFrom, dateTo, stationId | |

## Chart data endpoints

| Chart | Endpoint | Params | Response shape |
|-------|----------|--------|----------------|
| Compliance trend | `GET /api/v1/weighing-transactions/compliance-trend` | dateFrom, dateTo, stationId | Daily compliance/overload counts |
| Overload distribution | `GET /api/v1/weighing-transactions/overload-distribution` | dateFrom, dateTo, stationId | Severity bands |
| Revenue by station | `GET /api/v1/receipts/revenue-by-station` | dateFrom, dateTo | |
| Monthly revenue | `GET /api/v1/receipts/monthly-revenue` | dateFrom, dateTo | Time series |
| Case disposition | `GET /api/v1/case/cases/disposition-breakdown` | dateFrom, dateTo, stationId | |
| Case trend | `GET /api/v1/case/cases/trend` | dateFrom, dateTo, stationId | Time series |
| Payment methods | `GET /api/v1/receipts/payment-methods` | (as per receipt search) | |
| Prosecution trend | `GET /api/v1/prosecutions/trend` | dateFrom, dateTo, stationId | |
| Prosecution by status | `GET /api/v1/prosecutions/by-status` | dateFrom, dateTo, stationId | |
| Yard processing trend | `GET /api/v1/yard-entries/processing-trend` | dateFrom, dateTo | |
| Hearing outcomes | `GET /api/v1/hearings/outcomes` | dateFrom, dateTo | |

## Frontend usage

- **Dashboard filters**: `DashboardFilterContext` initialises with default `dateFrom` (30 days ago), `dateTo` (today), and `stationId` from token: HQ → `'all'`, non-HQ → user's station. Ensure `dateTo` includes today for weighing same-day data.
- **Weighing statistics**: Backend merges materialized view data (past days) with a live query for **today**; no MV refresh is required for current-day accuracy.
- **Param format**: Send `dateFrom`/`dateTo` as ISO date (e.g. `yyyy-MM-dd`). Omit `stationId` when "All stations" so the backend can return all-station data for users with global read.

## Reports API

- **Generate report**: `GET /api/v1/reports/{module}/{reportType}` with query params: `format` (pdf|csv|xlsx), `dateFrom`, `dateTo`, `stationId`, `status`, and for weighing reports `weighingType`, `controlStatus`.
- **Catalog**: `GET /api/v1/reports/catalog?module=` (optional module filter).

## Materialized views

Reporting may use:

- `mv_daily_weighing_stats` – refreshed by script or job; weighing stats endpoint uses **live fallback for today** so the dashboard is accurate without immediate MV refresh.
