## TruLoad Backend Plan (C#/.NET 8, PostgreSQL, Redis, RabbitMQ)

### 1. Overview
Cloud-hosted enforcement and weighing backend with offline-aware clients. Core flows and legal rules follow the KURAWEIGH specification and prosecution narration provided, applying either EAC Act or Traffic Act. Backend exposes REST APIs (and WebSockets/SignalR where applicable), integrates with TruConnect for weight acquisition, supports high concurrency, rate limiting, caching, resilient messaging, and auditable workflows.

### 2. Tech Stack
- C#, .NET 8 Web API (vertical slices per module)
- PostgreSQL (Npgsql, EF Core 8, JSONB where beneficial)
- Redis (caching, distributed locks)
- RabbitMQ (domain events, async tasks)
- Swagger/OpenAPI, Serilog, FluentValidation, MediatR (CQRS), Polly (resiliency)
- Identity: ASP.NET Identity + JWT (HS256)

### 3. Architecture
- Modular monolith with microservice-like boundaries for: User/Role/Shift, Weighing, Prosecution, Special Release, Vehicle Inspection, Tags/Yard, Reporting, Settings, Security, Technical.
- Domain events (RabbitMQ): `weighing.recorded`, `vehicle.tagged`, `vehicle.sent_to_yard`, `prosecution.case_opened`, `invoice.paid`, `vehicle.compliant`.
- Background services: sync jobs, document generation, notifications, cleanup.
- API rate limiting: ingress (Nginx annotations) + AspNetCoreRateLimit.
- Caching: per-entity read models; query caching for heavy reports; response caching for public metadata.
- Observability: structured logging (Serilog), OpenTelemetry traces/metrics, health endpoints.

### 4. Data Model (high level)
Key conventions: surrogate primary keys (bigserial), natural keys indexed, foreign keys with ON UPDATE/DELETE rules, created_at/updated_at, soft-delete where necessary. Composite indexes for hot paths; partitioning for large tables.

#### 4.1 User Management & Security
- `users` (id, email, phone, password_hash, status, last_login_at)
- `roles` (id, name, description)
- `user_roles` (user_id, role_id, unique)
- `shifts` (id, name, start_time, end_time, days_mask, rotation_group)
- `user_shifts` (user_id, shift_id, starts_on, ends_on)
- `audit_logs` (id, actor_id, action, entity, entity_id, data_json, ip, at)
- Indexes: unique(email), idx_user_roles_user, idx_user_shifts_active, btree on audit (actor_id, at desc)

#### 4.2 Reference & Settings
- `stations` (id, code, name, route_id, cluster_id, bound, default_camera_id)
- `routes` (id, code, name)
- `transporters` (id, name, address, phone)
- `vehicles` (id, reg_no unique, make_id, trailer_no, transporter_id, permit_no, permit_issued_at, permit_expires_at)
- `drivers` (id, id_no_or_passport, license_no, full_names, gender, nationality, address, ntac_no)
- `currencies` (code, name, rate_to_usd, as_of)
- `system_settings` (key unique, value)
- `prosecution_settings` (station_default_id, court_default_id, tolerance_gvw_kg default 200, tolerance_axle_kg default 200, act_default)
- Indexes: unique(reg_no), idx_currencies_as_of, idx_settings_key

#### 4.3 Axle Configurations and Acts
- `axle_configurations` (id, code e.g. 2A/3A/…, axle_count, base_gvw_kg)
- `axle_groups` (id, name A/B/C/D, position_range json: [start_axle,end_axle])
- `axle_configuration_groups` (axle_configuration_id, group_id, group_order)
- `axle_group_limits` (axle_configuration_id, group_id, permissible_kg)
- `act_definitions` (id, name: EAC|TRAFFIC, notes)
- `eac_fee_bands_gvw` (id, overload_kg_from, overload_kg_to, fee_usd)
- `eac_fee_bands_axle` (id, axle_type, overload_kg_from, overload_kg_to, fee_usd)
- `traffic_fee_bands_gvw` (id, overload_kg_from, overload_kg_to, fee_usd)
- Permits: `permit_rules` (vehicle_class_code, extra_axle_kg, extra_gvw_kg)
- Indexes: unique(axle_configuration_id, group_id), btree on bands ranges, partial indexes for active rules

#### 4.4 Weighing (Core)
- `weighings` (id, station_id, vehicle_id, driver_id, weighing_type enum: static|wim|axle, act_id, bound, ticket_no, weighed_at, origin_id, destination_id, cargo_id, gvw_measured_kg, gvw_permissible_kg, gvw_overload_kg, tolerance_applied_bool, has_permit_bool, is_sent_to_yard_bool, reweigh_cycle_no, reweigh_limit default 8)
- `weighing_axles` (id, weighing_id, axle_no, measured_kg, permissible_kg, overload_kg, group_name A/B/C/D)
- `weight_stream_events` (id, weighing_id, scale_id, raw_json, recorded_at)  // streaming snapshots from TruConnect
- `scale_tests` (id, station_id, carried_at, result, details)
- Indexes: (vehicle_id, weighed_at desc), (station_id, weighed_at desc), partial idx on is_sent_to_yard, btree on ticket_no unique
- Partitioning: monthly on `weighings(weighed_at)` for high write/read performance

#### 4.5 Yard, Tags & Enforcement
- `vehicle_tags` (id, reg_no, tag_type auto|manual, reason, station_code, status open|closed, created_by, closed_by, closed_reason, opened_at, closed_at)
- `yard_entries` (id, weighing_id, station_id, reason redistrib|gvw_overload|permit_check|offload)
- `prohibition_orders` (id, yard_entry_id, doc_no, issued_at, inspector_name, yard_location, offload_truck_id nullable)
- Indexes: (reg_no, status), (yard_entry_id)

#### 4.6 Prosecution
- `prosecution_cases` (id, yard_entry_id, act_id, case_no, ntac_no, ob_no, court_id, complainant_officer_id, investigating_officer_id, created_at)
- `prosecution_parties` (id, case_id, party_type driver|owner|transporter, identity fields…)
- `charge_breakdowns` (id, case_id, basis axle|gvw|permit, overload_kg, fee_usd, fee_kes, forex_rate, rule_ref)
- `invoices` (id, case_id, invoice_no, total_usd, total_kes, issued_at, ext_ref)
- `receipts` (id, case_id, invoice_id, amount_usd, amount_kes, paid_at, ext_ref)
- `load_corrections` (id, case_id, memo_text, created_at)
- `compliance_certificates` (id, case_id, issued_at)
- `charge_summaries` (materialized view): pre-join of the highest charge per EAC rule
- Indexes: (case_id), (yard_entry_id unique), (issued_at), MV refreshed on invoice/receipt

#### 4.7 Special Release & Permits
- `special_releases` (id, weighing_id or case_id, reason tolerance|permit|redistribution, issued_at, officer_id, details)
- `permits` (id, vehicle_id, permit_no, permit_class, extra_axle_kg, extra_gvw_kg, valid_from, valid_to, issuer)

#### 4.8 Vehicle Inspection (Dimensions)
- `vehicle_inspections` (id, vehicle_id, station_id, height_m, width_m, length_m, side_projection_m, front_rear_projection_m, act_id, result pass|fail, inspector_id, inspected_at, permit_no_nullable)

### 5. Legal Computation Rules (summary)
- EAC (Vehicle Load Control Act 2016; Enforcement Regs 2018):
  - Charging basis: higher of GVW overload vs any axle overload.
  - Tolerance: 5% axle tolerance (statutory) on permissible axle loads; GVW has no statutory tolerance. Additionally, road authorities may operate an absolute tolerance (e.g., ≤200 kg) for auto special release; store as policy.
  - Fee bands: GVW bands in 500 kg increments (e.g., 1–500 kg = 90.95 USD; 501–1000 kg = 186.00 USD; 1001–1500 kg = 289.35 USD; …). Axle fee bands per axle type (steering/single/tandem/tridem). Persist into `eac_fee_bands_gvw` and `eac_fee_bands_axle` with gazette/version.
  - Bridge formula / max GVW (e.g., 56 t for ≤7 axles) enforced via configuration tables.
- Kenya Traffic Act (Cap 403) / KeNHA schedules:
  - Charging basis: GVW overload only (axle overloads recorded but not charged for fee computation).
  - Tolerance: no statutory GVW tolerance; axle tolerances mirror EAC (5%). Operational absolute tolerance (≤200 kg) may be configured for auto special release where policy allows.
  - Fee bands: KSh schedule per GVW excess band (e.g., 1–500 kg ≈ $235.90; 501–1000 kg ≈ $482.50; 1001–1500 kg ≈ $750.55). Store USD with KSh reference and schedule version.
- Permit vehicles (wide/excess load): apply permit-extended limits per class (e.g., 2A: +3,000 kg axle, +1,000 kg GVW; 3A: +3,000 kg axle, +2,000 kg GVW). If measured exceeds permit-extended limits, prosecute; otherwise special permit release.

5.1 Tolerance & Policy Tables
- `tolerance_policies` (id, act_id, type axle|gvw, value_kg, percent_nullable, enabled_bool, applies_to permit|non_permit|both, authority_ref, effective_from, effective_to). Use policy engine to resolve applicable tolerance at evaluation time.

5.2 Computation Flow
1) Determine applicable Act: road/station default + user override if permitted.
2) Resolve permissible limits from `axle_group_limits` and `axle_configurations` (+ permit rules if any).
3) Compute per-axle overloads and GVW overload; apply tolerance policies to classify auto release vs enforcement.
4) Under EAC: compute GVW fee and axle fee, select higher; under Traffic: compute GVW fee only.
5) Convert USD↔KES using `currencies` snapshot (store both values in `charge_breakdowns`).

5.3 Seed Data (from provided SQL and research)
- Import axle configuration codes (2A, 3A, …), groups (A/B/C/D), and permissible limits.
- Seed EAC GVW bands (sample given: 1–500=90.95, 501–1000=186.00, 1001–1500=289.35, …) and axle bands per axle type.
- Seed Traffic GVW bands per KeNHA schedule (store USD with KSh reference), mark schedule version and source.
- Seed permit rules for common classes (2A, 3A) and allow organization overrides.


### 6. Performance & Concurrency
- Hot-path indexes: `weighings(vehicle_id, weighed_at desc)`, `weighings(station_id, weighed_at desc)`, partial idx for `is_sent_to_yard`, GIN on JSONB where used.
- Partition `weighings` by month; archive strategy with retention.
- Redis caching: permissible tables, fee bands, station config; sliding TTL with cache bust on updates.
- Idempotency keys on weighing ticket generation and case creation.
- Advisory locks during reweigh cycles per vehicle to prevent race conditions.

### 7. Integrations
- TruConnect: local endpoint, polled/streamed to UI; backend validates/reconciles final saved weights and stores stream in `weight_stream_events` for audit.
- Payments: eCitizen/Road Authority (invoice/receipt webhooks) → update `receipts` and trigger release flow.
- Case Management (NTAC/OB/Court) references mapped in `prosecution_cases` and parties.

### 8. Offline & Sync
- Clients cache drafts in IndexedDB (frontend); upon submit, backend persists `weighings` atomically with axles.
- Device sync queues: `device_sync_events` (id, device_id, payload, status), replay-safe via idempotency.

### 9. API Surface (selected)
- Auth: /auth/login, /auth/refresh
- Users/Roles/Shifts: CRUD endpoints with RBAC
- Weighing: /weighings (create), /weighings/{id}, /weighings/{id}/axles, /weighings/{id}/send-to-yard
- Tags: /tags (list/create/close)
- Yard: /yard (list), /yard/{id}/prohibit
- Prosecution: /cases (create from yard), /cases/{id}/charges, /cases/{id}/invoice, /cases/{id}/receipt, /cases/{id}/escalate
- Special Release: /releases (create)
- Inspections: /inspections (create)
- Settings/Acts/Axle Configs: CRUD, import tables (seeders)

### 10. DevOps
- Dockerfile (present); build.sh created to integrate with centralized devops-k8s; KubeSecrets/devENV.yml holds runtime env (Kubernetes encodes to base64 automatically).
- HPA/VPA: chart values tuned per module; liveness/readiness; PodDisruptionBudgets; Nginx rate limiting.
- ArgoCD: `devops-k8s/apps/truload-backend/{app.yaml,values.yaml}` used by build.sh to update image tag.

### 11. Sprints (by priority)
1) User Management & Security
   - Auth/JWT, roles, shifts; audit logging; RBAC gates. Seed admin.
2) Weighing (Static, WIM, Mobile(Axle by Axle Weighing)) + Station/Vehicle/Driver/Transporter
   - TruConnect contract, data capture, tolerance evaluation, ticketing; send-to-yard; reweigh loop (limit 8).
3) Prosecution Core
   - Case intake, prohibition orders, charge computation (EAC/Traffic), invoices/receipts, load correction, compliance certificate, court escalation.
4) Special Release & Permits
   - Auto/manual special release; permit validation; permit rules CRUD.
5) Inspection (Dimensions)
   - Capture, validate, linkage to releases/prosecution.
6) Reporting & Analytics
   - Registers, overload & reweigh, charged reports, scale tests, statements; exports.
7) Settings/Technical
   - Cameras, I/O, health checks, calibration certificates; organization look & feel.

### 12. Indexing & Views (examples)
- MV `charge_summaries(case_id, best_basis, fee_usd, fee_kes)` refreshed on charge changes.
- Partial index on `vehicle_tags(status='open')`.
- Composite index `weighing_axles(weighing_id, axle_no)`.

### 13. Data Seeding
- Axle configs, groups, group limits; EAC & Traffic fee bands; tolerance defaults; permit rules (2A, 3A samples).

### 14. Compliance Notes
- Keep fee bands traceable to gazetted schedules; store source/version in band tables.
- Store daily forex in `currencies` and snapshot into `charge_breakdowns` for audit.

