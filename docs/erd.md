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

TruLoad backend integrates with the centralized `auth-service` for authentication while maintaining application-level user management (roles, shifts, permissions) locally. This approach avoids entity redundancy while ensuring seamless Single Sign-On (SSO) across micro-services.

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
- Seamless SSO across all micro services
- Flexible role and permission management per service

---

---

## Core Entities

### User Management & Security

#### users
Application-level user management with synchronization to centralized auth-service.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Local user ID |
| auth_service_user_id | UUID | UNIQUE, INDEX | Reference to centralized auth-service user |
| email | VARCHAR(255) | UNIQUE, NOT NULL, INDEX | User email (synced from auth-service) |
| phone | VARCHAR(50) | | Contact phone |
| full_name | VARCHAR(255) | NOT NULL | Full name |
| status | VARCHAR(20) | DEFAULT 'active', CHECK | Status: active, inactive, locked |
| station_id | UUID | FK → stations(id), INDEX | Assigned station (nullable) |
| organization_id | UUID | FK → organizations(id), INDEX | Organization/Company |
| department_id | UUID | FK → departments(id), INDEX | Department |
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
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Role ID |
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
| user_id | UUID | FK → users(id), PRIMARY KEY | User ID |
| role_id | UUID | FK → roles(id), PRIMARY KEY | Role ID |
| assigned_at | TIMESTAMPTZ | DEFAULT NOW() | Assignment timestamp |

**Relationships:**
- Many-to-many between `users` and `roles`

#### work_shifts
Work shift definitions (e.g., "Morning Shift", "Night Shift").

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Shift ID |
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
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Schedule ID |
| work_shift_id | UUID | FK → work_shifts(id), NOT NULL | Parent shift |
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
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Rotation ID |
| title | VARCHAR(255) | NOT NULL | Rotation title |
| current_active_shift_id | UUID | FK → work_shifts(id) | Currently active shift |
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
| rotation_id | UUID | FK → shift_rotations(id) | Rotation ID |
| work_shift_id | UUID | FK → work_shifts(id) | Work Shift ID |
| sequence_order | INTEGER | NOT NULL | Order in rotation |
| PRIMARY KEY (rotation_id, work_shift_id) | | | |

#### attendance_rules
Enforcement rules for attendance (lateness, overtime).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Rule ID |
| name | VARCHAR(100) | NOT NULL | Rule name |
| rule_type | VARCHAR(30) | NOT NULL | Type: late_policy, overtime_policy, etc. |
| late_threshold_minutes | INTEGER | DEFAULT 15 | Lateness threshold |
| overtime_threshold_hours | DECIMAL(4,2) | DEFAULT 8.00 | Overtime threshold |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |

#### user_shifts
User assignments to specific shifts or rotations.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Assignment ID |
| user_id | UUID | FK → users(id), NOT NULL | User ID |
| work_shift_id | UUID | FK → work_shifts(id) | Assigned static shift |
| shift_rotation_id | UUID | FK → shift_rotations(id) | Assigned rotation |
| starts_on | DATE | NOT NULL | Assignment start date |
| ends_on | DATE | | Assignment end date (nullable) |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |
| CHECK (work_shift_id IS NOT NULL OR shift_rotation_id IS NOT NULL) | | | Must have shift or rotation |

**Indexes:**
- `idx_user_shifts_active` ON user_shifts(user_id) WHERE ends_on IS NULL OR ends_on > CURRENT_DATE

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
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Weighing ID (Client-generated) |
| ticket_no | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Weight ticket number |
| station_id | UUID | FK → stations(id) | Station |
| vehicle_id | UUID | FK → vehicles(id) | Vehicle |
| driver_id | UUID | FK → drivers(id) | Driver |
| weighing_type | VARCHAR(20) | NOT NULL, CHECK | Type: static, wim, axle |
| act_id | UUID | FK → act_definitions(id) | Applicable Act |
| bound | VARCHAR(10) | | Direction: A or B |
| gvw_measured_kg | INTEGER | NOT NULL | Measured GVW in kg |
| gvw_permissible_kg | INTEGER | NOT NULL | Permissible GVW in kg |
| gvw_overload_kg | INTEGER | GENERATED ALWAYS AS (gvw_measured_kg - gvw_permissible_kg) STORED | Overload amount |
| origin_id | UUID | FK → origins_destinations(id) | Origin |
| destination_id | UUID | FK → origins_destinations(id) | Destination |
| cargo_id | UUID | FK → cargo_types(id) | Cargo type |
| tolerance_applied | BOOLEAN | DEFAULT FALSE | Whether tolerance was applied |
| has_permit | BOOLEAN | DEFAULT FALSE | Whether vehicle has permit |
| is_compliant | BOOLEAN | DEFAULT TRUE | Compliance status |
| is_sent_to_yard | BOOLEAN | DEFAULT FALSE, INDEX | Whether sent to yard |
| reweigh_cycle_no | INTEGER | DEFAULT 1 | Reweigh cycle number |
| reweigh_limit | INTEGER | DEFAULT 8 | Maximum reweigh cycles |
| original_weighing_id | UUID | FK → weighings(id) | Original weighing if reweigh |
| weighed_at | TIMESTAMPTZ | DEFAULT NOW(), INDEX | Weighing timestamp (partition key) |
| weighed_by_id | UUID | FK → users(id) | Officer who weighed |
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

**Vector Columns:**
- `violation_reason_embedding` VECTOR(384) - Vector embedding for violation reasons (for semantic search)

**Vector Indexes:**
- `idx_weighings_violation_reason_embedding` ON weighings USING hnsw (violation_reason_embedding vector_cosine_ops)

#### weighing_axles
Individual axle weights for each weighing.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Axle weight ID |
| weighing_id | UUID | FK → weighings(id), NOT NULL | Weighing ID |
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
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Test ID |
| station_id | UUID | FK → stations(id) | Station |
| test_weight_kg | INTEGER | | Known test weight |
| result | VARCHAR(20) | CHECK | Result: pass, fail |
| deviation_kg | INTEGER | | Deviation from expected |
| details | TEXT | | Test details |
| carried_at | TIMESTAMPTZ | DEFAULT NOW(), INDEX | Test timestamp |
| carried_by_id | UUID | FK → users(id) | Officer who carried test |

**Indexes:**
- `idx_scale_tests_station_date` ON scale_tests(station_id, carried_at DESC)

---

### Reference Data Module

#### counties
County master data for geographic organization.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | County ID |
| code | VARCHAR(20) | UNIQUE, NOT NULL, INDEX | County code |
| name | VARCHAR(255) | NOT NULL | County name |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_counties_code` ON counties(code)
- `idx_counties_active` ON counties(is_active) WHERE is_active = TRUE

#### districts
District master data within counties.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | District ID |
| county_id | UUID | FK → counties(id), NOT NULL, INDEX | Parent county |
| code | VARCHAR(20) | UNIQUE, NOT NULL, INDEX | District code |
| name | VARCHAR(255) | NOT NULL | District name |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_districts_code` ON districts(code)
- `idx_districts_county` ON districts(county_id)

**Relationships:**
- Many-to-one with `counties`

#### subcounties
Subcounty master data within districts.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Subcounty ID |
| district_id | UUID | FK → districts(id), NOT NULL, INDEX | Parent district |
| code | VARCHAR(20) | UNIQUE, NOT NULL, INDEX | Subcounty code |
| name | VARCHAR(255) | NOT NULL | Subcounty name |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_subcounties_code` ON subcounties(code)
- `idx_subcounties_district` ON subcounties(district_id)

**Relationships:**
- Many-to-one with `districts`

| status | VARCHAR(20) | DEFAULT 'active', CHECK | Status: active, expired, revoked |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |

---

### Case Management Module

#### case_registers
Central register for all violation cases (Subfile A). Auto-created from weighing or manually created by officer.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Case register ID |
| case_no | VARCHAR(100) | UNIQUE, NOT NULL, INDEX | Case number |
| weighing_id | UUID | FK → weighings(id), UNIQUE | Related weighing (nullable for manual entries) |
| yard_entry_id | UUID | FK → yard_entries(id), UNIQUE | Related yard entry |
| prohibition_order_id | UUID | FK → prohibition_orders(id) | Prohibition order reference |
| vehicle_id | UUID | FK → vehicles(id), NOT NULL, INDEX | Vehicle |
| driver_id | UUID | FK → drivers(id), INDEX | Driver |
| violation_type_id | UUID | FK → violation_types(id), NOT NULL, INDEX | Violation type (FK to taxonomy) |
| road_id | UUID | FK → roads(id), INDEX | Road where violation occurred |
| county_id | UUID | FK → counties(id), INDEX | County |
| district_id | UUID | FK → districts(id), INDEX | District |
| subcounty_id | UUID | FK → subcounties(id), INDEX | Subcounty |
| violation_details | TEXT | | Detailed description |
| act_id | UUID | FK → act_definitions(id) | Applicable Act (EAC or Traffic) |
| driver_ntac_no | VARCHAR(50) | INDEX | NTAC number served to Driver |
| transporter_ntac_no | VARCHAR(50) | INDEX | NTAC number served to Transporter |
| ob_no | VARCHAR(50) | | Occurrence Book number |
| court_id | UUID | FK → courts(id), INDEX | Assigned court |
| disposition_type | VARCHAR(50) | CHECK | Disposition: special_release, paid, court, pending |
| status | VARCHAR(30) | DEFAULT 'open', CHECK, INDEX | Status: open, pending, closed, escalated |
| escalated_to_case_manager | BOOLEAN | DEFAULT FALSE, INDEX | Whether escalated to formal case management |
| case_manager_id | UUID | FK → case_managers(id) | Assigned case manager (Prosecutor/Legal Liaison) |
| prosecutor_id | UUID | FK → users(id) | Assigned prosecutor (if different from case manager) |
| complainant_officer_id | UUID | FK → users(id) | Complainant officer (Witness 1) |
| investigating_officer_id | UUID | FK → users(id) | Investigating officer (Required ONLY for Court Escalation) |
| investigating_officer_assigned_by_id | UUID | FK → users(id) | Supervisor who assigned IO (Court cases only) |
| investigating_officer_assigned_at | TIMESTAMPTZ | | Assignment timestamp (Court cases only) |
| created_by_id | UUID | FK → users(id) | Officer who created |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Creation timestamp |
| closed_at | TIMESTAMPTZ | | Closure timestamp |
| closed_by_id | UUID | FK → users(id) | Officer who closed |
| closing_reason | TEXT | | Closure reason/notes |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update time |

**Indexes:**
- `idx_case_registers_case_no` ON case_registers(case_no)
- `idx_case_registers_status` ON case_registers(status, created_at DESC)
- `idx_case_registers_weighing` ON case_registers(weighing_id) WHERE weighing_id IS NOT NULL
- `idx_case_registers_vehicle` ON case_registers(vehicle_id, created_at DESC)
- `idx_case_registers_violation_type` ON case_registers(violation_type_id)
- `idx_case_registers_road` ON case_registers(road_id) WHERE road_id IS NOT NULL
- `idx_case_registers_county` ON case_registers(county_id) WHERE county_id IS NOT NULL
- `idx_case_registers_court` ON case_registers(court_id) WHERE court_id IS NOT NULL
- `idx_case_registers_driver_ntac` ON case_registers(driver_ntac_no) WHERE driver_ntac_no IS NOT NULL
- `idx_case_registers_transporter_ntac` ON case_registers(transporter_ntac_no) WHERE transporter_ntac_no IS NOT NULL
- `idx_case_registers_escalated` ON case_registers(escalated_to_case_manager) WHERE escalated_to_case_manager = TRUE

**Vector Columns:**
- `violation_details_embedding` VECTOR(384) - Vector embedding for violation details (for semantic search)

**Vector Indexes:**
- `idx_case_registers_violation_details_embedding` ON case_registers USING hnsw (violation_details_embedding vector_cosine_ops)

**Relationships:**
- One-to-one with `weighings` (optional)
- One-to-one with `yard_entries` (optional)
- One-to-one with `prohibition_orders` (optional)
- Many-to-one with `violation_types`
- Many-to-one with `roads` (optional)
- Many-to-one with `counties` (optional)
- Many-to-one with `districts` (optional)
- Many-to-one with `subcounties` (optional)
- Many-to-one with `courts` (optional)
- Many-to-one with `act_definitions`
- One-to-many with `special_releases`
- One-to-many with `case_subfiles`
- One-to-many with `arrest_warrants`
- One-to-many with `court_hearings`
- One-to-one with `case_closure_checklists`

#### special_releases
Special release records for compliant/redistribution cases (fast-path dispositions).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Special release ID |
| case_register_id | UUID | FK → case_registers(id), UNIQUE, NOT NULL, INDEX | Case reference |
| certificate_no | VARCHAR(100) | UNIQUE, NOT NULL, INDEX | Certificate number |
| release_type | VARCHAR(50) | CHECK | Type: redistribution, tolerance, permit_valid, admin_discretion |
| overload_kg | INTEGER | | Original overload amount |
| redistribution_allowed | BOOLEAN | DEFAULT FALSE | Whether redistribution allowed |
| reweigh_required | BOOLEAN | DEFAULT FALSE | Whether reweigh required |
| reweigh_weighing_id | UUID | FK → weighings(id) | Reweigh reference |
| compliance_achieved | BOOLEAN | DEFAULT FALSE | Whether reweigh compliant |
| reason | TEXT | NOT NULL | Release reason/justification |
| authorized_by_id | UUID | FK → users(id), NOT NULL | Authorizing supervisor |
| issued_at | TIMESTAMPTZ | DEFAULT NOW() | Issue timestamp |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_special_releases_case` ON special_releases(case_register_id)
- `idx_special_releases_cert` ON special_releases(certificate_no)
- `idx_special_releases_issued` ON special_releases(issued_at DESC)

**Relationships:**
- Many-to-one with `case_registers`
- Many-to-one with `weighings` (reweigh)

#### case_managers
Case manager/prosecutor assignments.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Case manager ID |
| user_id | UUID | FK → users(id), NOT NULL, INDEX | User reference |
| role_type | VARCHAR(30) | CHECK | Role: case_manager, prosecutor, investigator |
| specialization | VARCHAR(100) | | Area of specialization |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_case_managers_user` ON case_managers(user_id) WHERE is_active = TRUE
- `idx_case_managers_role` ON case_managers(role_type, is_active)

**Relationships:**
- Many-to-one with `users`
- One-to-many with `case_registers`

#### case_assignment_logs
Audit trail for case officer assignments and re-assignments.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Log ID |
| case_register_id | UUID | FK → case_registers(id), NOT NULL, INDEX | Case reference |
| previous_officer_id | UUID | FK → users(id) | Previous officer (nullable for first assignment) |
| new_officer_id | UUID | FK → users(id), NOT NULL, INDEX | New officer assigned |
| assigned_by_id | UUID | FK → users(id), NOT NULL | Supervisor who made assignment |
| assignment_type | VARCHAR(50) | CHECK | Type: initial, re_assignment, transfer |
| reason | TEXT | NOT NULL | Reason for assignment/change |
| assigned_at | TIMESTAMPTZ | DEFAULT NOW(), INDEX | Assignment timestamp |

**Indexes:**
- `idx_case_assignment_case` ON case_assignment_logs(case_register_id, assigned_at DESC)
- `idx_case_assignment_officer` ON case_assignment_logs(new_officer_id)

**Relationships:**
- Many-to-one with `case_registers`

#### case_subfiles
Subfiles B through J (Document Evidence, Expert Reports, Witness Statements, etc.).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Subfile ID |
| case_register_id | UUID | FK → case_registers(id), NOT NULL, INDEX | Case reference |
| subfile_type | VARCHAR(2) | CHECK | Subfile: B, C, D, E, F, G, H, I, J |
| subfile_name | VARCHAR(100) | | Subfile document name |
| document_type | VARCHAR(100) | | Type: evidence, report, statement, diary, charge, bond, minute, etc. |
| content | TEXT | | Text content |
| file_path | VARCHAR(500) | | File storage path |
| file_url | VARCHAR(500) | | File URL |
| mime_type | VARCHAR(100) | | File MIME type |
| file_size_bytes | BIGINT | | File size |
| checksum | VARCHAR(64) | | File checksum (SHA-256) |
| uploaded_by_id | UUID | FK → users(id) | Uploader |
| uploaded_at | TIMESTAMPTZ | DEFAULT NOW() | Upload timestamp |
| metadata | JSONB | | Additional metadata (JSON) |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_case_subfiles_case_type` ON case_subfiles(case_register_id, subfile_type)
- `idx_case_subfiles_uploaded` ON case_subfiles(uploaded_at DESC)

**Vector Columns:**
- `content_embedding` VECTOR(384) - Vector embedding for document content (for semantic search)

**Vector Indexes:**
- `idx_case_subfiles_content_embedding` ON case_subfiles USING hnsw (content_embedding vector_cosine_ops)

**Relationships:**
- Many-to-one with `case_registers`

#### arrest_warrants
Arrest warrant tracking (part of Subfile G).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Warrant ID |
| case_register_id | UUID | FK → case_registers(id), NOT NULL, INDEX | Case reference |
| warrant_no | VARCHAR(100) | UNIQUE, NOT NULL, INDEX | Warrant number |
| issued_by | VARCHAR(255) | | Issuing authority (court/magistrate) |
| accused_name | VARCHAR(255) | NOT NULL | Accused person name |
| accused_id_no | VARCHAR(50) | INDEX | Accused ID/Passport number |
| offence_description | TEXT | | Offence description |
| warrant_status | VARCHAR(30) | DEFAULT 'issued', CHECK, INDEX | Status: issued, active, executed, dropped |
| issued_at | TIMESTAMPTZ | NOT NULL, INDEX | Issue date |
| executed_at | TIMESTAMPTZ | | Execution date |
| dropped_at | TIMESTAMPTZ | | Dropped date |
| execution_details | TEXT | | Execution notes |
| dropped_reason | TEXT | | Drop reason |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update |

**Indexes:**
- `idx_arrest_warrants_case` ON arrest_warrants(case_register_id)
- `idx_arrest_warrants_status` ON arrest_warrants(warrant_status, issued_at DESC)
- `idx_arrest_warrants_accused` ON arrest_warrants(accused_id_no) WHERE accused_id_no IS NOT NULL

**Relationships:**
- Many-to-one with `case_registers`

#### court_hearings
Court hearing schedule and minutes (part of Subfile J).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Hearing ID |
| case_register_id | UUID | FK → case_registers(id), NOT NULL, INDEX | Case reference |
| court_id | UUID | FK → courts(id), INDEX | Court |
| hearing_date | DATE | NOT NULL, INDEX | Scheduled date |
| hearing_time | TIME | | Scheduled time |
| hearing_type | VARCHAR(50) | CHECK | Type: mention, hearing, judgment, ruling, bail, etc. |
| status | VARCHAR(30) | DEFAULT 'scheduled', CHECK | Status: scheduled, held, adjourned, cancelled |
| outcome | VARCHAR(50) | | Outcome: adjourned, ruling, convicted, acquitted, etc. |
| minute_notes | TEXT | | Minute sheet notes |
| next_hearing_date | DATE | | Next hearing date |
| adjournment_reason | TEXT | | Adjournment reason |
| presiding_officer | VARCHAR(255) | | Magistrate/Judge name |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Last update |

**Indexes:**
- `idx_court_hearings_case_date` ON court_hearings(case_register_id, hearing_date DESC)
- `idx_court_hearings_status_date` ON court_hearings(status, hearing_date)
- `idx_court_hearings_court` ON court_hearings(court_id, hearing_date DESC)

**Vector Columns:**
- `minute_notes_embedding` VECTOR(384) - Vector embedding for minute notes (for semantic search)

**Vector Indexes:**
- `idx_court_hearings_minute_notes_embedding` ON court_hearings USING hnsw (minute_notes_embedding vector_cosine_ops)

**Relationships:**
- Many-to-one with `case_registers`
- Many-to-one with `courts`

#### case_closure_checklists
Closure requirement tracking by disposition type (validates Subfiles A-J completeness).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Checklist ID |
| case_register_id | UUID | FK → case_registers(id), UNIQUE, NOT NULL | Case reference |
| closure_type | VARCHAR(50) | CHECK | Type: withdrawn, discharged, paid, jailed |
| cpc_section_id | UUID | FK → cpc_sections(id) | CPC section reference (FK) |
| pc_section_id | UUID | FK → pc_sections(id) | Penal Code section reference (FK) |
| required_subfiles | VARCHAR(50)[] | | Required subfile types array (e.g., {A, I, J}) |
| subfile_a_complete | BOOLEAN | DEFAULT FALSE | Subfile A (Initial Case Details) |
| subfile_b_complete | BOOLEAN | DEFAULT FALSE | Subfile B (Document Evidence) |
| subfile_c_complete | BOOLEAN | DEFAULT FALSE | Subfile C (Expert Reports) |
| subfile_d_complete | BOOLEAN | DEFAULT FALSE | Subfile D (Witness Statements) |
| subfile_e_complete | BOOLEAN | DEFAULT FALSE | Subfile E (Accused Statements) |
| subfile_f_complete | BOOLEAN | DEFAULT FALSE | Subfile F (Investigation Diary) |
| subfile_g_complete | BOOLEAN | DEFAULT FALSE | Subfile G (Charge Sheets/Warrants) |
| subfile_h_complete | BOOLEAN | DEFAULT FALSE | Subfile H (Accused Records) |
| subfile_i_complete | BOOLEAN | DEFAULT FALSE | Subfile I (Covering Report) |
| subfile_j_complete | BOOLEAN | DEFAULT FALSE | Subfile J (Minute Sheets) |
| all_required_complete | BOOLEAN | DEFAULT FALSE | Whether all required subfiles complete |
| review_status | VARCHAR(20) | DEFAULT 'none', CHECK | Status: none, requested, approved, rejected |
| review_requested_at | TIMESTAMPTZ | | Review request timestamp |
| review_requested_by_id | UUID | FK → users(id) | Officer requesting review |
| review_notes | TEXT | | Notes from reviewer |
| approved_by_id | UUID | FK → users(id) | Approving officer (Supervisor) |
| approved_at | TIMESTAMPTZ | | Approval timestamp |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update |

**Indexes:**
- `idx_case_closure_case` ON case_closure_checklists(case_register_id)
- `idx_case_closure_complete` ON case_closure_checklists(all_required_complete)
- `idx_case_closure_review` ON case_closure_checklists(review_status)

**Relationships:**
- One-to-one with `case_registers`

#### prohibition_orders
Prohibition order documents (extracted from weighing flow for better tracking).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Order ID |
| order_no | VARCHAR(100) | UNIQUE, NOT NULL, INDEX | Order number |
| weighing_id | UUID | FK → weighings(id), UNIQUE, INDEX | Related weighing |
| vehicle_id | UUID | FK → vehicles(id), NOT NULL, INDEX | Vehicle |
| driver_id | UUID | FK → drivers(id), INDEX | Driver |
| issued_by_id | UUID | FK → users(id) | Issuing officer |
| issued_at | TIMESTAMPTZ | DEFAULT NOW(), INDEX | Issue timestamp |
| reason | TEXT | | Prohibition reason |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_prohibition_orders_order_no` ON prohibition_orders(order_no)
- `idx_prohibition_orders_weighing` ON prohibition_orders(weighing_id)
- `idx_prohibition_orders_vehicle` ON prohibition_orders(vehicle_id, issued_at DESC)

**Relationships:**
- One-to-one with `weighings`
- One-to-many with `case_registers`

#### load_correction_memos
Load correction/redistribution memos.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Memo ID |
| memo_no | VARCHAR(100) | UNIQUE, NOT NULL, INDEX | Memo number |
| case_register_id | UUID | FK → case_registers(id), INDEX | Case reference |
| weighing_id | UUID | FK → weighings(id), INDEX | Original weighing |
| overload_kg | INTEGER | NOT NULL | Overload to correct |
| redistribution_type | VARCHAR(50) | CHECK | Type: offload, redistribute |
| reweigh_scheduled_at | TIMESTAMPTZ | | Scheduled reweigh time |
| reweigh_weighing_id | UUID | FK → weighings(id) | Reweigh reference |
| compliance_achieved | BOOLEAN | DEFAULT FALSE | Compliance status |
| issued_by_id | UUID | FK → users(id) | Issuing officer |
| issued_at | TIMESTAMPTZ | DEFAULT NOW(), INDEX | Issue timestamp |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_load_correction_memo_no` ON load_correction_memos(memo_no)
- `idx_load_correction_case` ON load_correction_memos(case_register_id)
- `idx_load_correction_weighing` ON load_correction_memos(weighing_id)

**Relationships:**
- Many-to-one with `case_registers`
- Many-to-one with `weighings` (original)
- Many-to-one with `weighings` (reweigh)
- One-to-many with `compliance_certificates`

#### compliance_certificates
Compliance certificates issued after successful reweigh.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Certificate ID |
| certificate_no | VARCHAR(100) | UNIQUE, NOT NULL, INDEX | Certificate number |
| case_register_id | UUID | FK → case_registers(id), INDEX | Case reference |
| weighing_id | UUID | FK → weighings(id), INDEX | Compliant weighing |
| load_correction_memo_id | UUID | FK → load_correction_memos(id), INDEX | Related memo |
| issued_by_id | UUID | FK → users(id) | Issuing officer |
| issued_at | TIMESTAMPTZ | DEFAULT NOW(), INDEX | Issue timestamp |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_compliance_cert_no` ON compliance_certificates(certificate_no)
- `idx_compliance_case` ON compliance_certificates(case_register_id)
- `idx_compliance_weighing` ON compliance_certificates(weighing_id)

**Relationships:**
- Many-to-one with `case_registers`
- Many-to-one with `weighings`
- Many-to-one with `load_correction_memos`

#### courts
Court master data (referenced by prosecution_cases and court_hearings).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Court ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL | Court code |
| name | VARCHAR(255) | NOT NULL | Court name |
| location | VARCHAR(255) | | Court location |
| court_type | VARCHAR(50) | CHECK | Type: magistrate, high_court, etc. |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_courts_code` ON courts(code)
- `idx_courts_active` ON courts(is_active) WHERE is_active = TRUE

---

### Yard & Tags Module

#### yard_entries
Vehicles sent to holding yard.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Yard entry ID |
| weighing_id | UUID | FK → weighings(id), UNIQUE | Weighing reference |
| station_id | UUID | FK → stations(id) | Station |
| reason | VARCHAR(50) | CHECK | Reason: redistribution, gvw_overload, permit_check, offload |
| status | VARCHAR(20) | DEFAULT 'pending', CHECK, INDEX | Status: pending, processing, released, escalated |
| entered_at | TIMESTAMPTZ | DEFAULT NOW() | Entry timestamp |
| released_at | TIMESTAMPTZ | | Release timestamp |

**Indexes:**
- `idx_yard_entries_weighing` ON yard_entries(weighing_id)
- `idx_yard_entries_status` ON yard_entries(status, entered_at DESC)

#### vehicle_tags
Violation tags (automatic or manual).

| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Tag ID |
| reg_no | VARCHAR(50) | NOT NULL, INDEX | Vehicle registration |
| tag_type | VARCHAR(20) | CHECK | Type: automatic, manual |
| tag_category_id | UUID | FK → tag_categories(id), INDEX | Tag category (FK to taxonomy) |
| reason | TEXT | NOT NULL | Tag reason |
| station_code | VARCHAR(20) | INDEX | Station code |
| status | VARCHAR(20) | DEFAULT 'open', CHECK | Status: open, closed |
| tag_photo_path | VARCHAR(500) | | Photo path |
| effective_time_period | INTERVAL | | Duration tag is active |
| created_by_id | UUID | FK → users(id) | Creator |
| closed_by_id | UUID | FK → users(id) | Closer |
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

### Financial Module

#### invoices
Generated invoices for violations or services.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Invoice ID |
| invoice_no | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Invoice number |
| case_register_id | UUID | FK → case_registers(id), INDEX | Related case (optional) |
| weighing_id | UUID | FK → weighings(id), INDEX | Related weighing (optional) |
| amount_due | DECIMAL(18,2) | NOT NULL | Amount due |
| currency | VARCHAR(3) | DEFAULT 'USD' | Currency code |
| status | VARCHAR(20) | DEFAULT 'pending', CHECK, INDEX | Status: pending, paid, cancelled, void |
| generated_at | TIMESTAMPTZ | DEFAULT NOW() | Generation timestamp |
| due_date | DATE | | Due date |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update |

**Indexes:**
- `idx_invoices_no` ON invoices(invoice_no)
- `idx_invoices_case` ON invoices(case_register_id)
- `idx_invoices_status` ON invoices(status)

#### receipts
Payment receipts with idempotency support.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Receipt ID |
| receipt_no | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Receipt number |
| invoice_id | UUID | FK → invoices(id), NOT NULL, INDEX | Related invoice |
| amount_paid | DECIMAL(18,2) | NOT NULL | Amount paid |
| currency | VARCHAR(3) | DEFAULT 'USD' | Currency code |
| payment_method | VARCHAR(50) | NOT NULL | Method: cash, mobile_money, bank_transfer, card |
| transaction_reference | VARCHAR(100) | UNIQUE, INDEX | External transaction ref (e.g., M-Pesa code) |
| idempotency_key | UUID | UNIQUE, INDEX | Client-generated key for duplicate prevention |
| received_by_id | UUID | FK → users(id) | Officer who received payment |
| payment_date | TIMESTAMPTZ | DEFAULT NOW(), INDEX | Payment timestamp |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_receipts_no` ON receipts(receipt_no)
- `idx_receipts_invoice` ON receipts(invoice_id)
- `idx_receipts_transaction` ON receipts(transaction_reference)
- `idx_receipts_idempotency` ON receipts(idempotency_key)

**Relationships:**
- One-to-many with `invoices`
- `invoices` ↔ `case_registers` (many-to-one)
- `invoices` ↔ `weighings` (many-to-one)

---

## Views & Materialized Views

### Materialized Views

#### charge_summaries
Pre-aggregated charge summaries for quick dashboard queries.

**Columns:**
- case_id (UUID)
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
- station_id (UUID)
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
- station_id (UUID)
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
5. `case_registers.violation_details_embedding` - Violation details (Case Register)
6. `case_subfiles.content_embedding` - Document content (Subfiles B-J)
7. `court_hearings.minute_notes_embedding` - Court minute notes

**Vector Embedding Generation:**
- Embeddings generated server-side using ONNX Runtime
- Model: all-MiniLM-L12-v2 (384 dimensions)
- Embeddings updated on text field changes
- Background jobs refresh embeddings periodically

---

**Key Relationships:**

**User Management:**
- `users` ↔ `roles` (many-to-many via `user_roles`)
- `users` ↔ `shifts` (many-to-many via `user_shifts`)
- `users` ↔ `weighings` (one-to-many, weighed_by_id)
- `users` ↔ `case_managers` (one-to-many)

**Weighing Flow:**
- `vehicles` ↔ `weighings` (one-to-many)
- `weighings` ↔ `weighing_axles` (one-to-many)
- `weighings` ↔ `prohibition_orders` (one-to-one)
- `weighings` ↔ `yard_entries` (one-to-one)
- `stations` ↔ `weighings` (one-to-many)
- `stations` ↔ `scale_tests` (one-to-many)

**Case Management Flow (case_registers is central hub):**
- `weighings` → `prohibition_orders` → `case_registers` (chain for violation cases)
- `yard_entries` → `case_registers` (one-to-one)
- `case_registers` ↔ `special_releases` (one-to-many)
- `case_registers` ↔ `case_subfiles` (one-to-many, Subfiles B-J)
- `case_registers` ↔ `arrest_warrants` (one-to-many)
- `case_registers` ↔ `court_hearings` (one-to-many)
- `case_registers` ↔ `case_closure_checklists` (one-to-one)
- `case_registers` ↔ `load_correction_memos` (one-to-many)
- `case_registers` ↔ `compliance_certificates` (one-to-many)
- `case_managers` ↔ `case_registers` (one-to-many)

**Geographic & Reference Data:**
- `counties` ↔ `districts` (one-to-many)
- `districts` ↔ `subcounties` (one-to-many)
- `districts` ↔ `roads` (one-to-many)
- `case_registers` ↔ `violation_types` (many-to-one)
- `case_registers` ↔ `roads` (many-to-one, optional)
- `case_registers` ↔ `counties` (many-to-one, optional)
- `case_registers` ↔ `districts` (many-to-one, optional)
- `case_registers` ↔ `subcounties` (many-to-one, optional)
- `case_registers` ↔ `act_definitions` (many-to-one)
- `case_closure_checklists` ↔ `cpc_sections` (many-to-one)
- `case_closure_checklists` ↔ `pc_sections` (many-to-one)
- `vehicle_tags` ↔ `tag_categories` (many-to-one)

**Court Management:**
- `courts` ↔ `court_hearings` (one-to-many)
- `courts` ↔ `case_registers` (one-to-many)

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

