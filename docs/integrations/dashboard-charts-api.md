# Dashboard Charts & Reports API

This document describes which backend endpoints power the dashboard statistics and charts, and the expected query parameters (Section 5 of Implementation Plan).

## Statistics endpoints

All statistics endpoints support optional `dateFrom`, `dateTo`, and `stationId` when provided by the frontend. The dashboard builds params via `buildParams(filters)` in `dashboard.ts` (dateFrom, dateTo, stationId).

| Chart / Widget | Endpoint | Params | Notes |
|----------------|----------|--------|--------|
| Weighing stats | `GET /api/v1/weighing-transactions/statistics` | dateFrom, dateTo, stationId | Uses MV for past days + **live query for today**; ensure range includes today. |
| Case statistics | `GET /api/v1/case/cases/statistics` | (filters as query) | |
| Hearing statistics | `GET /api/v1/hearings/statistics` | (filters as query) | |
| Prosecution statistics | `GET /api/v1/prosecutions/statistics` | (filters as query) | |
| Invoice statistics | `GET /api/v1/invoices/statistics` | (filters as query) | |
| Receipt statistics | `GET /api/v1/receipts/statistics` | (filters as query) | |
| Yard statistics | `GET /api/v1/yard-entries/statistics` | stationId | |
| Vehicle tag statistics | `GET /api/v1/vehicle-tags/statistics` | dateFrom, dateTo, stationId | |

## Chart data endpoints

| Chart | Endpoint | Params | Response shape |
|-------|----------|--------|----------------|
| Compliance trend | `GET /api/v1/weighing-transactions/compliance-trend` | dateFrom, dateTo, stationId | Daily compliance/overload counts |
| Overload distribution | `GET /api/v1/weighing-transactions/overload-distribution` | dateFrom, dateTo, stationId | Severity bands |
| Revenue by station | `GET /api/v1/receipts/revenue-by-station` | dateFrom, dateTo, stationId | |
| Monthly revenue | `GET /api/v1/receipts/monthly-revenue` | dateFrom, dateTo, stationId | Time series |
| Case disposition | `GET /api/v1/case/cases/disposition-breakdown` | dateFrom, dateTo, stationId | |
| Case trend | `GET /api/v1/case/cases/trend` | dateFrom, dateTo, stationId | Time series |
| Payment methods | `GET /api/v1/receipts/payment-methods` | dateFrom, dateTo, stationId | |
| Top offenders | `GET /api/v1/weighing-transactions/top-offenders` (or similar) | dateFrom, dateTo, stationId | |

## Frontend usage

- **Dashboard filters**: `DashboardFilterContext` provides default `dateFrom` (e.g. 30 days ago), `dateTo` (today), `stationId` ('all' or specific). Ensure `dateTo` includes today so that weighing stats and charts reflect same-day data.
- **Weighing statistics**: Backend merges materialized view data (past days) with a live query for **today**; no MV refresh is required for current-day accuracy.

## Materialized views

Reporting may use:

- `mv_daily_weighing_stats` – refreshed by script or job; weighing stats endpoint uses **live fallback for today** so the dashboard is accurate without immediate MV refresh.
