# TruLoad Backend - Comprehensive Implementation Plan

## 1. Executive Summary

**System Purpose:** Cloud-hosted intelligent weighing and enforcement solution enabling roadside officers to capture vehicle weights, verify compliance with EAC Vehicle Load Control Act (2016) or Kenya Traffic Act (Cap 403), and manage enforcement actions (prosecution, redistribution, special release).

**Key Capabilities:**
- Multi-mode weighing: Static (multi-deck), WIM (Weigh-In-Motion), Mobile/Axle weighing
- Real-time weight acquisition via TruConnect microservice
- Offline-aware client systems with automatic cloud sync
- Legal compliance enforcement with automated charge computation
- Integration with eCitizen/Road Authority payment gateways
- Court case tracking (NTAC, OB numbers)
- Comprehensive audit trails and reporting

---

## 2. Technology Stack

### Core Framework
- **Language:** C# 12
- **Framework:** .NET 8 (LTS) Web API
- **Architecture Pattern:** Modular Monolith with CQRS (MediatR), Vertical Slice Architecture

### Data & Caching
- **Primary Database:** PostgreSQL 16+ (Npgsql, Entity Framework Core 8)
- **Caching:** Redis 7+ (StackExchange.Redis)
- **Message Broker:** RabbitMQ 3.13+ (MassTransit for .NET)
- **Search (Optional):** Elasticsearch for advanced report queries

### Supporting Libraries
- **Authentication:** ASP.NET Core Identity + JWT (System.IdentityModel.Tokens.Jwt)
- **Validation:** FluentValidation
- **Resilience:** Polly (circuit breaker, retry, timeout policies)
- **API Documentation:** Swashbuckle (Swagger/OpenAPI 3.0)
- **Logging:** Serilog (structured logging to Seq/ELK)
- **Mapping:** AutoMapper or Mapster
- **Testing:** xUnit, FluentAssertions, Moq, Testcontainers
- **Background Jobs:** Hangfire (dashboard for monitoring)

### DevOps & Observability
- **Containerization:** Docker multi-stage builds
- **Orchestration:** Kubernetes (via centralized devops-k8s)
- **CI/CD:** GitHub Actions → ArgoCD
- **Monitoring:** Prometheus + Grafana, OpenTelemetry
- **APM:** Application Insights or Jaeger distributed tracing

---

## 3. System Architecture

### 3.1 Architectural Principles
1. **Modular Boundaries:** Each module (User, Weighing, Prosecution, etc.) is a vertical slice with its own controllers, commands/queries, validators, and domain logic
2. **Domain Events:** Cross-module communication via RabbitMQ (e.g., `WeighingCompleted`, `VehicleSentToYard`, `InvoicePaid`)
3. **API-First:** REST endpoints with versioning (`/api/v1/...`); SignalR hubs for real-time weight streaming
4. **Offline Resilience:** Idempotent operations; client-generated correlation IDs; conflict resolution strategies
5. **Performance:** Read/write separation via materialized views; Redis caching for hot data; partitioned tables
6. **Security:** RBAC with claims-based authorization; audit logs for all mutations; encrypted sensitive fields

### 3.2 Module Breakdown

| Module | Responsibility | Key Entities |
|--------|---------------|--------------|
| **User Management** | Authentication, roles, shifts, permissions | Users, Roles, Shifts, UserRoles |
| **Weighing** | Weight capture (Static/WIM/Axle), compliance evaluation, ticketing | Weighings, WeighingAxles, ScaleTests |
| **Prosecution** | Case management, charge computation, invoicing, court escalation | ProsecutionCases, ChargeBreakdowns, Invoices |
| **Yard & Tags** | Vehicle detention, prohibition orders, tag lifecycle | YardEntries, VehicleTags, ProhibitionOrders |
| **Special Release** | Tolerance-based/permit-based releases | SpecialReleases, Permits |
| **Inspection** | Dimensional compliance (wide load) | VehicleInspections |
| **Reporting** | Registers, analytics, exports | Dynamic report generation |
| **Settings** | System config, stations, cameras, I/O, prosecution defaults | Stations, Cameras, IoDevices, SystemSettings |
| **Security** | Audit logs, backups, password policies | AuditLogs, PasswordHistory |

---

## 4. Database Schema Design (PostgreSQL)

### 4.1 Naming Conventions
- **Tables:** snake_case, plural (e.g., `weighing_axles`)
- **Columns:** snake_case
- **Primary Keys:** `id` (BIGSERIAL)
- **Foreign Keys:** `{entity}_id` (e.g., `vehicle_id`)
- **Timestamps:** `created_at`, `updated_at`, `deleted_at` (soft delete)
- **Enums:** Stored as VARCHAR with CHECK constraints or PostgreSQL ENUM types

### 4.2 Core Tables

#### **4.2.1 User Management & Security**

```sql
-- Users table
CREATE TABLE users (
    id BIGSERIAL PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    phone VARCHAR(50),
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    status VARCHAR(20) DEFAULT 'active' CHECK (status IN ('active', 'inactive', 'locked')),
    last_login_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    deleted_at TIMESTAMPTZ
);
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_status ON users(status) WHERE deleted_at IS NULL;

-- Roles table
CREATE TABLE roles (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) UNIQUE NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- User-Role mapping
CREATE TABLE user_roles (
    user_id BIGINT REFERENCES users(id) ON DELETE CASCADE,
    role_id BIGINT REFERENCES roles(id) ON DELETE CASCADE,
    assigned_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (user_id, role_id)
);

-- Shifts
CREATE TABLE shifts (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    start_time TIME NOT NULL,
    end_time TIME NOT NULL,
    days_mask INTEGER DEFAULT 127, -- Bitmask: Mon=1, Tue=2, ..., Sun=64
    rotation_group VARCHAR(50),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- User-Shift assignments
CREATE TABLE user_shifts (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT REFERENCES users(id) ON DELETE CASCADE,
    shift_id BIGINT REFERENCES shifts(id) ON DELETE CASCADE,
    starts_on DATE NOT NULL,
    ends_on DATE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_user_shifts_active ON user_shifts(user_id) WHERE ends_on IS NULL OR ends_on > CURRENT_DATE;

-- Audit logs
CREATE TABLE audit_logs (
    id BIGSERIAL PRIMARY KEY,
    actor_id BIGINT REFERENCES users(id) ON DELETE SET NULL,
    action VARCHAR(100) NOT NULL,
    entity VARCHAR(100) NOT NULL,
    entity_id VARCHAR(100),
    data_before JSONB,
    data_after JSONB,
    ip_address INET,
    user_agent TEXT,
    performed_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_audit_logs_actor_time ON audit_logs(actor_id, performed_at DESC);
CREATE INDEX idx_audit_logs_entity ON audit_logs(entity, entity_id);
```

#### **4.2.2 Reference Data & Settings**

```sql
-- Stations/Weighbridges
CREATE TABLE stations (
    id BIGSERIAL PRIMARY KEY,
    code VARCHAR(20) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    route_id BIGINT REFERENCES routes(id),
    cluster_id BIGINT REFERENCES clusters(id),
    bound VARCHAR(10), -- A, B, or NULL for bidirectional
    default_camera_id BIGINT,
    domain VARCHAR(255),
    ip_address INET,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_stations_code ON stations(code);

-- Routes
CREATE TABLE routes (
    id BIGSERIAL PRIMARY KEY,
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Clusters (grouping stations)
CREATE TABLE clusters (
    id BIGSERIAL PRIMARY KEY,
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Vehicle Makes
CREATE TABLE vehicle_makes (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) UNIQUE NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Transporters
CREATE TABLE transporters (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    address TEXT,
    phone VARCHAR(50),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_transporters_name ON transporters(name);

-- Drivers
CREATE TABLE drivers (
    id BIGSERIAL PRIMARY KEY,
    id_no_or_passport VARCHAR(50) UNIQUE NOT NULL,
    license_no VARCHAR(50),
    full_names VARCHAR(255) NOT NULL,
    surname VARCHAR(100),
    gender VARCHAR(10),
    nationality VARCHAR(100),
    age INTEGER,
    address TEXT,
    ntac_no VARCHAR(50), -- Court case tracking number
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_drivers_id_no ON drivers(id_no_or_passport);
CREATE INDEX idx_drivers_ntac ON drivers(ntac_no) WHERE ntac_no IS NOT NULL;

-- Vehicles
CREATE TABLE vehicles (
    id BIGSERIAL PRIMARY KEY,
    reg_no VARCHAR(50) UNIQUE NOT NULL,
    make_id BIGINT REFERENCES vehicle_makes(id),
    trailer_no VARCHAR(50),
    transporter_id BIGINT REFERENCES transporters(id),
    axle_configuration_id BIGINT, -- FK added later
    permit_no VARCHAR(100),
    permit_issued_at DATE,
    permit_expires_at DATE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_vehicles_reg_no ON vehicles(reg_no);
CREATE INDEX idx_vehicles_transporter ON vehicles(transporter_id);

-- Origin & Destination
CREATE TABLE origins_destinations (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(255) UNIQUE NOT NULL,
    type VARCHAR(20) CHECK (type IN ('origin', 'destination', 'both')),
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Cargo types
CREATE TABLE cargo_types (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(255) UNIQUE NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Currencies (for forex rates)
CREATE TABLE currencies (
    code VARCHAR(10) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    rate_to_usd DECIMAL(18,6) NOT NULL,
    as_of TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_currencies_as_of ON currencies(code, as_of DESC);

-- System Settings (key-value store)
CREATE TABLE system_settings (
    key VARCHAR(255) PRIMARY KEY,
    value TEXT NOT NULL,
    description TEXT,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Prosecution Settings
CREATE TABLE prosecution_settings (
    id BIGSERIAL PRIMARY KEY,
    station_default_id BIGINT REFERENCES stations(id),
    court_default_id BIGINT, -- FK to courts table
    district_default VARCHAR(255),
    tolerance_gvw_kg INTEGER DEFAULT 200,
    tolerance_axle_kg INTEGER DEFAULT 200,
    act_default VARCHAR(10) DEFAULT 'EAC' CHECK (act_default IN ('EAC', 'TRAFFIC')),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
```

#### **4.2.3 Axle Configurations & Acts**

Based on the provided SQL and KURAWEIGH spec, we model axle configurations with groups (A/B/C/D) and permissible limits per group.

```sql
-- Axle Configurations
CREATE TABLE axle_configurations (
    id BIGSERIAL PRIMARY KEY,
    code VARCHAR(20) UNIQUE NOT NULL, -- e.g., '2A', '3A', '5*S|DD|DD|', '7*SS|DD|D|DD'
    name VARCHAR(100),
    axle_count INTEGER NOT NULL,
    base_gvw_kg INTEGER DEFAULT 0, -- Max GVW for this config under EAC (e.g., 56000 for 7-axle)
    image_path VARCHAR(500), -- Path to axle diagram image
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_axle_config_code ON axle_configurations(code);

-- Axle Groups (A, B, C, D deck/group classifications)
-- Groups are logical divisions of axles (e.g., front axle=A, tandem=B)
CREATE TABLE axle_groups (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(10) NOT NULL, -- A, B, C, D
    description TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Axle Configuration to Group mapping (which groups apply to which config)
CREATE TABLE axle_configuration_groups (
    id BIGSERIAL PRIMARY KEY,
    axle_configuration_id BIGINT REFERENCES axle_configurations(id) ON DELETE CASCADE,
    axle_number INTEGER NOT NULL, -- Which axle in the config (1, 2, 3, ...)
    group_id BIGINT REFERENCES axle_groups(id),
    group_name VARCHAR(10), -- Denormalized A/B/C/D for quick lookup
    permissible_kg INTEGER NOT NULL, -- Legal weight limit for this axle
    deck_grouping VARCHAR(10), -- Physical deck assignment if multi-deck
    tyre_type VARCHAR(10), -- S (single), D (dual), W (wide), etc.
    camera_wim VARCHAR(50), -- Camera ID for WIM capture
    UNIQUE (axle_configuration_id, axle_number)
);
CREATE INDEX idx_axle_config_groups_config ON axle_configuration_groups(axle_configuration_id);

-- Act Definitions
CREATE TABLE act_definitions (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(50) UNIQUE NOT NULL, -- 'EAC', 'TRAFFIC'
    full_name TEXT,
    description TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- EAC Fee Bands - GVW Overload (from EAC Enforcement Regulations 2018)
CREATE TABLE eac_fee_bands_gvw (
    id BIGSERIAL PRIMARY KEY,
    overload_kg_from INTEGER NOT NULL,
    overload_kg_to INTEGER NOT NULL,
    fee_usd DECIMAL(18,2) NOT NULL,
    schedule_version VARCHAR(50), -- e.g., 'EAC 2018 Gazette'
    effective_from DATE,
    effective_to DATE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT chk_overload_range CHECK (overload_kg_to >= overload_kg_from)
);
CREATE INDEX idx_eac_gvw_bands ON eac_fee_bands_gvw(overload_kg_from, overload_kg_to);

-- EAC Fee Bands - Axle Overload (per axle type)
CREATE TABLE eac_fee_bands_axle (
    id BIGSERIAL PRIMARY KEY,
    axle_type VARCHAR(50) NOT NULL, -- 'single_steering', 'single_non_steering', 'tandem', 'tridem'
    overload_kg_from INTEGER NOT NULL,
    overload_kg_to INTEGER NOT NULL,
    fee_usd DECIMAL(18,2) NOT NULL,
    schedule_version VARCHAR(50),
    effective_from DATE,
    effective_to DATE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_eac_axle_bands ON eac_fee_bands_axle(axle_type, overload_kg_from, overload_kg_to);

-- Traffic Act Fee Bands - GVW Only (KeNHA Schedule 2021-2030)
CREATE TABLE traffic_fee_bands_gvw (
    id BIGSERIAL PRIMARY KEY,
    overload_kg_from INTEGER NOT NULL,
    overload_kg_to INTEGER NOT NULL,
    fee_usd DECIMAL(18,2) NOT NULL,
    fee_ksh DECIMAL(18,2), -- Reference KSh amount for audit
    schedule_version VARCHAR(50), -- e.g., 'KeNHA 2021-2030'
    effective_from DATE,
    effective_to DATE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_traffic_gvw_bands ON traffic_fee_bands_gvw(overload_kg_from, overload_kg_to);

-- Permit Rules (extended limits for special permits)
CREATE TABLE permit_rules (
    id BIGSERIAL PRIMARY KEY,
    vehicle_class_code VARCHAR(20) NOT NULL, -- e.g., '2A', '3A'
    extra_axle_kg INTEGER DEFAULT 0, -- Additional kg allowed per axle
    extra_gvw_kg INTEGER DEFAULT 0, -- Additional kg allowed for GVW
    description TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Tolerance Policies (operational tolerances beyond statutory)
CREATE TABLE tolerance_policies (
    id BIGSERIAL PRIMARY KEY,
    act_id BIGINT REFERENCES act_definitions(id),
    type VARCHAR(20) CHECK (type IN ('axle', 'gvw')),
    value_kg INTEGER,
    percent_value DECIMAL(5,2), -- e.g., 5.00 for 5%
    enabled BOOLEAN DEFAULT TRUE,
    applies_to VARCHAR(20) DEFAULT 'all' CHECK (applies_to IN ('permit', 'non_permit', 'all')),
    authority_reference TEXT,
    effective_from DATE,
    effective_to DATE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
```

**Sample Seed Data for Axle Configurations (from provided SQL):**

```sql
-- Sample: 2A (2-axle vehicle)
INSERT INTO axle_configurations (code, name, axle_count, base_gvw_kg) VALUES ('2A', '2A', 2, 18000);
INSERT INTO axle_configuration_groups (axle_configuration_id, axle_number, group_name, permissible_kg, tyre_type) VALUES
  ((SELECT id FROM axle_configurations WHERE code='2A'), 1, 'A', 8000, 'S'),
  ((SELECT id FROM axle_configurations WHERE code='2A'), 2, 'B', 10000, 'D');

-- Sample: 3A (3-axle vehicle)
INSERT INTO axle_configurations (code, name, axle_count, base_gvw_kg) VALUES ('3A', '3A', 3, 28000);
INSERT INTO axle_configuration_groups (axle_configuration_id, axle_number, group_name, permissible_kg, tyre_type) VALUES
  ((SELECT id FROM axle_configurations WHERE code='3A'), 1, 'A', 8000, 'S'),
  ((SELECT id FROM axle_configurations WHERE code='3A'), 2, 'B', 9000, 'D'),
  ((SELECT id FROM axle_configurations WHERE code='3A'), 3, 'B', 9000, 'D');

-- Sample: 7*SS|DD|D|DD (7-axle with steering + groups)
INSERT INTO axle_configurations (code, name, axle_count, base_gvw_kg) VALUES ('7*SS|DD|D|DD', '7*SS|DD|D|DD', 7, 56000);
INSERT INTO axle_configuration_groups (axle_configuration_id, axle_number, group_name, permissible_kg, deck_grouping, tyre_type) VALUES
  ((SELECT id FROM axle_configurations WHERE code='7*SS|DD|D|DD'), 1, 'A', 6000, 'A', 'S'),
  ((SELECT id FROM axle_configurations WHERE code='7*SS|DD|D|DD'), 2, 'A', 6000, 'A', 'S'),
  ((SELECT id FROM axle_configurations WHERE code='7*SS|DD|D|DD'), 3, 'B', 9000, 'B', 'D'),
  ((SELECT id FROM axle_configurations WHERE code='7*SS|DD|D|DD'), 4, 'B', 9000, 'B', 'D'),
  ((SELECT id FROM axle_configurations WHERE code='7*SS|DD|D|DD'), 5, 'C', 10000, 'C', 'D'),
  ((SELECT id FROM axle_configurations WHERE code='7*SS|DD|D|DD'), 6, 'D', 9000, 'D', 'D'),
  ((SELECT id FROM axle_configurations WHERE code='7*SS|DD|D|DD'), 7, 'D', 9000, 'D', 'D');
```

**EAC Fee Bands (from Appendix A):**

```sql
-- EAC GVW overload fees (USD)
INSERT INTO eac_fee_bands_gvw (overload_kg_from, overload_kg_to, fee_usd, schedule_version, effective_from) VALUES
  (1, 500, 90.95, 'EAC 2018', '2018-01-01'),
  (501, 1000, 186.00, 'EAC 2018', '2018-01-01'),
  (1001, 1500, 289.35, 'EAC 2018', '2018-01-01'),
  (1501, 2000, 392.70, 'EAC 2018', '2018-01-01'),
  (2001, 2500, 504.30, 'EAC 2018', '2018-01-01'),
  (2501, 3000, 620.05, 'EAC 2018', '2018-01-01'),
  (3001, 3500, 744.10, 'EAC 2018', '2018-01-01'),
  (3501, 4000, 872.20, 'EAC 2018', '2018-01-01'),
  (4001, 4500, 1008.65, 'EAC 2018', '2018-01-01'),
  (4501, 5000, 1153.30, 'EAC 2018', '2018-01-01');
-- (Continue for higher bands...)

-- Traffic Act GVW fees (KeNHA 2021-2030, converted to USD)
INSERT INTO traffic_fee_bands_gvw (overload_kg_from, overload_kg_to, fee_usd, fee_ksh, schedule_version, effective_from) VALUES
  (1, 500, 235.90, 25786, 'KeNHA 2021-2030', '2021-01-01'),
  (501, 1000, 482.50, 52742, 'KeNHA 2021-2030', '2021-01-01'),
  (1001, 1500, 750.55, 82043, 'KeNHA 2021-2030', '2021-01-01');
-- (Continue based on published schedules...)

-- Permit rules (2A, 3A examples)
INSERT INTO permit_rules (vehicle_class_code, extra_axle_kg, extra_gvw_kg, description) VALUES
  ('2A', 3000, 1000, '2A vehicles with permit: +3000 kg axle, +1000 kg GVW'),
  ('3A', 3000, 2000, '3A vehicles with permit: +3000 kg axle, +2000 kg GVW');
```

#### **4.2.4 Weighing Module (Core)**

```sql
-- Weighings (main transaction table)
CREATE TABLE weighings (
    id BIGSERIAL PRIMARY KEY,
    ticket_no VARCHAR(50) UNIQUE NOT NULL,
    station_id BIGINT REFERENCES stations(id),
    vehicle_id BIGINT REFERENCES vehicles(id),
    driver_id BIGINT REFERENCES drivers(id),
    weighing_type VARCHAR(20) NOT NULL CHECK (weighing_type IN ('static', 'wim', 'axle')),
    act_id BIGINT REFERENCES act_definitions(id),
    bound VARCHAR(10), -- A or B
    
    -- Weight data
    gvw_measured_kg INTEGER NOT NULL,
    gvw_permissible_kg INTEGER NOT NULL,
    gvw_overload_kg INTEGER GENERATED ALWAYS AS (gvw_measured_kg - gvw_permissible_kg) STORED,
    
    -- Additional context
    origin_id BIGINT REFERENCES origins_destinations(id),
    destination_id BIGINT REFERENCES origins_destinations(id),
    cargo_id BIGINT REFERENCES cargo_types(id),
    
    -- Compliance flags
    tolerance_applied BOOLEAN DEFAULT FALSE,
    has_permit BOOLEAN DEFAULT FALSE,
    is_compliant BOOLEAN DEFAULT TRUE,
    is_sent_to_yard BOOLEAN DEFAULT FALSE,
    
    -- Reweigh tracking
    reweigh_cycle_no INTEGER DEFAULT 1,
    reweigh_limit INTEGER DEFAULT 8,
    original_weighing_id BIGINT REFERENCES weighings(id), -- Links to first weigh if reweigh
    
    -- Metadata
    weighed_at TIMESTAMPTZ DEFAULT NOW(),
    weighed_by_id BIGINT REFERENCES users(id),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
) PARTITION BY RANGE (weighed_at);

-- Create monthly partitions (example for 2025)
CREATE TABLE weighings_2025_01 PARTITION OF weighings FOR VALUES FROM ('2025-01-01') TO ('2025-02-01');
CREATE TABLE weighings_2025_02 PARTITION OF weighings FOR VALUES FROM ('2025-02-01') TO ('2025-03-01');
-- (Auto-create partitions via script or pg_partman extension)

CREATE INDEX idx_weighings_vehicle ON weighings(vehicle_id, weighed_at DESC);
CREATE INDEX idx_weighings_station ON weighings(station_id, weighed_at DESC);
CREATE INDEX idx_weighings_ticket ON weighings(ticket_no);
CREATE INDEX idx_weighings_yard ON weighings(is_sent_to_yard) WHERE is_sent_to_yard = TRUE;

-- Weighing Axles (individual axle weights)
CREATE TABLE weighing_axles (
    id BIGSERIAL PRIMARY KEY,
    weighing_id BIGINT NOT NULL, -- FK to weighings partition
    axle_number INTEGER NOT NULL,
    measured_kg INTEGER NOT NULL,
    permissible_kg INTEGER NOT NULL,
    overload_kg INTEGER GENERATED ALWAYS AS (measured_kg - permissible_kg) STORED,
    group_name VARCHAR(10), -- A, B, C, D
    group_grouping VARCHAR(10), -- Deck grouping reference
    tyre_type VARCHAR(10),
    fee_usd DECIMAL(18,2) DEFAULT 0,
    captured_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (weighing_id, axle_number)
);
CREATE INDEX idx_weighing_axles_weighing ON weighing_axles(weighing_id, axle_number);

-- Weight Stream Events (raw TruConnect data for audit)
CREATE TABLE weight_stream_events (
    id BIGSERIAL PRIMARY KEY,
    weighing_id BIGINT,
    scale_id VARCHAR(50),
    raw_json JSONB NOT NULL,
    recorded_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_weight_stream_weighing ON weight_stream_events(weighing_id);

-- Scale Tests (daily calibration checks)
CREATE TABLE scale_tests (
    id BIGSERIAL PRIMARY KEY,
    station_id BIGINT REFERENCES stations(id),
    test_weight_kg INTEGER,
    result VARCHAR(20) CHECK (result IN ('pass', 'fail')),
    deviation_kg INTEGER,
    details TEXT,
    carried_at TIMESTAMPTZ DEFAULT NOW(),
    carried_by_id BIGINT REFERENCES users(id)
);
CREATE INDEX idx_scale_tests_station_date ON scale_tests(station_id, carried_at DESC);
```

#### **4.2.5 Yard, Tags & Prohibition**

```sql
-- Vehicle Tags (automatic or manual tagging for transgression tracking)
CREATE TABLE vehicle_tags (
    id BIGSERIAL PRIMARY KEY,
    reg_no VARCHAR(50) NOT NULL,
    tag_type VARCHAR(20) CHECK (tag_type IN ('automatic', 'manual')),
    reason TEXT NOT NULL,
    station_code VARCHAR(20),
    status VARCHAR(20) DEFAULT 'open' CHECK (status IN ('open', 'closed')),
    category VARCHAR(50),
    tag_photo_path VARCHAR(500),
    effective_time_period INTERVAL, -- Duration tag is active
    created_by_id BIGINT REFERENCES users(id),
    closed_by_id BIGINT REFERENCES users(id),
    closed_reason TEXT,
    opened_at TIMESTAMPTZ DEFAULT NOW(),
    closed_at TIMESTAMPTZ,
    exported BOOLEAN DEFAULT FALSE
);
CREATE INDEX idx_vehicle_tags_reg_status ON vehicle_tags(reg_no, status);
CREATE INDEX idx_vehicle_tags_station ON vehicle_tags(station_code, opened_at DESC);

-- Yard Entries (vehicles sent to holding yard)
CREATE TABLE yard_entries (
    id BIGSERIAL PRIMARY KEY,
    weighing_id BIGINT REFERENCES weighings(id) UNIQUE,
    station_id BIGINT REFERENCES stations(id),
    reason VARCHAR(50) CHECK (reason IN ('redistribution', 'gvw_overload', 'permit_check', 'offload')),
    status VARCHAR(20) DEFAULT 'pending' CHECK (status IN ('pending', 'processing', 'released', 'escalated')),
    entered_at TIMESTAMPTZ DEFAULT NOW(),
    released_at TIMESTAMPTZ
);
CREATE INDEX idx_yard_entries_weighing ON yard_entries(weighing_id);
CREATE INDEX idx_yard_entries_status ON yard_entries(status, entered_at DESC);

-- Prohibition Orders
CREATE TABLE prohibition_orders (
    id BIGSERIAL PRIMARY KEY,
    yard_entry_id BIGINT REFERENCES yard_entries(id) UNIQUE,
    doc_no VARCHAR(100) UNIQUE NOT NULL,
    inspector_name VARCHAR(255),
    yard_location VARCHAR(255),
    repairs_needed TEXT,
    offload_truck_id BIGINT REFERENCES vehicles(id), -- Relief truck for offloading
    issued_at TIMESTAMPTZ DEFAULT NOW(),
    issued_by_id BIGINT REFERENCES users(id)
);
CREATE INDEX idx_prohibition_orders_yard ON prohibition_orders(yard_entry_id);
```

#### **4.2.6 Prosecution Module**

```sql
-- Courts
CREATE TABLE courts (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(255) UNIQUE NOT NULL,
    county VARCHAR(100),
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Prosecution Cases
CREATE TABLE prosecution_cases (
    id BIGSERIAL PRIMARY KEY,
    yard_entry_id BIGINT REFERENCES yard_entries(id) UNIQUE,
    act_id BIGINT REFERENCES act_definitions(id),
    case_no VARCHAR(100) UNIQUE NOT NULL,
    ntac_no VARCHAR(50), -- National Traffic Case Number
    ob_no VARCHAR(50), -- Occurrence Book Number
    
    -- Location details
    road_used VARCHAR(255),
    district VARCHAR(100),
    county VARCHAR(100),
    
    -- Court details
    court_id BIGINT REFERENCES courts(id),
    complainant_officer_id BIGINT REFERENCES users(id),
    investigating_officer_id BIGINT REFERENCES users(id),
    
    -- Status
    status VARCHAR(50) DEFAULT 'open' CHECK (status IN ('open', 'charged', 'paid', 'escalated', 'closed')),
    
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_prosecution_cases_yard ON prosecution_cases(yard_entry_id);
CREATE INDEX idx_prosecution_cases_status ON prosecution_cases(status, created_at DESC);
CREATE INDEX idx_prosecution_cases_ntac ON prosecution_cases(ntac_no) WHERE ntac_no IS NOT NULL;

-- Prosecution Parties (driver, owner, transporter details)
CREATE TABLE prosecution_parties (
    id BIGSERIAL PRIMARY KEY,
    case_id BIGINT REFERENCES prosecution_cases(id) ON DELETE CASCADE,
    party_type VARCHAR(20) CHECK (party_type IN ('driver', 'owner', 'transporter')),
    
    -- Identity
    id_no_or_passport VARCHAR(50),
    full_names VARCHAR(255),
    surname VARCHAR(100),
    gender VARCHAR(10),
    nationality VARCHAR(100),
    age INTEGER,
    address TEXT,
    phone VARCHAR(50),
    ntac_no VARCHAR(50),
    
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_prosecution_parties_case ON prosecution_parties(case_id, party_type);

-- Charge Breakdowns (detailed fee computation)
CREATE TABLE charge_breakdowns (
    id BIGSERIAL PRIMARY KEY,
    case_id BIGINT REFERENCES prosecution_cases(id) ON DELETE CASCADE,
    basis VARCHAR(20) CHECK (basis IN ('axle', 'gvw', 'permit')),
    overload_kg INTEGER NOT NULL,
    fee_usd DECIMAL(18,2) NOT NULL,
    fee_kes DECIMAL(18,2) NOT NULL,
    forex_rate DECIMAL(18,6) NOT NULL, -- Snapshot of daily rate
    rule_reference TEXT, -- e.g., 'EAC 2018 Band 501-1000'
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_charge_breakdowns_case ON charge_breakdowns(case_id);

-- Invoices
CREATE TABLE invoices (
    id BIGSERIAL PRIMARY KEY,
    case_id BIGINT REFERENCES prosecution_cases(id),
    invoice_no VARCHAR(100) UNIQUE NOT NULL,
    total_usd DECIMAL(18,2) NOT NULL,
    total_kes DECIMAL(18,2) NOT NULL,
    issued_at TIMESTAMPTZ DEFAULT NOW(),
    external_reference VARCHAR(255), -- eCitizen/Road Authority ref
    status VARCHAR(20) DEFAULT 'pending' CHECK (status IN ('pending', 'paid', 'cancelled'))
);
CREATE INDEX idx_invoices_case ON invoices(case_id);
CREATE INDEX idx_invoices_status ON invoices(status, issued_at DESC);

-- Receipts
CREATE TABLE receipts (
    id BIGSERIAL PRIMARY KEY,
    case_id BIGINT REFERENCES prosecution_cases(id),
    invoice_id BIGINT REFERENCES invoices(id),
    receipt_no VARCHAR(100) UNIQUE NOT NULL,
    amount_usd DECIMAL(18,2) NOT NULL,
    amount_kes DECIMAL(18,2) NOT NULL,
    paid_at TIMESTAMPTZ DEFAULT NOW(),
    external_reference VARCHAR(255),
    payment_method VARCHAR(50)
);
CREATE INDEX idx_receipts_case ON receipts(case_id);

-- Load Correction Memos
CREATE TABLE load_corrections (
    id BIGSERIAL PRIMARY KEY,
    case_id BIGINT REFERENCES prosecution_cases(id),
    memo_type VARCHAR(50) CHECK (memo_type IN ('conditional', 'final')),
    memo_text TEXT,
    amount_paid_usd DECIMAL(18,2),
    receipt_no VARCHAR(100),
    relief_truck_reg_no VARCHAR(50), -- Linked offload truck
    created_at TIMESTAMPTZ DEFAULT NOW(),
    created_by_id BIGINT REFERENCES users(id)
);
CREATE INDEX idx_load_corrections_case ON load_corrections(case_id);

-- Compliance Certificates
CREATE TABLE compliance_certificates (
    id BIGSERIAL PRIMARY KEY,
    case_id BIGINT REFERENCES prosecution_cases(id) UNIQUE,
    certificate_no VARCHAR(100) UNIQUE NOT NULL,
    issued_at TIMESTAMPTZ DEFAULT NOW(),
    issued_by_id BIGINT REFERENCES users(id)
);

-- Materialized View: Charge Summaries (for quick dashboard queries)
CREATE MATERIALIZED VIEW charge_summaries AS
SELECT 
    c.id AS case_id,
    c.case_no,
    CASE 
        WHEN MAX(cb_gvw.fee_usd) > MAX(cb_axle.fee_usd) THEN 'gvw'
        ELSE 'axle'
    END AS best_basis,
    GREATEST(MAX(cb_gvw.fee_usd), MAX(cb_axle.fee_usd)) AS fee_usd,
    GREATEST(MAX(cb_gvw.fee_kes), MAX(cb_axle.fee_kes)) AS fee_kes
FROM prosecution_cases c
LEFT JOIN charge_breakdowns cb_gvw ON c.id = cb_gvw.case_id AND cb_gvw.basis = 'gvw'
LEFT JOIN charge_breakdowns cb_axle ON c.id = cb_axle.case_id AND cb_axle.basis = 'axle'
GROUP BY c.id, c.case_no;

CREATE UNIQUE INDEX idx_charge_summaries_case ON charge_summaries(case_id);
-- Refresh strategy: REFRESH MATERIALIZED VIEW CONCURRENTLY charge_summaries; (on invoice/receipt mutations)
```

#### **4.2.7 Special Release & Permits**

```sql
-- Permits (vehicle-specific special load permits)
CREATE TABLE permits (
    id BIGSERIAL PRIMARY KEY,
    vehicle_id BIGINT REFERENCES vehicles(id),
    permit_no VARCHAR(100) UNIQUE NOT NULL,
    permit_class VARCHAR(50), -- e.g., 'wide_load', 'excess_weight'
    extra_axle_kg INTEGER DEFAULT 0,
    extra_gvw_kg INTEGER DEFAULT 0,
    valid_from DATE NOT NULL,
    valid_to DATE NOT NULL,
    issuer VARCHAR(255),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_permits_vehicle ON permits(vehicle_id, valid_to DESC);
CREATE INDEX idx_permits_active ON permits(is_active, valid_to) WHERE is_active = TRUE;

-- Special Releases
CREATE TABLE special_releases (
    id BIGSERIAL PRIMARY KEY,
    weighing_id BIGINT REFERENCES weighings(id),
    case_id BIGINT REFERENCES prosecution_cases(id), -- Nullable, for manual releases from yard
    release_type VARCHAR(50) CHECK (release_type IN ('auto_tolerance', 'permit_based', 'conditional_redistribution', 'manual')),
    reason TEXT NOT NULL,
    issued_at TIMESTAMPTZ DEFAULT NOW(),
    issued_by_id BIGINT REFERENCES users(id),
    details JSONB
);
CREATE INDEX idx_special_releases_weighing ON special_releases(weighing_id);
CREATE INDEX idx_special_releases_case ON special_releases(case_id) WHERE case_id IS NOT NULL;
```

#### **4.2.8 Vehicle Inspection (Dimensions)**

```sql
-- Vehicle Inspections
CREATE TABLE vehicle_inspections (
    id BIGSERIAL PRIMARY KEY,
    vehicle_id BIGINT REFERENCES vehicles(id),
    station_id BIGINT REFERENCES stations(id),
    inspection_type VARCHAR(50) CHECK (inspection_type IN ('wide_load', 'heavy_vehicle', 'dimension_check')),
    
    -- Measured dimensions
    height_m DECIMAL(5,2),
    width_m DECIMAL(5,2),
    length_m DECIMAL(5,2),
    side_projection_m DECIMAL(5,2),
    front_rear_projection_m DECIMAL(5,2),
    
    -- Compliance
    act_id BIGINT REFERENCES act_definitions(id),
    result VARCHAR(20) CHECK (result IN ('pass', 'fail')),
    permit_no VARCHAR(100), -- Wide load exemption permit
    
    inspected_at TIMESTAMPTZ DEFAULT NOW(),
    inspector_id BIGINT REFERENCES users(id),
    details JSONB
);
CREATE INDEX idx_vehicle_inspections_vehicle ON vehicle_inspections(vehicle_id, inspected_at DESC);
```

#### **4.2.9 Settings & Technical**

```sql
-- Cameras
CREATE TABLE cameras (
    id BIGSERIAL PRIMARY KEY,
    station_id BIGINT REFERENCES stations(id),
    name VARCHAR(255) NOT NULL,
    position VARCHAR(50) CHECK (position IN ('front', 'rear', 'overview')),
    camera_type VARCHAR(50), -- e.g., 'dahua', 'hikvision'
    ip_address INET,
    port INTEGER,
    protocol VARCHAR(10) DEFAULT 'http',
    username VARCHAR(100),
    password_encrypted TEXT, -- Encrypt via AES
    image_path VARCHAR(500), -- URL template for snapshot
    folder_path VARCHAR(500), -- Local storage path
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_cameras_station ON cameras(station_id, is_active);

-- I/O Devices (PLC for boom barriers)
CREATE TABLE io_devices (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    station_id BIGINT REFERENCES stations(id),
    ip_address INET NOT NULL,
    deck_entry BOOLEAN DEFAULT TRUE,
    deck_exit BOOLEAN DEFAULT FALSE,
    status VARCHAR(20) DEFAULT 'active' CHECK (status IN ('active', 'inactive', 'maintenance')),
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Calibration Certificates (for weighbridge scales)
CREATE TABLE calibration_certificates (
    id BIGSERIAL PRIMARY KEY,
    station_id BIGINT REFERENCES stations(id),
    weighbridge_name VARCHAR(255),
    weighbridge_type VARCHAR(100),
    status VARCHAR(20) DEFAULT 'valid' CHECK (status IN ('valid', 'expired', 'pending')),
    date_issued DATE NOT NULL,
    expiry_date DATE NOT NULL,
    certificate_image_path VARCHAR(500),
    days_left INTEGER GENERATED ALWAYS AS (expiry_date - CURRENT_DATE) STORED,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_calibration_certs_station ON calibration_certificates(station_id, expiry_date DESC);
```

#### **4.2.10 Device Sync (Offline Support)**

```sql
-- Device Sync Events (queue for offline client submissions)
CREATE TABLE device_sync_events (
    id BIGSERIAL PRIMARY KEY,
    device_id VARCHAR(100) NOT NULL, -- Unique client identifier
    correlation_id UUID UNIQUE NOT NULL, -- Client-generated idempotency key
    event_type VARCHAR(50) NOT NULL, -- 'weighing_submitted', 'case_updated', etc.
    payload JSONB NOT NULL,
    status VARCHAR(20) DEFAULT 'pending' CHECK (status IN ('pending', 'processing', 'completed', 'failed')),
    error_message TEXT,
    submitted_at TIMESTAMPTZ DEFAULT NOW(),
    processed_at TIMESTAMPTZ
);
CREATE INDEX idx_device_sync_status ON device_sync_events(status, submitted_at);
CREATE INDEX idx_device_sync_correlation ON device_sync_events(correlation_id);
```

---

## 5. Legal Charging Rules & Computation Logic

### 5.1 EAC Vehicle Load Control Act (2016) - Enforcement Regulations (2018)

**Axle Load Limits (from Appendix A in spec):**

| Axle Type | Permissible Load (kg) | Tyre Configuration |
|-----------|----------------------|--------------------|
| Single (steering) | 8,000 | 2 conventional |
| Single (non-steering) | 10,000 | 4 conventional |
| Single (heavy) | 18,000 | 8 conventional |
| Tandem (2-axle unit) | 16,000 | 4 super-single |
| Tandem (2-axle unit) | 24,000 | 12 conventional |
| Tridem (3-axle unit) | 22,500 | 6 super-single |

**GVW Limit:** 56,000 kg max for vehicles ≤ 7 axles (subject to bridge formula).

**Tolerance:**
- **Statutory:** 5% overload margin on axle limits.
- **Operational (configurable):** ≤200 kg on GVW/axle for auto special release (road authority policy).

**Charging Logic:**
1. Compute GVW overload fee using `eac_fee_bands_gvw`.
2. Compute each axle overload fee using `eac_fee_bands_axle` (by axle type).
3. **Charge the higher** of GVW fee vs. maximum axle fee.
4. All fees in USD; convert to KES using daily `currencies.rate_to_usd`.

**Penalty Multipliers (from narration):**
- Overload 201–1500 kg (redistributable): 1× standard fee
- Overload >1500 kg (non-redistributable): 5× standard fee

**API Implementation (pseudo-logic):**

```csharp
public class EacChargeCalculator
{
    public async Task<ChargeResult> CalculateAsync(Weighing weighing, List<WeighingAxle> axles, Act act)
    {
        // 1. Compute GVW overload
        var gvwOverload = weighing.GvwMeasuredKg - weighing.GvwPermissibleKg;
        var gvwFee = gvwOverload > 0 
            ? await GetEacGvwFee(gvwOverload) 
            : 0m;
        
        // 2. Compute axle overloads
        var maxAxleFee = 0m;
        foreach (var axle in axles.Where(a => a.OverloadKg > 0))
        {
            var axleType = DetermineAxleType(axle); // steering, tandem, etc.
            var axleFee = await GetEacAxleFee(axleType, axle.OverloadKg);
            if (axleFee > maxAxleFee) maxAxleFee = axleFee;
        }
        
        // 3. Select higher
        var finalFeeUsd = Math.Max(gvwFee, maxAxleFee);
        var basis = finalFeeUsd == gvwFee ? "gvw" : "axle";
        
        // 4. Apply multiplier if non-redistributable
        if (gvwOverload > 1500 && !CanRedistribute(weighing))
        {
            finalFeeUsd *= 5;
        }
        
        // 5. Convert to KES
        var forexRate = await GetCurrentForexRate("USD", "KES");
        var finalFeeKes = finalFeeUsd * forexRate;
        
        return new ChargeResult 
        { 
            Basis = basis, 
            FeeUsd = finalFeeUsd, 
            FeeKes = finalFeeKes, 
            ForexRate = forexRate 
        };
    }
}
```

### 5.2 Kenya Traffic Act (Cap 403) - KeNHA Schedules

**GVW Limits (from Appendix B in spec):**

| Vehicle Type | Max GVW (kg) |
|--------------|--------------|
| 2-axle rigid (2×4 tires) | 18,000 |
| 3-axle rigid (2–4–4) | 26,000 |
| 3-axle tractor + semi | 28,000 |
| 4-axle rigid (2–2–4–4) | 30,000 |
| 4-axle tractor + semi | 36,000 |
| 5-axle (layout A) | 42,000 |
| 5-axle (layout B) | 44,000 |

**Tolerance:**
- **Axle:** 5% on permissible axle loads (same as EAC).
- **GVW:** No statutory tolerance; configurable operational tolerance (≤200 kg) for auto release.

**Charging Logic:**
1. Compute GVW overload only (axles recorded but not charged).
2. Lookup fee in `traffic_fee_bands_gvw` by overload band.
3. Convert USD to KES (daily forex).

**Fee Bands (KeNHA 2021-2030):**

| Overload (kg) | Fee (USD) | Fee (KSh) |
|---------------|-----------|-----------|
| 1–500 | 235.90 | 25,786 |
| 501–1000 | 482.50 | 52,742 |
| 1001–1500 | 750.55 | 82,043 |

**API Implementation (pseudo-logic):**

```csharp
public class TrafficChargeCalculator
{
    public async Task<ChargeResult> CalculateAsync(Weighing weighing, Act act)
    {
        var gvwOverload = weighing.GvwMeasuredKg - weighing.GvwPermissibleKg;
        if (gvwOverload <= 0) return ChargeResult.NoCharge;
        
        var feeUsd = await GetTrafficGvwFee(gvwOverload);
        var forexRate = await GetCurrentForexRate("USD", "KES");
        var feeKes = feeUsd * forexRate;
        
        return new ChargeResult 
        { 
            Basis = "gvw", 
            FeeUsd = feeUsd, 
            FeeKes = feeKes, 
            ForexRate = forexRate 
        };
    }
}
```

### 5.3 Permit Vehicles (Extended Limits)

**Permit Rules (from narration):**

| Vehicle Class | Base GVW (kg) | Permit Extra Axle (kg) | Permit Extra GVW (kg) | Adjusted GVW Limit (kg) |
|---------------|---------------|------------------------|-----------------------|-------------------------|
| 2A | 18,000 | +3,000 | +1,000 | 19,000 |
| 3A | 28,000 | +3,000 | +2,000 | 30,000 |

**Enforcement Workflow:**
1. Detect active permit via `permits` table.
2. Apply permit-extended limits: `permissible_kg + extra_axle_kg` per axle, `base_gvw + extra_gvw_kg` for GVW.
3. If measured ≤ permit-extended limits: **Special Permit Release** (auto).
4. If above statutory tolerance (≤200 kg) but below permit limits: **Auto Special Release**.
5. If beyond permit allowance: **Prohibit and Prosecute** (charge based on exceedance above base limits, not permit limits).

**API Pseudo-logic:**

```csharp
public async Task<ComplianceResult> EvaluatePermitVehicle(Weighing weighing, Permit permit, AxleConfiguration config)
{
    var extendedGvwLimit = config.BaseGvwKg + permit.ExtraGvwKg;
    var gvwOverload = weighing.GvwMeasuredKg - extendedGvwLimit;
    
    // Check each axle against permit-extended limit
    foreach (var axle in weighing.Axles)
    {
        var extendedAxleLimit = axle.PermissibleKg + permit.ExtraAxleKg;
        if (axle.MeasuredKg > extendedAxleLimit)
        {
            // Beyond permit → Prosecute using base limits
            return ComplianceResult.SendToYard("Exceeds permit allowance");
        }
    }
    
    // Within permit limits
    if (gvwOverload <= 0)
        return ComplianceResult.SpecialRelease("Valid permit within extended limits");
    else
        return ComplianceResult.SendToYard("GVW exceeds permit allowance");
}
```

---

## 6. Module Implementation Details

### 6.1 User Management & Security Module

**Sprint 1: Foundation**

**Entities:**
- User, Role, UserRole, Shift, UserShift, AuditLog

**Features:**
1. User registration (admin-only; seed default admin via migration)
2. JWT-based login (HS256; access token 1hr, refresh token 7d)
3. Role assignment (SuperAdmin, StationManager, Operator, Inspector, etc.)
4. Shift management with rotation groups and day masks
5. Audit logging middleware (intercepts all mutations, logs actor/action/entity/changes)

**API Endpoints:**
```
POST   /api/v1/auth/login
POST   /api/v1/auth/refresh
POST   /api/v1/auth/logout
GET    /api/v1/users
POST   /api/v1/users
PUT    /api/v1/users/{id}
DELETE /api/v1/users/{id}
GET    /api/v1/roles
POST   /api/v1/roles
GET    /api/v1/shifts
POST   /api/v1/shifts
GET    /api/v1/audit-logs
```

**Security Policies:**
- Password: min 8 chars, 1 uppercase, 1 digit, 1 special
- JWT claims: `sub` (user id), `email`, `roles` (array), `station_id` (if applicable)
- RBAC: Decorate controllers with `[Authorize(Roles = "...")]` or custom policies

**Database Migrations:**
```bash
dotnet ef migrations add InitialUserSchema --context AppDbContext
dotnet ef database update
```

**Seed Admin User:**
```csharp
modelBuilder.Entity<User>().HasData(new User
{
    Id = 1,
    Email = "admin@truload.io",
    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
    FullName = "System Administrator",
    Status = "active"
});
```

---

### 6.2 Weighing Module (Core)

**Sprint 2-3: Weight Capture & Compliance Evaluation**

**Entities:**
- Weighing, WeighingAxle, WeightStreamEvent, ScaleTest, AxleConfiguration, AxleConfigurationGroup

**Features:**

**6.2.1 Weighing Process Flow (from KURAWEIGH spec & narration):**

**Screen 1: Vehicle Entry & ANPR**
1. Officer selects bound (A/B) and clicks "Weigh" → open entry boom (send signal to I/O device via API call).
2. Vehicle enters deck; ANPR camera auto-captures number plate and front/overview images.
3. System queries `vehicles` table by `reg_no`:
   - **Existing vehicle:** Auto-populate make, transporter, axle config, permit info.
   - **New vehicle:** Prompt operator to confirm plate and proceed.
4. Operator verifies/corrects plate (edit action logged in audit).

**Screen 2: Axle Config & Details Capture**
1. Auto-detect or prompt for axle configuration selection from `axle_configurations`.
2. Capture: origin, destination, cargo type, driver details (auto-sync from `drivers` or add new), transporter.
3. Select road and applicable Act (EAC or Traffic) based on station default; allow override.

**Screen 3: Weight Capture (3 modes)**

**Mode A: Static (Multi-Deck)**
- TruConnect microservice streams live weights per deck (JSON: `{deck: 1, weight: 7950, stable: true}`).
- UI polls `/truconnect/weights` or receives via SignalR hub.
- Operator locks each deck when stable; backend stores in `weighing_axles` with axle numbers mapped per config.

**Mode B: WIM (Weigh-In-Motion)**
- As vehicle moves over scale, TruConnect auto-captures highest stable weight per axle.
- Backend receives axle stream and stores in `weight_stream_events`; finalizes stable values in `weighing_axles`.

**Mode C: Mobile/Axle-by-Axle**
- Operator manually assigns weight per axle (button click per axle).
- Vehicle advances; repeat for next axle; GVW auto-sums.

**Compliance Evaluation (after weight capture):**
1. Sum axle weights → `gvw_measured_kg`.
2. Lookup `gvw_permissible_kg` from `axle_configurations.base_gvw_kg` or vehicle-specific permit-extended value.
3. For each axle, compare `measured_kg` vs `permissible_kg` from `axle_configuration_groups`.
4. Apply tolerance policies:
   - If all axles within tolerance AND GVW within tolerance → **Compliant** → Generate weigh ticket.
   - If axle/GVW overload ≤200 kg (or configured tolerance) → **Auto Special Release**.
   - If axle overload >200 kg but ≤1500 kg (redistributable) → **Send to Yard** (reason: redistribution).
   - If GVW overload >200 kg → **Send to Yard** (reason: gvw_overload).
5. If has valid permit: evaluate against permit-extended limits before applying above logic.

**Decision Actions:**

| Condition | Action |
|-----------|--------|
| Within limits | Generate ticket → Open exit boom → Vehicle exits |
| Overload ≤200 kg | Auto Special Release → Exit |
| Axle overload >200 kg, ≤1500 kg (redistributable) | Send to Yard → Prohibition Order → Redistribution flow |
| GVW overload >200 kg | Send to Yard → Prosecution |
| Valid permit, within permit limits | Permit-based Special Release → Exit |
| Permit, exceeds permit allowance | Send to Yard → Prosecution (charge base + permit exceedance) |

**Reweigh Loop:**
- Max 8 cycles (tracked via `reweigh_cycle_no`).
- Each reweigh creates new `weighings` record with `original_weighing_id` linking to first.
- Upon zero overload, generate compliance certificate.

**API Endpoints:**
```
POST   /api/v1/weighings                      # Create new weighing
GET    /api/v1/weighings/{id}
PUT    /api/v1/weighings/{id}/axles           # Batch update axle weights
POST   /api/v1/weighings/{id}/send-to-yard
POST   /api/v1/weighings/{id}/reweigh
GET    /api/v1/weighings/ticket/{ticketNo}
POST   /api/v1/scale-tests                    # Daily calibration test
GET    /api/v1/axle-configurations
POST   /api/v1/truconnect/weights/stream      # SignalR hub
```

**Database Triggers & Functions:**
- Auto-generate `ticket_no` using station code + date + sequence.
- Trigger on `weighings` insert → publish `WeighingRecordedEvent` to RabbitMQ.

---

### 6.3 Prosecution Module

**Sprint 4-5: Case Management & Charging**

**Entities:**
- ProsecutionCase, ProsecutionParty, ChargeBreakdown, Invoice, Receipt, LoadCorrection, ComplianceCertificate

**Process Flow (from narration):**

**6.3.1 Case Initialization**
1. When `weighings.is_sent_to_yard = TRUE`, system auto-posts to `yard_entries`.
2. Auto-generate `prohibition_orders` with doc_no, inspector, yard location.
3. Officer opens case from Yard List → creates `prosecution_cases` record.

**6.3.2 Case Details Capture**
Form sections (map to `prosecution_parties` and case fields):
1. **Driver Details:** ID/Passport, License, Names, Gender, Nationality, Address, NTAC No
2. **Owner Details:** Same fields
3. **Location:** Road used, District, County, Station, OB No
4. **Court:** Court name, Complainant officer, Investigating officer
5. **Prohibition:** Inspector, Yard location, Repairs needed, Offload truck reg (if applicable)
6. **Transporter:** Name, Address
7. **Act Selection:** EAC or Traffic (pre-selected based on road)

**6.3.3 Charge Computation**

**EAC Workflow:**
1. Fetch weighing data (`gvw_overload_kg`, axle overloads from `weighing_axles`).
2. Compute GVW fee and axle fees (as per 5.1).
3. Store both in `charge_breakdowns` (basis='gvw', basis='axle').
4. Generate EAC Certificate document (PDF) showing:
   - Vehicle info, overload type, magnitude
   - Applicable clause/section
   - Computed fine (USD + KES)
5. Create `invoices` record linked to eCitizen API.

**Traffic Workflow:**
1. Compute GVW fee only.
2. Generate Traffic Act Certificate (PDF).
3. Create invoice.

**6.3.4 Payment & Compliance**
1. Invoice paid (webhook from eCitizen) → insert `receipts`, update `invoices.status = 'paid'`.
2. Officer generates Load Correction Memo → insert `load_corrections`.
3. Reweigh vehicle (link via `original_weighing_id`) until zero overload.
4. Generate Compliance Certificate → insert `compliance_certificates`, release vehicle.

**6.3.5 Court Escalation**
- "Escalate to Court" button → assign `ntac_no`, update `prosecution_cases.status = 'escalated'`.
- Link to external Case Management System via API or manual OB entry.

**API Endpoints:**
```
GET    /api/v1/yard                           # List yard entries
POST   /api/v1/cases                          # Create case from yard entry
PUT    /api/v1/cases/{id}/parties             # Update driver/owner details
POST   /api/v1/cases/{id}/compute-charges     # Calculate fees
POST   /api/v1/cases/{id}/invoice             # Generate invoice
POST   /api/v1/cases/{id}/receipt             # Record payment
POST   /api/v1/cases/{id}/load-correction     # Add memo
POST   /api/v1/cases/{id}/compliance          # Issue certificate
POST   /api/v1/cases/{id}/escalate-to-court
GET    /api/v1/cases/{id}/documents/{type}    # Download PDF
```

**Document Generation:**
- Use template engine (e.g., QuestPDF, DinkToPdf) to generate:
  - Prohibition Order
  - EAC Certificate
  - Traffic Act Certificate
  - Invoice
  - Receipt
  - Load Correction Memo
  - Compliance Certificate

---

### 6.4 Special Release Module

**Sprint 6: Tolerance & Permit Releases**

**Entities:**
- SpecialRelease, Permit

**Release Types:**
1. **Auto Tolerance Release:** Overload ≤200 kg (or configured) → auto-generate during weighing decision.
2. **Permit-Based Release:** Valid permit, within extended limits → auto-generate.
3. **Conditional Release:** Post-redistribution, axle compliant → auto-generate after reweigh.
4. **Manual Release:** Authorized officer issues release from Yard List with reason.

**API Endpoints:**
```
POST   /api/v1/releases                       # Manual release
GET    /api/v1/releases/{id}
GET    /api/v1/permits
POST   /api/v1/permits                        # Register permit
PUT    /api/v1/permits/{id}/block             # Permit blocking feature
```

**Business Rules:**
- Auto releases logged with `release_type='auto_tolerance'` or `'permit_based'`.
- Manual releases require officer ID and justification (audited).

---

### 6.5 Vehicle Inspection Module

**Sprint 7: Dimensional Compliance**

**Legal Limits (from narration & KURAWEIGH Appendix):**

| Parameter | Rigid Chassis | Articulated | Combined | EAC | Traffic |
|-----------|---------------|-------------|----------|-----|---------|
| **Height** | 4.3m | 4.3m | 4.3m | ✓ | 4.2m |
| **Width** | 2.65m | 2.65m | 2.65m | ✓ | ✓ |
| **Length** | 12.5m | 17.4m | 22m | ✓ | ✓ |
| **Side Projection** | 0.3m | 0.3m | 0.3m | ✓ | ✓ |
| **Front+Rear Projection** | 1.8m | 1.8m | 1.8m | ✓ | ✓ |

**Process:**
1. Officer opens inspection screen linked to vehicle.
2. Captures dimensions (manual or via sensors if available).
3. System validates against act-specific limits (from settings or hardcoded constants).
4. If within limits: Pass → attach to weigh ticket.
5. If exceeds: Check for Wide Load Exemption Permit → if valid, permit-based release; else, prohibit and escalate.

**API Endpoints:**
```
POST   /api/v1/inspections
GET    /api/v1/inspections/{id}
GET    /api/v1/inspections/vehicle/{vehicleId}
```

---

### 6.6 Reporting Module

**Sprint 8: Analytics & Exports**

**Report Types (from KURAWEIGH spec):**
1. Axle Configuration Report
2. Weighbridge Register
3. Daily/Hourly Statistics
4. Monthly Statistics
5. Overload & Reweigh Reports
6. Impounded & Prohibited Vehicles
7. Shift Reports
8. Scale Test Reports
9. Transporter Statements
10. Wide Load Reports
11. Heavy Vehicle Inspection Reports
12. Special Release Reports
13. GVW & Axle Reports
14. Daily Charged Reports
15. Initial vs. Redistribution Comparison

**Implementation:**
- Use Dapper for high-performance read queries (bypass EF tracking).
- Pre-aggregate data in materialized views where beneficial.
- Export formats: PDF, Excel (ClosedXML), CSV.

**API Endpoints:**
```
GET    /api/v1/reports/weighbridge-register?from=...&to=...
GET    /api/v1/reports/overload-summary?station=...
GET    /api/v1/reports/charged-vehicles?act=EAC&from=...
GET    /api/v1/reports/export/{reportType}?format=pdf
```

---

### 6.7 Settings & Technical Module

**Sprint 9: Configuration Management**

**Features:**
1. **Camera Settings:** CRUD for ANPR/overview cameras (IP, port, auth, snapshot URL template).
2. **Station Settings:** Cluster assignment, default route, bound, domain/IP fallback.
3. **I/O Settings:** PLC devices for boom barriers (IP, deck entry/exit flags).
4. **Prosecution Defaults:** Auto-populate station, court, district in case forms.
5. **Dollar Rate Management:** Daily forex rate entry (USD→KES); audit trail.
6. **Calibration Certificates:** Upload and track expiry; alert when nearing expiration.

**API Endpoints:**
```
GET    /api/v1/settings/cameras
POST   /api/v1/settings/cameras
PUT    /api/v1/settings/cameras/{id}
GET    /api/v1/settings/stations
POST   /api/v1/settings/io-devices
GET    /api/v1/settings/forex-rates
POST   /api/v1/settings/forex-rates           # Daily update
GET    /api/v1/technical/calibration-certs
POST   /api/v1/technical/calibration-certs
GET    /api/v1/technical/health-check         # Check camera/scale/PLC connectivity
```

---

## 7. Performance Optimization

### 7.1 Indexing Strategy

**Hot Query Paths:**
- `weighings(vehicle_id, weighed_at DESC)` → vehicle history
- `weighings(station_id, weighed_at DESC)` → station activity
- `weighings(ticket_no)` → unique lookup
- Partial index on `weighings(is_sent_to_yard)` WHERE TRUE → yard list queries
- GIN index on JSONB columns if heavy filtering (e.g., `weight_stream_events.raw_json`)

### 7.2 Partitioning
- Monthly partitions on `weighings(weighed_at)` → archive old partitions after retention period.
- Detach and drop partitions older than 2 years (configurable).

### 7.3 Caching (Redis)
**Cache Keys:**
- `axle:configs` → all axle configurations (refresh on update)
- `fee:bands:eac:gvw` → EAC GVW fee bands
- `fee:bands:traffic:gvw` → Traffic GVW fee bands
- `station:config:{stationId}` → station settings (cameras, I/O, defaults)
- `currency:usd:kes:latest` → latest forex rate

**Cache TTL:**
- Static reference data: 24 hours (sliding, bust on CRUD)
- Dynamic data (forex): 1 hour

**Cache Invalidation:**
- On updates to `axle_configurations`, `eac_fee_bands_*`, `traffic_fee_bands_*`, publish `CacheInvalidationEvent` → background service clears keys.

### 7.4 Concurrency & Locking
- **Idempotency:** Client generates `correlation_id` (UUID); backend checks `device_sync_events` for duplicates.
- **Advisory Locks:** During reweigh cycles, acquire PostgreSQL advisory lock on `vehicle_id` to prevent simultaneous reweighs.
- **Optimistic Concurrency:** EF Core row versioning (timestamp) on `weighings`, `prosecution_cases`.

---

## 8. Integration Points

### 8.1 TruConnect Microservice
- **Type:** Node.js/Electron service running on client machine.
- **Function:** Connects to scale indicator via Serial/RF/Ethernet, reads weights, exposes HTTP endpoint.
- **Contract (JSON):**
```json
{
  "deck": 1,
  "weight": 7950,
  "stable": true,
  "timestamp": "2025-10-28T12:34:56Z"
}
```
- **Backend Handling:** Poll or SignalR stream from TruConnect; validate and store in `weight_stream_events`; lock stable values into `weighing_axles`.

### 8.2 eCitizen / Road Authority Payment Gateway
- **Invoice Creation:** POST to external API with case details, amount.
- **Webhook Callback:** Receive payment confirmation → update `invoices.status`, insert `receipts`.
- **Retry Logic:** Polly retry policy (3 attempts, exponential backoff).

### 8.3 Case Management System (NTAC/OB)
- **NTAC Assignment:** Auto-generate or manual entry.
- **OB Integration:** API call to police system (if available) or manual OB number entry.
- **Data Exchange:** JSON over HTTPS with mutual TLS.

---

## 9. Offline & Sync Architecture

### 9.1 Client Offline Strategy
- **Frontend:** Stores draft weighings in IndexedDB with status='queued'.
- **Submission:** On network restore, POST to `/api/v1/device-sync/submit` with `correlation_id`.
- **Backend:** Inserts into `device_sync_events`, background worker processes queue.

### 9.2 Idempotency & Conflict Resolution
- **Idempotency Key:** `correlation_id` (UUID) generated by client.
- **Duplicate Detection:** Check `device_sync_events.correlation_id` before processing.
- **Conflicts:** If ticket_no collision detected, append suffix and notify operator.

---

## 10. API Design Principles

### 10.1 Versioning
- URL-based: `/api/v1/...`, `/api/v2/...`
- Header-based fallback: `Accept: application/vnd.truload.v1+json`

### 10.2 Response Format
```json
{
  "success": true,
  "data": { ... },
  "meta": {
    "timestamp": "2025-10-28T12:34:56Z",
    "version": "v1"
  },
  "errors": []
}
```

### 10.3 Error Handling
- **400 Bad Request:** Validation failures (FluentValidation messages).
- **401 Unauthorized:** Missing/invalid JWT.
- **403 Forbidden:** Insufficient role permissions.
- **404 Not Found:** Entity not found.
- **409 Conflict:** Duplicate ticket_no or correlation_id.
- **500 Internal Server Error:** Logged to Serilog; generic message to client.

### 10.4 Rate Limiting
- **Nginx Ingress:** `nginx.ingress.kubernetes.io/limit-rps: "100"`
- **AspNetCoreRateLimit:** Per-client IP throttling (10 req/sec for writes, 50 req/sec for reads).

---

## 11. Security & Compliance

### 11.1 Authentication & Authorization
- **JWT:** HS256 (symmetric) with secret in K8s secret; rotate every 90 days.
- **Claims:** User ID, email, roles array, station ID (if assigned).
- **RBAC Policies:** Define custom policies (e.g., `CanProsecute`, `CanReleaseVehicle`).

### 11.2 Data Encryption
- **At Rest:** PostgreSQL transparent data encryption (TDE) or disk-level encryption.
- **In Transit:** TLS 1.3 for all API calls.
- **Sensitive Fields:** Encrypt camera passwords, PLC credentials using AES-256 (store key in K8s secret).

### 11.3 Audit Trail
- **Middleware:** Intercept all POST/PUT/DELETE requests → log to `audit_logs`.
- **Immutable Logs:** Append-only; never delete (retention: 7 years per compliance).

### 11.4 Backup & Restore
- **Database:** Daily PostgreSQL backups via CronJob (pg_dump).
- **Media/Docs:** S3-compatible storage (MinIO or AWS S3) with versioning.

---

## 12. Testing Strategy

### 12.1 Unit Tests (xUnit)
- Test charge calculation logic with known inputs (EAC/Traffic fee bands).
- Mock external services (TruConnect, eCitizen).
- FluentAssertions for readable assertions.

### 12.2 Integration Tests (Testcontainers)
- Spin up PostgreSQL, Redis, RabbitMQ containers.
- Seed test data (axle configs, fee bands).
- Test full weighing → prosecution → payment flow.

### 12.3 Load Testing (k6, NBomber)
- Simulate 100 concurrent weighings/sec.
- Verify HPA scales pods correctly.
- Monitor latency P99 < 500ms for reads, < 2s for writes.

---

## 13. Deployment & DevOps

### 13.1 Docker Build
- Multi-stage Dockerfile:
  - Stage 1: `mcr.microsoft.com/dotnet/sdk:8.0` → restore, build, publish
  - Stage 2: `mcr.microsoft.com/dotnet/aspnet:8.0` → runtime
- Health endpoint: `/health` (returns 200 OK if DB connectable, Redis pingable).

### 13.2 Kubernetes Manifests (via centralized devops-k8s)
- ArgoCD app: `devops-k8s/apps/truload-backend/app.yaml`
- Helm values: `devops-k8s/apps/truload-backend/values.yaml`
- Chart: `devops-k8s/charts/app` (shared template)

### 13.3 CI/CD Pipeline (GitHub Actions)
- **Trigger:** Push to `main` branch under `TruLoad/truload-backend/**`.
- **Steps:**
  1. Checkout code.
  2. Install devops tools (via central action).
  3. Run `build.sh` (Trivy scan, Docker build/push, K8s apply, Helm values update).
  4. Tag `:latest` image.
  5. Health check via ingress.

### 13.4 Observability
- **Logs:** Serilog → Seq or ELK.
- **Metrics:** Prometheus scrape `/metrics` endpoint (app.UsePrometheusMetrics).
- **Traces:** OpenTelemetry → Jaeger.
- **Dashboards:** Grafana with pre-built panels (request rate, error rate, DB query duration).

---

## 14. Sprint Breakdown (16-Week Plan)

| Sprint | Duration | Module | Deliverables |
|--------|----------|--------|--------------|
| **1** | Week 1-2 | User & Security | Auth (JWT), Roles, Shifts, Audit logs, RBAC policies, Seed admin |
| **2** | Week 3-4 | Weighing (Setup) | Stations, Vehicles, Drivers, Transporters, Axle configs seed, Camera/I/O CRUD |
| **3** | Week 5-6 | Weighing (Core) | Static/WIM/Axle modes, TruConnect integration, Compliance evaluation, Ticketing |
| **4** | Week 7-8 | Yard & Tags | Yard entries, Prohibition orders, Tag lifecycle, Redistribution flow |
| **5** | Week 9-10 | Prosecution (EAC) | Case intake, EAC charge computation, Invoice/Receipt, Documents |
| **6** | Week 11-12 | Prosecution (Traffic) | Traffic charge computation, Court escalation, Load correction, Compliance cert |
| **7** | Week 13 | Special Release | Auto/manual releases, Permit CRUD, Permit blocking |
| **8** | Week 14 | Inspection | Dimension capture, Compliance check, Permit validation |
| **9** | Week 15 | Reporting | Registers, Analytics, Exports (PDF/Excel) |
| **10** | Week 16 | Polish | Settings UI, Health checks, Documentation, Load testing |

---

## 15. Sample API Request/Response

### Create Weighing (Static Mode)

**Request:**
```http
POST /api/v1/weighings
Authorization: Bearer eyJhbGc...
Content-Type: application/json

{
  "stationId": 5,
  "vehicleRegNo": "KDQ548Z",
  "driverId": 123,
  "weighingType": "static",
  "actId": 1,
  "bound": "A",
  "axleConfigurationCode": "3A",
  "originId": 10,
  "destinationId": 20,
  "cargoId": 3,
  "axles": [
    {"axleNumber": 1, "measuredKg": 7900},
    {"axleNumber": 2, "measuredKg": 19200},
    {"axleNumber": 3, "measuredKg": 10000}
  ]
}
```

**Response (Compliant):**
```json
{
  "success": true,
  "data": {
    "weighingId": 142,
    "ticketNo": "ATMBA202510000124",
    "gvwMeasuredKg": 37100,
    "gvwPermissibleKg": 28000,
    "gvwOverloadKg": 9100,
    "isCompliant": false,
    "decision": "send_to_yard",
    "reason": "GVW overload exceeds tolerance",
    "yardEntryId": 87
  },
  "meta": {"timestamp": "2025-10-28T14:22:10Z"}
}
```

---

## 16. Conclusion

This plan provides a comprehensive roadmap for building the TruLoad backend using modern .NET practices, ensuring legal compliance with EAC and Traffic Act regulations, optimizing for high concurrency and offline resilience, and integrating seamlessly with the centralized devops-k8s infrastructure. All modules are designed with testability, maintainability, and auditability as core principles.

**Next Steps:**
1. Initialize .NET solution with projects per module.
2. Set up PostgreSQL schema via EF Core migrations.
3. Seed axle configurations and fee bands.
4. Implement User Management (Sprint 1).
5. Proceed with Weighing module (Sprints 2-3).

