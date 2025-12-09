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

#### organizations
Organizations/companies (transporters, government agencies, etc.).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Organization ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Organization code |
| name | VARCHAR(255) | NOT NULL | Organization name |
| org_type | VARCHAR(50) | CHECK | Type: government, transporter, contractor |
| contact_email | VARCHAR(255) | | Contact email |
| contact_phone | VARCHAR(50) | | Contact phone |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_organizations_code` ON organizations(code)
- `idx_organizations_active` ON organizations(is_active) WHERE is_active = TRUE

#### departments
Departments within organizations.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Department ID |
| organization_id | UUID | FK → organizations(id), NOT NULL, INDEX | Parent organization |
| code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Department code |
| name | VARCHAR(255) | NOT NULL | Department name |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_departments_code` ON departments(code)
- `idx_departments_org` ON departments(organization_id)

**Relationships:**
- Many-to-one with `organizations`

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

#### permissions
Fine-grained permissions model for RBAC.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Permission ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Permission code (e.g., "weighing.create") |
| name | VARCHAR(255) | NOT NULL | Display name (e.g., "Create Weighing") |
| category | VARCHAR(50) | NOT NULL, INDEX | Category: weighing, case, prosecution, user, station, config, report, system |
| description | TEXT | | Detailed description |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |

**Indexes:**
- `idx_permissions_code` ON permissions(code) UNIQUE
- `idx_permissions_category` ON permissions(category) WHERE is_active = TRUE
- `idx_permissions_active` ON permissions(is_active)

**Seed Data (77 Total Permissions):**
- **Weighing (12):** weighing.{create, read, read_own, update, approve, override, send_to_yard, scale_test, export, delete, webhook, audit}
- **Case (15):** case.{create, read, read_own, update, assign, close, escalate, special_release, subfile_manage, closure_review, arrest_warrant, court_hearing, reweigh_schedule, export, audit}
- **Prosecution (8):** prosecution.{create, read, read_own, update, compute_charges, generate_certificate, export, audit}
- **User (10):** user.{create, read, read_own, update, update_own, delete, assign_roles, manage_permissions, manage_shifts, audit}
- **Station (12):** station.{read, read_own, create, update, update_own, delete, manage_staff, manage_devices, manage_io, configure_defaults, export, audit}
- **Configuration (8):** config.{read, manage_axle, manage_permits, manage_fees, manage_acts, manage_taxonomy, manage_references, audit}
- **Analytics (8):** report.{read, read_own, export, schedule, custom_query, manage_dashboards, superset, audit}
- **System (6):** system.{admin, audit_logs, cache_management, integration_management, backup_restore, security_policy}

**Relationships:**
- One-to-many with `role_permissions`

#### role_permissions
Junction table linking roles to permissions (supports fine-grained RBAC).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| role_id | UUID | FK → roles(id), PRIMARY KEY | Role ID |
| permission_id | UUID | FK → permissions(id), PRIMARY KEY | Permission ID |
| assigned_at | TIMESTAMPTZ | DEFAULT NOW() | Assignment timestamp |

**Indexes:**
- `idx_role_permissions_permission` ON role_permissions(permission_id)
- `idx_role_permissions_role_permission` ON role_permissions(role_id, permission_id) UNIQUE

**Relationships:**
- Many-to-many between `roles` and `permissions`

**Role-Permission Mappings (Seeded):**
- **SuperAdmin:** All 77 permissions
- **Admin:** 65 permissions (exclude system.*)
- **StationManager:** 45 permissions (station, weighing, case, prosecution [limited], user [limited], report)
- **Prosecutor:** 30 permissions (prosecution, case, user [limited], report)
- **ScaleOperator:** 12 permissions (weighing: create, read_own, scale_test, audit)
- **Inspector:** 18 permissions (weighing, case [limited], report [read_own])

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

---

### Vehicle & Driver Management Module

#### drivers
Driver master data with NTAC tracking.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Driver ID |
| id_no_or_passport | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | ID/Passport number |
| full_name | VARCHAR(255) | NOT NULL | Full name |
| license_no | VARCHAR(50) | INDEX | Driver's license number |
| license_expiry_date | DATE | | License expiry date |
| phone | VARCHAR(50) | | Contact phone |
| email | VARCHAR(255) | | Email address |
| nationality | VARCHAR(100) | | Nationality |
| age | INTEGER | | Age |
| address | TEXT | | Physical address |
| ntac_no | VARCHAR(50) | INDEX | Court case tracking number |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation time |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update time |

**Indexes:**
- `idx_drivers_id_no` ON drivers(id_no_or_passport)
- `idx_drivers_license` ON drivers(license_no) WHERE license_no IS NOT NULL
- `idx_drivers_ntac` ON drivers(ntac_no) WHERE ntac_no IS NOT NULL

**Relationships:**
- One-to-many with `weighings`
- One-to-many with `case_registers`

#### vehicles
Vehicle master data with semantic search support.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Vehicle ID |
| reg_no | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Registration number |
| make | VARCHAR(100) | | Vehicle make |
| model | VARCHAR(100) | | Vehicle model |
| vehicle_type | VARCHAR(50) | | Type: truck, trailer, bus, etc. |
| color | VARCHAR(50) | | Vehicle color |
| year_of_manufacture | INTEGER | | Manufacturing year |
| chassis_no | VARCHAR(100) | | Chassis number |
| engine_no | VARCHAR(100) | | Engine number |
| owner_id | UUID | FK → vehicle_owners(id), INDEX | Vehicle owner |
| transporter_id | UUID | FK → transporters(id), INDEX | Operating transporter |
| axle_configuration_id | UUID | FK → axle_configurations(id), INDEX | Axle configuration |
| description | TEXT | | Additional description |
| is_flagged | BOOLEAN | DEFAULT FALSE | Flagged for violations |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update |

**Indexes:**
- `idx_vehicles_reg_no` ON vehicles(reg_no)
- `idx_vehicles_owner` ON vehicles(owner_id) WHERE owner_id IS NOT NULL
- `idx_vehicles_transporter` ON vehicles(transporter_id) WHERE transporter_id IS NOT NULL
- `idx_vehicles_flagged` ON vehicles(is_flagged) WHERE is_flagged = TRUE

**Vector Columns:**
- `description_embedding` VECTOR(384) - Vector embedding for vehicle descriptions

**Vector Indexes:**
- `idx_vehicles_description_embedding` ON vehicles USING hnsw (description_embedding vector_cosine_ops)

**Relationships:**
- Many-to-one with `vehicle_owners`
- Many-to-one with `transporters`
- Many-to-one with `axle_configurations`
- One-to-many with `weighings`
- One-to-many with `permits`
- One-to-many with `case_registers`
- One-to-many with `prohibition_orders`

#### vehicle_owners
Vehicle owner master data with NTAC tracking.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Owner ID |
| id_no_or_passport | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | ID/Passport number |
| full_name | VARCHAR(255) | NOT NULL | Full name |
| phone | VARCHAR(50) | | Contact phone |
| email | VARCHAR(255) | | Email address |
| address | TEXT | | Physical address |
| ntac_no | VARCHAR(50) | INDEX | NTAC tracking number |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update |

**Indexes:**
- `idx_vehicle_owners_id_no` ON vehicle_owners(id_no_or_passport)
- `idx_vehicle_owners_ntac` ON vehicle_owners(ntac_no) WHERE ntac_no IS NOT NULL

**Relationships:**
- One-to-many with `vehicles`

#### transporters
Transporter/logistics company master data with NTAC tracking.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Transporter ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Transporter code |
| name | VARCHAR(255) | NOT NULL | Company name |
| registration_no | VARCHAR(100) | | Business registration |
| phone | VARCHAR(50) | | Contact phone |
| email | VARCHAR(255) | | Email address |
| address | TEXT | | Physical address |
| ntac_no | VARCHAR(50) | INDEX | NTAC tracking for company |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update |

**Indexes:**
- `idx_transporters_code` ON transporters(code)
- `idx_transporters_ntac` ON transporters(ntac_no) WHERE ntac_no IS NOT NULL
- `idx_transporters_active` ON transporters(is_active) WHERE is_active = TRUE

**Relationships:**
- One-to-many with `vehicles`

#### tyre_types
Master reference for tyre configurations (S, D, W).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Tyre type ID |
| code | VARCHAR(1) | UNIQUE, NOT NULL, INDEX | S (Single), D (Dual), W (Wide) |
| name | VARCHAR(50) | NOT NULL | Display name |
| description | TEXT | | Detailed description |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_tyre_types_code` ON tyre_types(code) UNIQUE
- `idx_tyre_types_active` ON tyre_types(is_active) WHERE is_active = TRUE

**Seed Data:**
- (S, Single Tyre, "Single tyre per axle end")
- (D, Dual Tyre, "Dual/twin tyres per axle end")
- (W, Wide Single, "Wide single super tyre")

#### axle_groups
Master reference for axle group classifications with spacing rules.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Axle group ID |
| code | VARCHAR(20) | UNIQUE, NOT NULL, INDEX | S1, SA4, SA6, TAG8, TAG8B, TAG12, QAG16, WWW, SSS, S4 |
| name | VARCHAR(100) | NOT NULL | Full name |
| description | TEXT | | Detailed description |
| typical_weight_kg | INTEGER | NOT NULL | Typical permissible weight |
| min_spacing_ft | DECIMAL(4,1) | | Minimum axle spacing (feet) |
| max_spacing_ft | DECIMAL(4,1) | | Maximum axle spacing (feet) |
| axle_count_in_group | INTEGER | DEFAULT 1 | Number of axles |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_axle_groups_code` ON axle_groups(code) UNIQUE
- `idx_axle_groups_active` ON axle_groups(is_active) WHERE is_active = TRUE

**Seed Data:** S1(8000kg), SA4(10000kg), SA6(6000kg), TAG8(9000kg), TAG8B(7000kg), TAG12(8000kg), QAG16(8000kg), WWW(7500kg), SSS(6000kg), S4(6000kg)

#### axle_configurations
**UNIFIED TABLE**: Master configuration for both standard (EAC-defined) and derived (custom) axle patterns.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Configuration ID |
| axle_code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Standard: "2*", "3A", "4B" / Derived: "5*S\|DD\|DD\|" |
| axle_name | VARCHAR(255) | NOT NULL | Display name |
| axle_number | INTEGER | NOT NULL | Total number of axles |
| gvw_permissible_kg | INTEGER | DEFAULT 0 | GVW limit (0 if not specified) |
| is_standard | BOOLEAN | DEFAULT FALSE | TRUE for EAC standard, FALSE for user-derived |
| legal_framework | VARCHAR(20) | DEFAULT 'BOTH' | EAC, TRAFFIC_ACT, or BOTH |
| visual_diagram_url | TEXT | | URL to axle diagram |
| notes | TEXT | | Additional notes or rules |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Last update |
| created_by_user_id | UUID | FK → users(id), NULL | Creator (NULL for standard configs) |

**Indexes:**
- `idx_axle_configurations_code` ON axle_configurations(axle_code) UNIQUE
- `idx_axle_configurations_standard` ON axle_configurations(is_standard) WHERE is_standard = TRUE
- `idx_axle_configurations_axles` ON axle_configurations(axle_number)
- `idx_axle_configurations_framework` ON axle_configurations(legal_framework)
- `idx_axle_configurations_active` ON axle_configurations(is_active) WHERE is_active = TRUE

**Business Rules:**
- Standard configs (is_standard=TRUE): Cannot be modified, created_by_user_id must be NULL
- Derived configs (is_standard=FALSE): User-created custom patterns, created_by_user_id tracks creator
- Standard codes: Simple pattern "2*", "3A", "4B", "5C", "6D", "7A", etc
- Derived codes: Pipe notation "5*S|DD|DD|", "3*S|DW||", etc encoding tyre types per position
- All configs use flat table - no separate derived_axle_configurations table needed

**Relationships:**
- One-to-many with `axle_weight_references`
- One-to-many with `weighing_axles`
- Many-to-one with `users` (for derived configs)

#### axle_weight_references
Individual axle weight specifications within each configuration (standard or derived).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Reference ID |
| axle_configuration_id | UUID | FK → axle_configurations(id), NOT NULL, INDEX | Parent configuration |
| axle_position | INTEGER | NOT NULL | Axle position (1, 2, 3...) |
| axle_legal_weight_kg | INTEGER | NOT NULL | Permissible weight for this axle |
| axle_group_id | UUID | FK → axle_groups(id), NOT NULL | Axle group type (S1, TAG8, etc) |
| axle_grouping | VARCHAR(10) | NOT NULL | Deck grouping: A, B, C, or D |
| tyre_type_id | UUID | FK → tyre_types(id), NULL | Tyre type (S, D, W) |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_axle_weight_ref_config` ON axle_weight_references(axle_configuration_id)
- `idx_axle_weight_ref_config_position` UNIQUE ON axle_weight_references(axle_configuration_id, axle_position)
- `idx_axle_weight_ref_group` ON axle_weight_references(axle_group_id)

**Relationships:**
- Many-to-one with `axle_configurations`
- Many-to-one with `axle_groups`
- Many-to-one with `tyre_types`

#### axle_fee_schedules
Fee calculation tiers for overload penalties per legal framework.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Fee schedule ID |
| legal_framework | VARCHAR(20) | NOT NULL, INDEX | EAC or TRAFFIC_ACT |
| fee_type | VARCHAR(20) | NOT NULL | GVW or AXLE |
| overload_min_kg | INTEGER | NOT NULL | Minimum overload (kg) |
| overload_max_kg | INTEGER | | Maximum overload (NULL = no limit) |
| fee_per_kg_usd | DECIMAL(10,4) | NOT NULL | Fee per kg in USD |
| flat_fee_usd | DECIMAL(10,2) | DEFAULT 0 | Flat fee component |
| demerit_points | INTEGER | DEFAULT 0 | Demerit points |
| penalty_description | TEXT | | Penalty description |
| effective_from | DATE | NOT NULL | Effective start date |
| effective_to | DATE | | Effective end date (NULL = current) |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_axle_fee_schedules_framework_type` ON axle_fee_schedules(legal_framework, fee_type)
- `idx_axle_fee_schedules_effective` ON axle_fee_schedules(effective_from, effective_to)

**Relationships:**
- Referenced by weighing fee calculations

#### permits

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Permit ID |
| permit_no | VARCHAR(100) | UNIQUE, NOT NULL, INDEX | Permit number |
| vehicle_id | UUID | FK → vehicles(id), NOT NULL, INDEX | Vehicle |
| permit_type | VARCHAR(10) | CHECK (permit_type IN ('2A', '3A')) | Permit type |
| axle_extension_kg | INTEGER | | Axle weight extension (e.g., +3000) |
| gvw_extension_kg | INTEGER | | GVW extension (e.g., +1000, +2000) |
| valid_from | DATE | NOT NULL | Validity start date |
| valid_to | DATE | NOT NULL, INDEX | Validity end date |
| issuing_authority | VARCHAR(255) | | Issuing authority |
| status | VARCHAR(20) | DEFAULT 'active', CHECK | Status: active, expired, revoked |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_permits_no` ON permits(permit_no)
- `idx_permits_vehicle` ON permits(vehicle_id, valid_to DESC)
- `idx_permits_status` ON permits(status, valid_to DESC)

**Relationships:**
- Many-to-one with `vehicles`

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
| client_local_id | UUID | UNIQUE, INDEX | Client-generated UUID for idempotency |
| sync_status | VARCHAR(20) | DEFAULT 'synced', CHECK | Sync status: queued, synced, failed |
| sync_at | TIMESTAMPTZ | | Last sync timestamp |
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
| axle_configuration_id | UUID | FK → axle_configurations(id), NOT NULL, INDEX | Configuration template |
| axle_weight_reference_id | UUID | FK → axle_weight_references(id), NULL, INDEX | Reference specification |
| axle_group_id | UUID | FK → axle_groups(id), NOT NULL | Axle group (S1, SA4, TAG8, etc) |
| axle_grouping | VARCHAR(10) | NOT NULL | Deck grouping: A, B, C, D |
| tyre_type_id | UUID | FK → tyre_types(id), NULL, INDEX | Tyre type (S, D, W) |
| fee_usd | DECIMAL(18,2) | DEFAULT 0 | Fee in USD |
| captured_at | TIMESTAMPTZ | DEFAULT NOW() | Capture timestamp |
| UNIQUE (weighing_id, axle_number) | | | |

**Indexes:**
- `idx_weighing_axles_weighing` ON weighing_axles(weighing_id, axle_number)
- `idx_weighing_axles_configuration` ON weighing_axles(axle_configuration_id)
- `idx_weighing_axles_group` ON weighing_axles(axle_group_id)

**Relationships:**
- Many-to-one with `weighings`
- Many-to-one with `axle_configurations`
- Many-to-one with `axle_weight_references`
- Many-to-one with `axle_groups`
- Many-to-one with `tyre_types`

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

### Taxonomy & Configuration Module

#### violation_types
Violation type taxonomy.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Violation type ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Violation code |
| name | VARCHAR(255) | NOT NULL | Violation name |
| description | TEXT | | Description |
| severity | VARCHAR(20) | CHECK | Severity: low, medium, high, critical |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_violation_types_code` ON violation_types(code)
- `idx_violation_types_active` ON violation_types(is_active) WHERE is_active = TRUE

#### tag_categories
Tag category taxonomy.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Category ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Category code |
| name | VARCHAR(255) | NOT NULL | Category name |
| description | TEXT | | Description |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_tag_categories_code` ON tag_categories(code)
- `idx_tag_categories_active` ON tag_categories(is_active) WHERE is_active = TRUE

#### cpc_sections
Criminal Procedure Code (CPC) sections for case closure.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Section ID |
| section_no | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Section number |
| title | VARCHAR(255) | NOT NULL | Section title |
| description | TEXT | | Full description |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_cpc_sections_no` ON cpc_sections(section_no)

#### pc_sections
Penal Code (PC) sections for case closure.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Section ID |
| section_no | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Section number |
| title | VARCHAR(255) | NOT NULL | Section title |
| description | TEXT | | Full description |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_pc_sections_no` ON pc_sections(section_no)

---

### Reference Data Module

#### stations
Weighbridge station master data with bidirectional support.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Station ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Station code |
| name | VARCHAR(255) | NOT NULL | Station name |
| station_type | VARCHAR(30) | CHECK | Type: fixed, mobile, temporary |
| location | VARCHAR(255) | | Physical location |
| road_id | UUID | FK → roads(id), INDEX | Road location |
| county_id | UUID | FK → counties(id), INDEX | County |
| latitude | DECIMAL(10, 8) | | GPS latitude |
| longitude | DECIMAL(11, 8) | | GPS longitude |
| supports_bidirectional | BOOLEAN | DEFAULT FALSE | Supports A/B bounds |
| bound_a_code | VARCHAR(20) | | Virtual station code for Bound A |
| bound_b_code | VARCHAR(20) | | Virtual station code for Bound B |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update |

**Indexes:**
- `idx_stations_code` ON stations(code)
- `idx_stations_road` ON stations(road_id) WHERE road_id IS NOT NULL
- `idx_stations_county` ON stations(county_id) WHERE county_id IS NOT NULL
- `idx_stations_active` ON stations(is_active) WHERE is_active = TRUE

**Relationships:**
- Many-to-one with `roads`
- Many-to-one with `counties`
- One-to-many with `weighings`
- One-to-many with `scale_tests`
- One-to-many with `yard_entries`
- One-to-many with `users` (assigned station)

#### roads
Road master data with district linkage.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Road ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Road code |
| name | VARCHAR(255) | NOT NULL | Road name |
| road_class | VARCHAR(30) | | Road class: A, B, C, D, E |
| district_id | UUID | FK → districts(id), INDEX | District |
| total_length_km | DECIMAL(10, 2) | | Total road length in km |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_roads_code` ON roads(code)
- `idx_roads_district` ON roads(district_id) WHERE district_id IS NOT NULL
- `idx_roads_active` ON roads(is_active) WHERE is_active = TRUE

**Relationships:**
- Many-to-one with `districts`
- One-to-many with `stations`
- One-to-many with `case_registers`

#### act_definitions
Legal act definitions (EAC Vehicle Load Control Act, Kenya Traffic Act).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Act ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Act code |
| name | VARCHAR(255) | NOT NULL | Short name |
| act_type | VARCHAR(20) | CHECK (act_type IN ('EAC', 'Traffic')) | Act type |
| full_name | TEXT | | Full legal name |
| description | TEXT | | Act description |
| effective_date | DATE | | Effective date |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_act_definitions_code` ON act_definitions(code)
- `idx_act_definitions_type` ON act_definitions(act_type)
- `idx_act_definitions_active` ON act_definitions(is_active) WHERE is_active = TRUE

**Relationships:**
- One-to-many with `weighings`
- One-to-many with `case_registers`
- One-to-many with `prosecution_cases`

#### origins_destinations
Origin and destination master data for cargo routes.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Location ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Location code |
| name | VARCHAR(255) | NOT NULL | Location name |
| location_type | VARCHAR(30) | CHECK | Type: city, town, port, border, warehouse |
| country | VARCHAR(100) | DEFAULT 'Kenya' | Country |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_origins_destinations_code` ON origins_destinations(code)
- `idx_origins_destinations_type` ON origins_destinations(location_type)
- `idx_origins_destinations_active` ON origins_destinations(is_active) WHERE is_active = TRUE

**Relationships:**
- One-to-many with `weighings` (origin_id)
- One-to-many with `weighings` (destination_id)

#### cargo_types
Cargo type taxonomy.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Cargo type ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Cargo code |
| name | VARCHAR(255) | NOT NULL | Cargo name |
| category | VARCHAR(100) | | Category: General, Hazardous, Perishable |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |

**Indexes:**
- `idx_cargo_types_code` ON cargo_types(code)
- `idx_cargo_types_active` ON cargo_types(is_active) WHERE is_active = TRUE

**Relationships:**
- One-to-many with `weighings`

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

### Prosecution Module

#### prosecution_cases
Detailed prosecution workflow tracking with charge computation.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Prosecution case ID |
| case_register_id | UUID | FK → case_registers(id), UNIQUE, NOT NULL, INDEX | Related case register |
| weighing_id | UUID | FK → weighings(id), INDEX | Related weighing |
| prosecution_officer_id | UUID | FK → users(id), NOT NULL, INDEX | Prosecuting officer |
| act_id | UUID | FK → act_definitions(id), NOT NULL | Applicable Act (EAC or Traffic) |
| gvw_overload_kg | INTEGER | | GVW overload amount |
| gvw_fee_usd | DECIMAL(18,2) | DEFAULT 0 | GVW overload fee USD |
| gvw_fee_kes | DECIMAL(18,2) | DEFAULT 0 | GVW overload fee KES |
| max_axle_overload_kg | INTEGER | | Maximum axle overload |
| max_axle_fee_usd | DECIMAL(18,2) | DEFAULT 0 | Maximum axle fee USD |
| max_axle_fee_kes | DECIMAL(18,2) | DEFAULT 0 | Maximum axle fee KES |
| best_charge_basis | VARCHAR(10) | CHECK | Basis: gvw, axle (higher charge) |
| penalty_multiplier | DECIMAL(3,1) | DEFAULT 1.0 | Penalty multiplier (1x or 5x) |
| total_fee_usd | DECIMAL(18,2) | NOT NULL | Total charge USD |
| total_fee_kes | DECIMAL(18,2) | NOT NULL | Total charge KES |
| forex_rate | DECIMAL(10,4) | NOT NULL | USD to KES conversion rate |
| certificate_no | VARCHAR(100) | UNIQUE, INDEX | Certificate number |
| case_notes | TEXT | | Prosecution notes |
| status | VARCHAR(30) | DEFAULT 'pending', CHECK, INDEX | Status: pending, invoiced, paid, court |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Record creation |
| updated_at | TIMESTAMPTZ | DEFAULT NOW() | Record update |

**Indexes:**
- `idx_prosecution_cases_case` ON prosecution_cases(case_register_id)
- `idx_prosecution_cases_weighing` ON prosecution_cases(weighing_id)
- `idx_prosecution_cases_status` ON prosecution_cases(status)
- `idx_prosecution_cases_officer` ON prosecution_cases(prosecution_officer_id)

**Vector Columns:**
- `case_notes_embedding` VECTOR(384) - Vector embedding for prosecution case notes

**Vector Indexes:**
- `idx_prosecution_cases_notes_embedding` ON prosecution_cases USING hnsw (case_notes_embedding vector_cosine_ops)

**Relationships:**
- One-to-one with `case_registers`
- Many-to-one with `weighings`
- One-to-many with `invoices`

---

### Financial Module

#### invoices
Generated invoices for violations or services.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Invoice ID |
| invoice_no | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Invoice number |
| case_register_id | UUID | FK → case_registers(id), INDEX | Related case (optional) |
| prosecution_case_id | UUID | FK → prosecution_cases(id), INDEX | Related prosecution case (optional) |
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
- `invoices` ↔ `case_registers` (many-to-one, optional)
- `invoices` ↔ `prosecution_cases` (many-to-one, optional)
- `invoices` ↔ `weighings` (many-to-one, optional)

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
- `users` ↔ `work_shifts` (many-to-many via `user_shifts`)
- `users` ↔ `organizations` (many-to-one)
- `users` ↔ `departments` (many-to-one)
- `users` ↔ `stations` (many-to-one, assigned station)
- `users` ↔ `weighings` (one-to-many, weighed_by_id)
- `users` ↔ `case_managers` (one-to-many)
- `users` ↔ `auth_service_sync_logs` (one-to-many)

**Vehicle & Driver Management:**
- `vehicles` ↔ `vehicle_owners` (many-to-one)
- `vehicles` ↔ `transporters` (many-to-one)
- `vehicles` ↔ `axle_configurations` (many-to-one)
- `vehicles` ↔ `weighings` (one-to-many)
- `vehicles` ↔ `permits` (one-to-many)
- `vehicles` ↔ `case_registers` (one-to-many)
- `vehicles` ↔ `prohibition_orders` (one-to-many)
- `drivers` ↔ `weighings` (one-to-many)
- `drivers` ↔ `case_registers` (one-to-many)

**Weighing Flow:**
- `stations` ↔ `weighings` (one-to-many)
- `stations` ↔ `scale_tests` (one-to-many)
- `stations` ↔ `yard_entries` (one-to-many)
- `stations` ↔ `roads` (many-to-one)
- `stations` ↔ `counties` (many-to-one)
- `weighings` ↔ `weighing_axles` (one-to-many)
- `weighings` ↔ `prohibition_orders` (one-to-one)
- `weighings` ↔ `yard_entries` (one-to-one)
- `weighings` ↔ `act_definitions` (many-to-one)
- `weighings` ↔ `origins_destinations` (many-to-one, origin_id)
- `weighings` ↔ `origins_destinations` (many-to-one, destination_id)
- `weighings` ↔ `cargo_types` (many-to-one)

**Case Management Flow (case_registers is central hub):**
- `weighings` → `prohibition_orders` → `case_registers` (chain for violation cases)
- `yard_entries` → `case_registers` (one-to-one)
- `case_registers` ↔ `prosecution_cases` (one-to-one)
- `case_registers` ↔ `special_releases` (one-to-many)
- `case_registers` ↔ `case_subfiles` (one-to-many, Subfiles B-J)
- `case_registers` ↔ `arrest_warrants` (one-to-many)
- `case_registers` ↔ `court_hearings` (one-to-many)
- `case_registers` ↔ `case_closure_checklists` (one-to-one)
- `case_registers` ↔ `load_correction_memos` (one-to-many)
- `case_registers` ↔ `compliance_certificates` (one-to-many)
- `case_managers` ↔ `case_registers` (one-to-many)

**Prosecution Flow:**
- `case_registers` → `prosecution_cases` (one-to-one)
- `prosecution_cases` ↔ `weighings` (many-to-one)
- `prosecution_cases` ↔ `invoices` (one-to-many)
- `invoices` ↔ `receipts` (one-to-many)

**Taxonomy Relationships:**
- `case_registers` ↔ `violation_types` (many-to-one)
- `vehicle_tags` ↔ `tag_categories` (many-to-one)
- `case_closure_checklists` ↔ `cpc_sections` (many-to-one)
- `case_closure_checklists` ↔ `pc_sections` (many-to-one)

**Geographic & Reference Data:**
- `counties` ↔ `districts` (one-to-many)
- `districts` ↔ `subcounties` (one-to-many)
- `districts` ↔ `roads` (one-to-many)
- `roads` ↔ `stations` (one-to-many)
- `roads` ↔ `case_registers` (one-to-many)
- `organizations` ↔ `departments` (one-to-many)
- `organizations` ↔ `users` (one-to-many)
- `departments` ↔ `users` (one-to-many)

**Taxonomy Relationships:**
- `case_registers` ↔ `violation_types` (many-to-one)
- `case_registers` ↔ `counties` (many-to-one, optional)
- `case_registers` ↔ `districts` (many-to-one, optional)
- `case_registers` ↔ `subcounties` (many-to-one, optional)
- `case_registers` ↔ `act_definitions` (many-to-one)
- `case_closure_checklists` ↔ `cpc_sections` (many-to-one)
- `case_closure_checklists` ↔ `pc_sections` (many-to-one)
- `vehicle_tags` ↔ `tag_categories` (many-to-one)
- `axle_configurations` ↔ `vehicles` (one-to-many)

**Court Management:**
- `courts` ↔ `court_hearings` (one-to-many)
- `courts` ↔ `case_registers` (one-to-many)

**Offline & Sync:**
- `device_sync_events` - Polymorphic references to synced entities
- `auth_service_sync_logs` ↔ `users` (many-to-one)

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

### Offline Support Module

#### device_sync_events
Queue for offline submissions and synchronization tracking.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Event ID |
| device_id | VARCHAR(100) | NOT NULL, INDEX | Device identifier |
| entity_type | VARCHAR(50) | NOT NULL, CHECK | Entity: weighing, case_register, etc. |
| entity_id | UUID | INDEX | Target entity ID (after sync) |
| correlation_id | UUID | UNIQUE, NOT NULL, INDEX | Client-generated correlation ID |
| operation | VARCHAR(20) | NOT NULL, CHECK | Operation: create, update, delete |
| payload | JSONB | NOT NULL | Full entity payload |
| sync_status | VARCHAR(20) | DEFAULT 'queued', CHECK, INDEX | Status: queued, processing, synced, failed |
| sync_attempts | INTEGER | DEFAULT 0 | Number of sync attempts |
| last_sync_attempt_at | TIMESTAMPTZ | | Last attempt timestamp |
| error_message | TEXT | | Error details if failed |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Event creation |
| synced_at | TIMESTAMPTZ | | Successful sync timestamp |

**Indexes:**
- `idx_device_sync_device_status` ON device_sync_events(device_id, sync_status)
- `idx_device_sync_correlation` ON device_sync_events(correlation_id)
- `idx_device_sync_status` ON device_sync_events(sync_status, created_at) WHERE sync_status IN ('queued', 'failed')
- `idx_device_sync_entity` ON device_sync_events(entity_type, entity_id) WHERE entity_id IS NOT NULL

**Relationships:**
- Polymorphic references to synced entities via entity_type and entity_id

#### auth_service_sync_logs
Audit trail for auth-service synchronization events.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY, DEFAULT gen_random_uuid() | Log ID |
| sync_type | VARCHAR(30) | CHECK | Type: user_sync, user_create, user_deactivate |
| auth_service_user_id | UUID | INDEX | Auth-service user ID |
| local_user_id | UUID | FK → users(id), INDEX | Local user ID |
| status | VARCHAR(20) | CHECK | Status: success, failed, partial |
| changes | JSONB | | Changes applied (JSON) |
| error_message | TEXT | | Error details if failed |
| synced_at | TIMESTAMPTZ | DEFAULT NOW(), INDEX | Sync timestamp |

**Indexes:**
- `idx_auth_sync_logs_user` ON auth_service_sync_logs(auth_service_user_id)
- `idx_auth_sync_logs_local` ON auth_service_sync_logs(local_user_id) WHERE local_user_id IS NOT NULL
- `idx_auth_sync_logs_status` ON auth_service_sync_logs(status, synced_at DESC)

**Relationships:**
- Many-to-one with `users`

---

## Sync & Offline Support

**Tables Supporting Offline Sync:**
- `weighings` - Includes `client_local_id` (UUID) and `sync_status`
- `device_sync_events` - Queue for offline submissions
- `auth_service_sync_logs` - Audit trail for auth-service sync

**Sync Metadata:**
- `client_local_id` - Client-generated UUID for idempotency
- `sync_status` - Status: queued, synced, failed
- `sync_at` - Last sync timestamp

**Idempotency:**
- Client-generated UUIDs prevent duplicate submissions
- Backend validates `client_local_id` uniqueness
- Duplicate detection via correlation_id in `device_sync_events`