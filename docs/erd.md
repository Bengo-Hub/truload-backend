# TruLoad Backend - Entity Relationship Diagram (ERD)

## Overview

This document defines all database entities, properties (fields), indexes, relationships, views, materialized views, and vector columns for the TruLoad backend. The database uses PostgreSQL 16+ with the pgvector extension enabled for semantic search capabilities.

## Database Configuration

**Naming Conventions:**
- **Tables:** snake_case, plural (e.g., `weighing_axles`)
- **Columns:** snake_case
- **Primary Keys:** `id` (BIGSERIAL)
- **Foreign Keys:** `{entity}_id` (e.g., `vehicle_id`)
- **Timestamps:** `created_at`, `updated_at`, `deleted_at` (soft delete)
- **Enums:** Stored as VARCHAR with CHECK constraints

**Extensions:**
- `pgvector` - Vector similarity search for embeddings
- `uuid-ossp` - UUID generation support

**Partitioning:**
- Monthly partitions on `weighings(weighed_at)` for high write/read performance
- Partition management via pg_partman extension or custom scripts

---

## Centralized SSO Integration

### Overview

TruLoad backend integrates with the centralized `auth-service` for authentication while maintaining application-level user management (roles, shifts, permissions) locally. This approach avoids entity redundancy while ensuring seamless Single Sign-On (SSO) across BengoBox services.

### Integration Strategy

**Authentication:**
- All authentication requests routed to centralized `auth-service`
- JWT tokens validated against `auth-service` public keys
- Token refresh handled via `auth-service` refresh endpoint

**User Identity Management:**
- User identity (email, basic profile) synced from `auth-service`
- Application-specific data (shifts, station assignments, role mappings) managed locally
- One-way sync for user identity from `auth-service`
- Two-way sync for app-specific attributes where applicable

**User Synchronization:**
- Periodic sync jobs (every 15 minutes) reconcile user data
- Event-driven sync on user creation/deactivation from `auth-service`
- Conflict resolution: Auth-service is source of truth for identity; local service for app-specific data
- Sync status tracked via `sync_status` and `sync_at` fields

**Entity Relationship:**
- `users` table includes `auth_service_user_id` (UUID) referencing centralized auth-service user
- Foreign key relationship maintained for consistency
- Unique constraint ensures one-to-one mapping between local and auth-service users
- Local user creation triggered by auth-service user creation events

**Benefits:**
- Eliminates duplicate user entities across services
- Single source of truth for authentication and identity
- Application-specific user data remains within service boundaries
- Seamless SSO across all BengoBox services
- Flexible role and permission management per service

---

---

## Core Entities

### User Management & Security

#### users
Application-level user management with synchronization to centralized auth-service.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Local user ID |
| auth_service_user_id | UUID | UNIQUE, INDEX | Reference to centralized auth-service user |
| email | VARCHAR(255) | UNIQUE, NOT NULL, INDEX | User email (synced from auth-service) |
| phone | VARCHAR(50) | | Contact phone |
| full_name | VARCHAR(255) | NOT NULL | Full name |
| status | VARCHAR(20) | DEFAULT 'active', CHECK | Status: active, inactive, locked |
| station_id | BIGINT | FK → stations(id), INDEX | Assigned station (nullable) |
| organization_id | BIGINT | FK → organizations(id), INDEX | Organization/Company |
| department_id | BIGINT | FK → departments(id), INDEX | Department |
| last_login_at | TIMESTAMPTZ | | Last login timestamp |
| sync_status | VARCHAR(20) | DEFAULT 'synced' | Sync status with auth-service |
| sync_at | TIMESTAMPTZ | | Last sync timestamp |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update time |
| deleted_at | TIMESTAMPTZ | | Soft delete timestamp |

**Indexes:**
- `idx_users_auth_service_user_id` ON users(auth_service_user_id)
- `idx_users_email` ON users(email)
- `idx_users_status` ON users(status) WHERE deleted_at IS NULL
- `idx_users_station` ON users(station_id) WHERE station_id IS NOT NULL

**Vector Columns:** None

**Relationships:**
- One-to-many with `user_roles` (through junction table)
- One-to-many with `user_shifts`
- One-to-many with `weighings` (weighed_by_id)
- One-to-many with `audit_logs` (actor_id)

#### roles
Application-specific roles and permissions.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Role ID |
| name | VARCHAR(100) | UNIQUE, NOT NULL | Role name |
| description | TEXT | | Role description |
| permissions | JSONB | | Permission definitions (JSON) |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |

**Indexes:**
- `idx_roles_name` ON roles(name)

**Vector Columns:** None

#### user_roles
Junction table for user-role assignments.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| user_id | BIGINT | FK → users(id), PRIMARY KEY | User ID |
| role_id | BIGINT | FK → roles(id), PRIMARY KEY | Role ID |
| assigned_at | TIMESTAMPTZ | DEFAULT NOW() | Assignment timestamp |

**Relationships:**
- Many-to-many between `users` and `roles`

#### work_shifts
Work shift definitions (e.g., "Morning Shift", "Night Shift").

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Shift ID |
| name | VARCHAR(100) | UNIQUE, NOT NULL | Shift name |
| total_hours_per_week | DECIMAL(5,2) | DEFAULT 40.00 | Expected weekly hours |
| grace_minutes | INTEGER | DEFAULT 0 | Late arrival grace period |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update time |

**Indexes:**
- `idx_work_shifts_name` ON work_shifts(name)

#### work_shift_schedules
Day-wise schedule configuration for work shifts.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Schedule ID |
| work_shift_id | BIGINT | FK → work_shifts(id), NOT NULL | Parent shift |
| day | VARCHAR(10) | CHECK | Day of week (Monday, etc.) |
| start_time | TIME | NOT NULL | Start time |
| end_time | TIME | NOT NULL | End time |
| break_hours | DECIMAL(3,1) | DEFAULT 0.0 | Break duration |
| is_working_day | BOOLEAN | DEFAULT TRUE | Is working day |
| UNIQUE (work_shift_id, day) | | | Unique day per shift |

#### shift_rotations
Rotation patterns for rotating shifts.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Rotation ID |
| title | VARCHAR(255) | NOT NULL | Rotation title |
| current_active_shift_id | BIGINT | FK → work_shifts(id) | Currently active shift |
| run_duration | INTEGER | DEFAULT 2 | Duration to run shift |
| run_unit | VARCHAR(20) | DEFAULT 'Months' | Unit (Days, Weeks, Months) |
| break_duration | INTEGER | DEFAULT 1 | Break duration between shifts |
| break_unit | VARCHAR(20) | DEFAULT 'Day' | Unit for break |
| next_change_date | TIMESTAMPTZ | | Next rotation date |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |

#### rotation_shifts
Junction table for shifts in a rotation.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| rotation_id | BIGINT | FK → shift_rotations(id) | Rotation ID |
| work_shift_id | BIGINT | FK → work_shifts(id) | Work Shift ID |
| sequence_order | INTEGER | NOT NULL | Order in rotation |
| PRIMARY KEY (rotation_id, work_shift_id) | | | |

#### attendance_rules
Enforcement rules for attendance (lateness, overtime).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Rule ID |
| name | VARCHAR(100) | NOT NULL | Rule name |
| rule_type | VARCHAR(30) | NOT NULL | Type: late_policy, overtime_policy, etc. |
| late_threshold_minutes | INTEGER | DEFAULT 15 | Lateness threshold |
| overtime_threshold_hours | DECIMAL(4,2) | DEFAULT 8.00 | Overtime threshold |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |

#### user_shifts
User assignments to specific shifts or rotations.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Assignment ID |
| user_id | BIGINT | FK → users(id), NOT NULL | User ID |
| work_shift_id | BIGINT | FK → work_shifts(id) | Assigned static shift |
| shift_rotation_id | BIGINT | FK → shift_rotations(id) | Assigned rotation |
| starts_on | DATE | NOT NULL | Assignment start date |
| ends_on | DATE | | Assignment end date (nullable) |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |
| CHECK (work_shift_id IS NOT NULL OR shift_rotation_id IS NOT NULL) | | | Must have shift or rotation |

**Indexes:**
- `idx_user_shifts_active` ON user_shifts(user_id) WHERE ends_on IS NULL OR ends_on > CURRENT_DATE

#### audit_logs
Comprehensive audit trail for all system actions.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Audit log ID |
| actor_id | BIGINT | FK → users(id) | User who performed action |
| action | VARCHAR(100) | NOT NULL | Action type (CREATE, UPDATE, DELETE) |
| entity | VARCHAR(100) | NOT NULL | Entity type |
| entity_id | VARCHAR(100) | | Entity ID (as string) |
| data_before | JSONB | | State before action |
| data_after | JSONB | | State after action |
| ip_address | INET | | Request IP address |
| user_agent | TEXT | | Request user agent |
| offline_flag | BOOLEAN | DEFAULT FALSE | Whether action was performed offline |
| performed_at | TIMESTAMPTZ | DEFAULT NOW(), INDEX | Action timestamp |

**Indexes:**
- `idx_audit_logs_actor_time` ON audit_logs(actor_id, performed_at DESC)
- `idx_audit_logs_entity` ON audit_logs(entity, entity_id)
- `idx_audit_logs_performed_at` ON audit_logs(performed_at DESC)

**Vector Columns:** None

---

### Reference Data & Settings

#### stations
Weighbridge stations/weighbridges.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Station ID |
| code | VARCHAR(20) | UNIQUE, NOT NULL, INDEX | Station code |
| name | VARCHAR(255) | NOT NULL | Station name |
| route_id | BIGINT | FK → routes(id) | Associated route |
| cluster_id | BIGINT | FK → clusters(id) | Associated cluster |
| bound | VARCHAR(10) | CHECK | Direction: A, B, or NULL |
| default_camera_id | BIGINT | FK → cameras(id) | Default camera |
| domain | VARCHAR(255) | | Station domain |
| ip_address | INET | | Station IP address |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |

**Indexes:**
- `idx_stations_code` ON stations(code)
- `idx_stations_active` ON stations(is_active) WHERE is_active = TRUE

#### routes
Transport routes.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Route ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL | Route code |
| name | VARCHAR(255) | NOT NULL | Route name |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |

#### organizations
Organization/Company master data.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Organization ID |
| name | VARCHAR(255) | NOT NULL | Organization name |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |

#### departments
Department master data.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Department ID |
| organization_id | BIGINT | FK → organizations(id) | Parent organization |
| name | VARCHAR(255) | NOT NULL | Department name |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |

#### clusters
Station clusters (grouping of stations).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Cluster ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL | Cluster code |
| name | VARCHAR(255) | NOT NULL | Cluster name |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |

#### vehicles
Vehicle master data.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Vehicle ID |
| reg_no | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Registration number |
| make_id | BIGINT | FK → vehicle_makes(id) | Vehicle make |
| trailer_no | VARCHAR(50) | | Trailer number |
| transporter_id | BIGINT | FK → transporters(id) | Transporter |
| axle_configuration_id | BIGINT | FK → axle_configurations(id) | Axle configuration |
| permit_no | VARCHAR(100) | | Current permit number |
| permit_issued_at | DATE | | Permit issue date |
| permit_expires_at | DATE | | Permit expiry date |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update time |

**Indexes:**
- `idx_vehicles_reg_no` ON vehicles(reg_no)
- `idx_vehicles_transporter` ON vehicles(transporter_id)

**Vector Columns:**
- `description_embedding` VECTOR(384) - Vector embedding for vehicle descriptions (for semantic search)

**Vector Indexes:**
- `idx_vehicles_description_embedding` ON vehicles USING hnsw (description_embedding vector_cosine_ops)

#### vehicle_makes
Vehicle manufacturer makes.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Make ID |
| name | VARCHAR(100) | UNIQUE, NOT NULL | Make name |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |

#### transporters
Transport company master data.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Transporter ID |
| name | VARCHAR(255) | NOT NULL, INDEX | Transporter name |
| address | TEXT | | Physical address |
| phone | VARCHAR(50) | | Contact phone |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update time |

**Indexes:**
- `idx_transporters_name` ON transporters(name)

#### drivers
Driver master data.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Driver ID |
| id_no_or_passport | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | ID/Passport number |
| license_no | VARCHAR(50) | INDEX | Driving license number |
| full_names | VARCHAR(255) | NOT NULL | Full names |
| surname | VARCHAR(100) | | Surname |
| gender | VARCHAR(10) | | Gender |
| nationality | VARCHAR(100) | | Nationality |
| age | INTEGER | | Age |
| address | TEXT | | Physical address |
| ntac_no | VARCHAR(50) | INDEX | Court case tracking number |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update time |

**Indexes:**
- `idx_drivers_id_no` ON drivers(id_no_or_passport)
- `idx_drivers_ntac` ON drivers(ntac_no) WHERE ntac_no IS NOT NULL

---

### Weighing Module

#### weighings
Main weighing transaction table (partitioned by month).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Weighing ID |
| client_local_id | UUID | UNIQUE, INDEX | Client-generated UUID (for offline sync) |
| ticket_no | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Weight ticket number |
| station_id | BIGINT | FK → stations(id) | Station |
| vehicle_id | BIGINT | FK → vehicles(id) | Vehicle |
| driver_id | BIGINT | FK → drivers(id) | Driver |
| weighing_type | VARCHAR(20) | NOT NULL, CHECK | Type: static, wim, axle |
| act_id | BIGINT | FK → act_definitions(id) | Applicable Act |
| bound | VARCHAR(10) | | Direction: A or B |
| gvw_measured_kg | INTEGER | NOT NULL | Measured GVW in kg |
| gvw_permissible_kg | INTEGER | NOT NULL | Permissible GVW in kg |
| gvw_overload_kg | INTEGER | GENERATED ALWAYS AS (gvw_measured_kg - gvw_permissible_kg) STORED | Overload amount |
| origin_id | BIGINT | FK → origins_destinations(id) | Origin |
| destination_id | BIGINT | FK → origins_destinations(id) | Destination |
| cargo_id | BIGINT | FK → cargo_types(id) | Cargo type |
| tolerance_applied | BOOLEAN | DEFAULT FALSE | Whether tolerance was applied |
| has_permit | BOOLEAN | DEFAULT FALSE | Whether vehicle has permit |
| is_compliant | BOOLEAN | DEFAULT TRUE | Compliance status |
| is_sent_to_yard | BOOLEAN | DEFAULT FALSE, INDEX | Whether sent to yard |
| reweigh_cycle_no | INTEGER | DEFAULT 1 | Reweigh cycle number |
| reweigh_limit | INTEGER | DEFAULT 8 | Maximum reweigh cycles |
| original_weighing_id | BIGINT | FK → weighings(id) | Original weighing if reweigh |
| weighed_at | TIMESTAMPTZ | DEFAULT NOW(), INDEX | Weighing timestamp (partition key) |
| weighed_by_id | BIGINT | FK → users(id) | Officer who weighed |
| sync_status | VARCHAR(20) | DEFAULT 'synced' | Sync status for offline operations |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update time |

**Partitioning:**
- Partitioned by RANGE (weighed_at)
- Monthly partitions (e.g., `weighings_2025_01`, `weighings_2025_02`)
- Partitions created automatically via pg_partman or custom scripts

**Indexes:**
- `idx_weighings_vehicle` ON weighings(vehicle_id, weighed_at DESC)
- `idx_weighings_station` ON weighings(station_id, weighed_at DESC)
- `idx_weighings_ticket` ON weighings(ticket_no)
- `idx_weighings_yard` ON weighings(is_sent_to_yard) WHERE is_sent_to_yard = TRUE
- `idx_weighings_client_local_id` ON weighings(client_local_id)

**Vector Columns:**
- `violation_reason_embedding` VECTOR(384) - Vector embedding for violation reasons (for semantic search)

**Vector Indexes:**
- `idx_weighings_violation_reason_embedding` ON weighings USING hnsw (violation_reason_embedding vector_cosine_ops)

#### weighing_axles
Individual axle weights for each weighing.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Axle weight ID |
| weighing_id | BIGINT | FK → weighings(id), NOT NULL | Weighing ID |
| axle_number | INTEGER | NOT NULL | Axle number (1, 2, 3, ...) |
| measured_kg | INTEGER | NOT NULL | Measured weight in kg |
| permissible_kg | INTEGER | NOT NULL | Permissible weight in kg |
| overload_kg | INTEGER | GENERATED ALWAYS AS (measured_kg - permissible_kg) STORED | Overload amount |
| group_name | VARCHAR(10) | | Axle group (A, B, C, D) |
| group_grouping | VARCHAR(10) | | Deck grouping reference |
| tyre_type | VARCHAR(10) | | Tyre type (S, D, W) |
| fee_usd | DECIMAL(18,2) | DEFAULT 0 | Fee in USD |
| captured_at | TIMESTAMPTZ | DEFAULT NOW() | Capture timestamp |
| UNIQUE (weighing_id, axle_number) | | | |

**Indexes:**
- `idx_weighing_axles_weighing` ON weighing_axles(weighing_id, axle_number)

#### scale_tests
Daily scale calibration tests.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Test ID |
| station_id | BIGINT | FK → stations(id) | Station |
| test_weight_kg | INTEGER | | Known test weight |
| result | VARCHAR(20) | CHECK | Result: pass, fail |
| deviation_kg | INTEGER | | Deviation from expected |
| details | TEXT | | Test details |
| carried_at | TIMESTAMPTZ | DEFAULT NOW(), INDEX | Test timestamp |
| carried_by_id | BIGINT | FK → users(id) | Officer who carried test |

**Indexes:**
- `idx_scale_tests_station_date` ON scale_tests(station_id, carried_at DESC)

---

### Prosecution Module

#### prosecution_cases
Prosecution case records.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Case ID |
| yard_entry_id | BIGINT | FK → yard_entries(id), UNIQUE | Yard entry reference |
| act_id | BIGINT | FK → act_definitions(id) | Applicable Act |
| case_no | VARCHAR(100) | UNIQUE, NOT NULL | Case number |
| ntac_no | VARCHAR(50) | INDEX | National Traffic Case Number |
| ob_no | VARCHAR(50) | | Occurrence Book Number |
| road_used | VARCHAR(255) | | Road where violation occurred |
| district | VARCHAR(100) | | District |
| county | VARCHAR(100) | | County |
| court_id | BIGINT | FK → courts(id) | Court |
| complainant_officer_id | BIGINT | FK → users(id) | Complainant officer |
| investigating_officer_id | BIGINT | FK → users(id) | Investigating officer |
| status | VARCHAR(50) | DEFAULT 'open', CHECK, INDEX | Status: open, charged, paid, escalated, closed |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update time |

**Indexes:**
- `idx_prosecution_cases_yard` ON prosecution_cases(yard_entry_id)
- `idx_prosecution_cases_status` ON prosecution_cases(status, created_at DESC)
- `idx_prosecution_cases_ntac` ON prosecution_cases(ntac_no) WHERE ntac_no IS NOT NULL

**Vector Columns:**
- `case_notes_embedding` VECTOR(384) - Vector embedding for case notes and descriptions

**Vector Indexes:**
- `idx_prosecution_cases_case_notes_embedding` ON prosecution_cases USING hnsw (case_notes_embedding vector_cosine_ops)

---

### Yard & Tags Module

#### yard_entries
Vehicles sent to holding yard.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Yard entry ID |
| weighing_id | BIGINT | FK → weighings(id), UNIQUE | Weighing reference |
| station_id | BIGINT | FK → stations(id) | Station |
| reason | VARCHAR(50) | CHECK | Reason: redistribution, gvw_overload, permit_check, offload |
| status | VARCHAR(20) | DEFAULT 'pending', CHECK, INDEX | Status: pending, processing, released, escalated |
| entered_at | TIMESTAMPTZ | DEFAULT NOW() | Entry timestamp |
| released_at | TIMESTAMPTZ | | Release timestamp |

**Indexes:**
- `idx_yard_entries_weighing` ON yard_entries(weighing_id)
- `idx_yard_entries_status` ON yard_entries(status, entered_at DESC)

#### vehicle_tags
Violation tags (automatic or manual).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | BIGSERIAL | PRIMARY KEY | Tag ID |
| reg_no | VARCHAR(50) | NOT NULL, INDEX | Vehicle registration |
| tag_type | VARCHAR(20) | CHECK | Type: automatic, manual |
| reason | TEXT | NOT NULL | Tag reason |
| station_code | VARCHAR(20) | INDEX | Station code |
| status | VARCHAR(20) | DEFAULT 'open', CHECK | Status: open, closed |
| category | VARCHAR(50) | | Tag category |
| tag_photo_path | VARCHAR(500) | | Photo path |
| effective_time_period | INTERVAL | | Duration tag is active |
| created_by_id | BIGINT | FK → users(id) | Creator |
| closed_by_id | BIGINT | FK → users(id) | Closer |
| closed_reason | TEXT | | Closure reason |
| opened_at | TIMESTAMPTZ | DEFAULT NOW(), INDEX | Open timestamp |
| closed_at | TIMESTAMPTZ | | Close timestamp |
| exported | BOOLEAN | DEFAULT FALSE | Export status |

**Indexes:**
- `idx_vehicle_tags_reg_status` ON vehicle_tags(reg_no, status)
- `idx_vehicle_tags_station` ON vehicle_tags(station_code, opened_at DESC)

**Vector Columns:**
- `reason_embedding` VECTOR(384) - Vector embedding for tag reasons

**Vector Indexes:**
- `idx_vehicle_tags_reason_embedding` ON vehicle_tags USING hnsw (reason_embedding vector_cosine_ops)

---

## Views & Materialized Views

### Materialized Views

#### charge_summaries
Pre-aggregated charge summaries for quick dashboard queries.

**Columns:**
- case_id (BIGINT)
- case_no (VARCHAR)
- best_basis (VARCHAR) - 'gvw' or 'axle'
- fee_usd (DECIMAL)
- fee_kes (DECIMAL)

**Refresh Strategy:**
- Refreshed CONCURRENTLY on invoice/receipt mutations
- Schedule: Every hour or on-demand

**Index:**
- UNIQUE INDEX on case_id

#### daily_weighing_stats
Daily weighing statistics by station.

**Columns:**
- station_id (BIGINT)
- date (DATE)
- total_weighings (BIGINT)
- compliant_count (BIGINT)
- non_compliant_count (BIGINT)
- avg_gvw_kg (DECIMAL)
- total_overload_kg (BIGINT)

**Refresh Strategy:**
- Refreshed daily at midnight
- Partition-friendly aggregation

### Views

#### active_vehicle_tags
View of currently active (open) vehicle tags.

**Filter:**
- status = 'open'
- opened_at within effective_time_period

#### yard_status_summary
View of current yard status by station.

**Columns:**
- station_id (BIGINT)
- status (VARCHAR)
- count (BIGINT)

---

## Vector Columns Summary

Vector columns are used for semantic search of text fields using pgvector. All vector columns use:
- **Data Type:** VECTOR(384) - Dimensions for all-MiniLM-L12-v2 model
- **Index Type:** HNSW (Hierarchical Navigable Small World) for efficient similarity search
- **Similarity Function:** Cosine similarity (vector_cosine_ops)

**Tables with Vector Columns:**
1. `vehicles.description_embedding` - Vehicle descriptions
2. `weighings.violation_reason_embedding` - Violation reasons
3. `prosecution_cases.case_notes_embedding` - Case notes and descriptions
4. `vehicle_tags.reason_embedding` - Tag reasons

**Vector Embedding Generation:**
- Embeddings generated server-side using ONNX Runtime
- Model: all-MiniLM-L12-v2 (384 dimensions)
- Embeddings updated on text field changes
- Background jobs refresh embeddings periodically

---

## Relationship Diagram Summary

**Key Relationships:**
- `users` ↔ `roles` (many-to-many via `user_roles`)
- `users` ↔ `shifts` (many-to-many via `user_shifts`)
- `users` ↔ `weighings` (one-to-many, weighed_by_id)
- `vehicles` ↔ `weighings` (one-to-many)
- `weighings` ↔ `weighing_axles` (one-to-many)
- `weighings` ↔ `yard_entries` (one-to-one)
- `yard_entries` ↔ `prosecution_cases` (one-to-one)
- `stations` ↔ `weighings` (one-to-many)
- `stations` ↔ `scale_tests` (one-to-many)

---

## Index Strategy Summary

**Primary Indexes:**
- All tables have primary key on `id` (BIGSERIAL)

**Foreign Key Indexes:**
- All foreign keys indexed for join performance

**Query-Specific Indexes:**
- Date-based indexes for time-range queries
- Status-based partial indexes for filtered queries
- Vector indexes for semantic search
- Unique indexes for business keys (ticket_no, case_no, etc.)

**Composite Indexes:**
- (entity_id, timestamp DESC) for history queries
- (status, created_at DESC) for status-based listings

---

## Sync & Offline Support

**Tables Supporting Offline Sync:**
- `weighings` - Includes `client_local_id` (UUID) and `sync_status`
- `device_sync_events` - Queue for offline submissions

**Sync Metadata:**
- `client_local_id` - Client-generated UUID for idempotency
- `sync_status` - Status: queued, synced, failed
- `sync_at` - Last sync timestamp

**Idempotency:**
- Client-generated UUIDs prevent duplicate submissions
- Backend validates `client_local_id` uniqueness
- Duplicate detection via correlation_id in `device_sync_events`

