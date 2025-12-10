# TruLoad Backend - Implementation Plan

## Table of Contents
1. [Development Status](#development-status)
2. [Executive Summary](#executive-summary)
3. [Technology Stack](#technology-stack)
4. [System Architecture](#system-architecture)
5. [Module Workflows (FRD-Aligned)](#module-workflows-frd-aligned)
6. [Database Schema](#database-schema)
7. [Legal Computation Rules](#legal-computation-rules)
8. [Data Analytics Integration](#data-analytics-integration)
9. [Performance & Concurrency](#performance--concurrency)
10. [Integrations](#integrations)
11. [Security & Compliance](#security--compliance)
12. [DevOps & Deployment](#devops--deployment)
13. [Sprint Delivery Plan](#sprint-delivery-plan)
14. [References](#references)

---

## Development Status

**Last Updated:** December 9, 2025, 19:30 UTC

### Current Phase: Sprint 1 - Phase 2 (Authorization & Enforcement)

**Build Status:** ✅ **HEALTHY** (0 errors, 13 warnings)  
**Application Status:** ✅ **RUNNING** (localhost:4000)  
**Database:** ✅ **UP-TO-DATE** (All pending migrations applied)

**Phase 2 Completions (Dec 9, 2025):**
- ✅ Audit logging middleware fully operational
- ✅ Audit log repository with 9 query methods implemented
- ✅ Repository structure refactored (consistent namespace pattern across all modules)
- ✅ DbContext configuration corrected for audit log properties
- ✅ Test files updated to reflect repository structure changes
- ✅ AuditLogSummaryDto created for audit statistics and reporting

**Next Priority Tasks (Recommended Sequence):**
1. **Authorization Policy Implementation** - Create [Authorize(Policy="...")] handlers for permission-based access
2. **Auth-Service Integration Completion** - Implement JWKS caching, token validation, user sync service
3. **Security & Authorization Phase** - Add [Authorize] attributes to all 90+ endpoints
4. **User Management Endpoints** - Implement user CRUD operations and filtering
5. **Role Management Endpoints** - Implement role CRUD and user-role assignment
6. **Shift Management** - Complete shift CRUD and user-shift assignment endpoints

---

## Executive Summary

**System Purpose:** Cloud-hosted intelligent weighing and enforcement solution enabling roadside officers to capture vehicle weights, verify compliance with EAC Vehicle Load Control Act (2016) or Kenya Traffic Act (Cap 403), and manage enforcement actions (prosecution, redistribution, special release).

**Key Capabilities:**
- Multi-mode weighing: Static (multi-deck), WIM (Weigh-In-Motion), Mobile/Axle weighing
- Real-time weight acquisition via TruConnect microservice
- Offline-aware client systems with automatic cloud sync
- Legal compliance enforcement with automated charge computation
- Integration with eCitizen/Road Authority payment gateways
- Court case tracking (NTAC, OB numbers)
- Comprehensive audit trails and reporting
- Natural language query processing with AI-powered vector search
- Advanced analytics and BI dashboards via Apache Superset integration

---

## Technology Stack

### Core Framework
- **Language:** C# 12
- **Framework:** .NET 8 (LTS) Web API
- **Architecture Pattern:** Modular Monolith with CQRS (MediatR), Vertical Slice Architecture

### Data & Caching
- **Primary Database:** PostgreSQL 16+ (Npgsql, Entity Framework Core 8) with pgvector extension
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
- **AI/ML:** ONNX Runtime for text-to-vector embeddings
- **Vector Database:** pgvector extension for PostgreSQL

### DevOps & Observability
- **Containerization:** Docker multi-stage builds
- **Orchestration:** Kubernetes (via centralized devops-k8s)
- **CI/CD:** GitHub Actions → ArgoCD
- **Monitoring:** Prometheus + Grafana, OpenTelemetry
- **APM:** Application Insights or Jaeger distributed tracing
- **Scaling:** HPA (Horizontal Pod Autoscaling) and VPA (Vertical Pod Autoscaling) for DA services

---

## System Architecture

### Architectural Principles
1. **Modular Boundaries:** Each module (User, Weighing, Prosecution, etc.) is a vertical slice with its own controllers, commands/queries, validators, and domain logic
2. **Domain Events:** Cross-module communication via RabbitMQ (e.g., `WeighingCompleted`, `VehicleSentToYard`, `InvoicePaid`)
3. **API-First:** REST endpoints with versioning (`/api/v1/...`); SignalR hubs for real-time weight streaming
4. **Offline Resilience:** Idempotent operations; client-generated correlation IDs; conflict resolution strategies
5. **Performance:** Read/write separation via materialized views; Redis caching for hot data; partitioned tables
6. **Security:** RBAC with claims-based authorization; audit logs for all mutations; encrypted sensitive fields

### Communication & Integration Patterns
- **Synchronous:** REST via shared HTTP client with retries/circuit breaker; service discovery via Kubernetes DNS (e.g., `auth-service.auth.svc.cluster.local`).
- **Async:** Domain events published through RabbitMQ outbox with versioned subjects (e.g., `truload.user.synced.v1`), DLQ configured.
- **Real-time:** SignalR reserved for TruConnect weight streaming; avoid ad-hoc websocket use elsewhere.
- **Webhooks:** eCitizen payment callbacks and any external partner notifications; validate HMAC and idempotency keys.
- **Avoid Duplication:** Only store foreign IDs for external systems (auth-service user IDs, NTSA references) and keep `auth_service_user_id` as the identity link.
| **Inspection** | Dimensional compliance (wide load) | VehicleInspections |
| **Reporting & Analytics** | Registers, analytics, exports, BI dashboards | Dynamic report generation, Superset dashboards |
| **Settings** | System config, stations, cameras, I/O, prosecution defaults | Stations, Cameras, IoDevices, SystemSettings |
| **Security** | Audit logs, backups, password policies | AuditLogs, PasswordHistory |

---

## Module Workflows (FRD-Aligned)

### A.1 - Weighing Process Workflow

**Service Initiation:** Frontend (PWA) - Weighing screen / Station dashboard

**Pre-conditions:**
- Officer logged in (or PWA has offline credentials cached)
- PWA service-worker registered and IndexedDB initialized
- TruConnect running and connected to attached scale(s)
- Scale calibration certificate valid and scale test completed for the day
- Permissions: officer role authorizes weighing/prosecution actions

**Process Flow:**

1. **Vehicle Entry & ANPR**
   - Officer selects bound (A/B) and clicks "Weigh" → open entry boom (send signal to I/O device)
   - Vehicle enters deck; ANPR camera auto-captures number plate and front/overview images
   - System queries `vehicles` table by `reg_no`
   - Operator verifies/corrects plate (edit action logged in audit)

2. **Axle Config & Details Capture**
   - Auto-detect or prompt for axle configuration selection
   - Capture: origin, destination, cargo type, driver details, transporter
   - Select road and applicable Act (EAC or Traffic) based on station default

3. **Weight Capture (3 modes)**
   - **Static (Multi-Deck):** TruConnect streams live weights per deck; operator locks each deck when stable
   - **WIM (Weigh-In-Motion):** Auto-captures highest stable weight per axle as vehicle moves
   - **Mobile/Axle-by-Axle:** Operator manually assigns weight per axle as vehicle advances
   - **Offline Resilience:** Client generates `weighing_id` (UUID) locally to ensure consistent identification before sync

4. **Compliance Evaluation**
   - Sum axle weights → `gvw_measured_kg`
   - Lookup `gvw_permissible_kg` from axle configuration or permit-extended value
   - Compare each axle measured vs permissible
   - Apply tolerance policies
   - Decision: Compliant → Ticket | Overload ≤200kg → Auto Special Release | Overload >200kg → Send to Yard

5. **Reweigh Loop**
   - Max 8 cycles (tracked via `reweigh_cycle_no`)
   - Upon zero overload, generate compliance certificate

**Post-conditions:**
- Weight Ticket generated for compliant vehicles
- Prohibition Order generated and case queued for prosecution for non-compliant vehicles
- Invoice generated when applicable and queued for eCitizen submission/payment
- Local IndexedDB updated with synced statuses

**Documents Generated:**
- Weight Ticket
- Permit Document (if applicable)
- Special release certificate (in case of open previous manual tag)

**Third Party Integrations:**
- ANPR & Camera system
- KeNHA Tags API
- KeNHA Permit API
- NTSA APIs (vehicle search)
- eCitizen payment gateway
- Media/file storage (cloud blob)
- GPS/location service

---

### A.2 - Tags Process Workflow

**Service Initiation:** Frontend (PWA) - Tags Screen

**Process Flow:**
1. System automatically tags vehicles based on predefined rules (e.g., repeated offenses, overload history)
2. Officers can manually create tags with reason and category
3. Tags can be associated with stations and time periods
4. Tag lifecycle: open → closed (with reason and closure timestamp)
5. Tags exported to external systems (KeNHA) when applicable

**Tag Types:**
- **Automatic:** System-generated based on violation patterns
- **Manual:** Officer-created with justification

**Tag Status:**
- **Open:** Active tag requiring attention
- **Closed:** Resolved tag with closure reason

---

### A.3 - Scale Test Workflow

**Process Flow:**
1. Daily calibration check required before weighing operations
2. Operator places known test weight on scale
3. System records measured weight, expected weight, deviation
4. Test result: Pass/Fail based on tolerance thresholds
5. Failed tests block weighing operations until resolved
6. Test history maintained for compliance and audit

---

### B.1 - Case Register / Special Release Process Workflow

**Service Initiation:** Frontend (PWA) - Case Register screen triggered from weighing or yard entry

**Process Flow:**

1. **Case Initialization**
   - System auto-creates case entry from Prohibition Order and Weighing Data
   - Officer verifies/edits/captures vehicle, driver, owner, prohibition id, location, time, officer details

2. **Case Processing Paths**
   - **Special Release:** Request admin authorization → Create conditional Load Correction Memo → Redistribute & schedule reweigh (optional) → Compliance certificate (optional) → Special release certificate
   - **Pay Now:** Push to Prosecution Module (compute charges, raise Traffic Act Charge Sheet, Generate invoice) → On payment confirmation attach receipt → Generate Load Correction Memo → Schedule reweigh → Generate Compliance certificate
   - **Court Process:** Escalate to Court → Case Manager with required subfiles

3. **Case Finalization**
   - Update case status with required checklist for finalisation
   - Log all closing actions to Subfile J (Minute sheet & correspondences)

**Printable Outputs:**
- Load Correction Memo
- Compliance Certificate
- Special release certificate

**Case Register Requirements:**
- All violations must first be recorded in the Case Register module before any further action
- Module tracks all case outcomes (settled, escalated, special release)
- Maintains initial case details (Driver, Owner, Transporter, Prohibition Order)
- Case status changes automatically when closed in prosecution/case manager modules
- Produces relevant reports (Special Releases, Pending vs Finalised, Redistribution Vs Original Weights)

---

### B.2 - Prosecution Process Workflow

**Service Initiation:** Frontend (PWA) - Prosecution screen, triggered from Case Register "Pay Now" action

**Process Flow:**

1. **Case Intake**
   - System pre-fills from Case Register and Weighing Data
   - Capture Prosecution Officer Details (synced from logged in user)
   - Verify Driver, Owner, Transporter details
   - Select applicable Act (EAC or Traffic)

2. **Charge Computation**
   - **EAC Act:** Compute GVW overload fee and axle overload fees; select higher; apply penalty multipliers if applicable
   - **Traffic Act:** Compute GVW overload fee only (axles recorded but not charged)
   - Store charge breakdowns with fee bands references
   - Generate EAC Certificate or Traffic Act Certificate (PDF)

3. **Invoice Generation**
   - Create invoice record linked to eCitizen API
   - Invoice includes total USD and KES (using daily forex rate)

4. **Payment Processing**
   - Receive payment confirmation via eCitizen webhook
   - Insert receipt record
   - Update invoice status to 'paid'

5. **Load Correction & Reweigh**
   - Generate Load Correction Memo
   - Schedule reweigh vehicle until zero overload
   - Generate Compliance Certificate upon compliance confirmation

6. **Court Escalation (if applicable)**
   - Assign NTAC number
   - Update case status to 'escalated'
   - Link to external Case Management System via API or manual OB entry

**Prosecution Requirements:**
- Generate Charge Sheet where GVW is exceeded or as configured per Kenya Traffic Act
- Generate onsite invoice for charges settlement
- Generate Receipt for invoices paid
- Generate Load correction memo and reweigh vehicle to confirm compliance
- Generate Compliance Certificates for compliant vehicles after re-weigh
- Develop prosecuted vehicle database and offender's database including outcomes of past
- Link in case management module to follow up on habitual offenders/previous convictions
- Produce Management Reports (Daily/Periodic Overload reports, Prosecution Status reports, Court Case status report, Prosecutions per Weighbridge or Cluster, Summary of Prosecutions per vehicle)

---

### Case Management Module Workflow

**Process Flow:**

1. **Case Tracking**
   - Track cases from inception to final ruling
   - Create and track cases related to vehicle overloads and other violations
   - Maintain violation history tracking for repeat offenders

2. **Court Case Tracking**
   - Track court cases right from case inception to final ruling in court
   - Maintain case subfiles from A-J (following legal document organization standards)
   - Link NTAC numbers and OB numbers

3. **Yard Integration**
   - Integrates with Yard sub-module to manage the real status of vehicles in yard
   - Keep count of each vehicle status (pending, processing, released, escalated)

**Case Subfiles (A-J):**
- Subfile A: Initial case details and violation information (= case_registers table)
- Subfile B: Document Evidence (weight tickets, photos, ANPR footage, permit documents)
- Subfile C: Expert Reports (engineering/forensic reports)
- Subfile D: Witness Statements (inspector/driver/witnesses)
- Subfile E: Accused Statements & Reweigh/Compliance documents
- Subfile F: Investigation Diary (investigation steps, timelines)
- Subfile G: Charge Sheets, Bonds, NTAC, Arrest Warrants
- Subfile H: Accused Records (prior offences, identification documents)
- Subfile I: Covering Report (prosecutorial summary memo)
- Subfile J: Minute Sheets & Correspondences (court minutes, adjournments, correspondence)

- Subfile J: Minute Sheets & Correspondences (court minutes, adjournments, correspondence)

---

### Technical Module Workflows

#### C.1 - Hardware Health Monitoring
**Process Flow:**
1. **Background Polling:** Hangfire job polls registered devices (`weighbridge_hardware`) every minute.
2. **Status Check:** Pings IP/Port or queries device status API (e.g., Zedem indicator).
3. **Logging:** Updates `weighbridge_hardware.status` and inserts log into `hardware_health_logs`.
4. **Alerting:** If critical device (Indicator/Camera) goes offline, trigger alert to station manager dashboard.

#### C.2 - Calibration Management
**Process Flow:**
1. **Validity Check:** System checks `calibration_certificates` validity during weighing initiation.
2. **Expiry Warning:** Notify admins 30 days before certificate expiry.
3. **Lockout:** If certificate expired (`valid_to` < Today), block weighing operations for that station/scale.
4. **Renewal:** Admin uploads new certificate PDF → System updates validity dates → Unlocks station.

#### C.3 - System Configuration & Bidirectional Logic
**Configuration Loading:**
1. **Startup:** Backend loads `system_settings` (Look & Feel, Core) and `api_configurations` into Redis cache.
2. **Station Context:** Frontend fetches `station_bounds` for the current station.
3. **Bidirectional Weighing:**
   - If `stations.supports_bidirectional` is TRUE, operator selects Bound (A/B).
   - System resolves `virtual_station_code` (e.g., ROKSA) from `station_bounds` based on selection.
   - Ticket is generated using the Virtual Station Code.

---

## Database Schema

For detailed database schema including all entities, properties, indexes, relationships, views, and vector columns, refer to [erd.md](./erd.md).

**Key Schema Highlights:**
- **Naming Conventions:** snake_case for tables and columns
- **Primary Keys:** BIGSERIAL `id` columns
- **Timestamps:** `created_at`, `updated_at`, `deleted_at` (soft delete)
- **Partitioning:** Monthly partitions on `weighings(weighed_at)` for high write/read performance
- **Vector Support:** pgvector extension enabled for natural language query embeddings
- **Materialized Views:** Pre-aggregated data for dashboards and reports

---

## Legal Computation Rules

### EAC Vehicle Load Control Act (2016) - Enforcement Regulations (2018)

**Axle Load Limits:**
- Single (steering): 8,000 kg
- Single (non-steering): 10,000 kg
- Tandem (2-axle unit): 16,000-24,000 kg (depending on tyre configuration)
- Tridem (3-axle unit): 22,500 kg

**GVW Limit:** 56,000 kg max for vehicles ≤ 7 axles

**Tolerance:**
- **Statutory:** 5% overload margin on axle limits
- **Operational (configurable):** ≤200 kg on GVW/axle for auto special release

**Charging Logic:**
1. Compute GVW overload fee using `eac_fee_bands_gvw`
2. Compute each axle overload fee using `eac_fee_bands_axle` (by axle type)
3. **Charge the higher** of GVW fee vs. maximum axle fee
4. Apply penalty multipliers: Overload 201–1500 kg (redistributable): 1× standard fee; Overload >1500 kg (non-redistributable): 5× standard fee
5. All fees in USD; convert to KES using daily `currencies.rate_to_usd`

### Kenya Traffic Act (Cap 403) - KeNHA Schedules

**GVW Limits:**
- 2-axle rigid: 18,000 kg
- 3-axle rigid: 26,000 kg
- 3-axle tractor + semi: 28,000 kg
- 4-axle rigid: 30,000 kg
- 4-axle tractor + semi: 36,000 kg
- 5-axle: 42,000-44,000 kg

**Tolerance:**
- **Axle:** 5% on permissible axle loads
- **GVW:** No statutory tolerance; configurable operational tolerance (≤200 kg) for auto release

**Charging Logic:**
1. Compute GVW overload only (axles recorded but not charged)
2. Lookup fee in `traffic_fee_bands_gvw` by overload band
3. Convert USD to KES (daily forex)

### Permit Vehicles (Extended Limits)

**Permit Rules:**
- 2A: +3,000 kg axle, +1,000 kg GVW
- 3A: +3,000 kg axle, +2,000 kg GVW

**Enforcement Workflow:**
1. Detect active permit via `permits` table
2. Apply permit-extended limits
3. If measured ≤ permit-extended limits: **Special Permit Release** (auto)
4. If beyond permit allowance: **Prohibit and Prosecute** (charge based on exceedance above base limits)

---

## Data Analytics Integration

### Overview

The TruLoad system integrates with a centralized Data Analytics (DA) platform using Apache Superset for BI dashboards, advanced analytics, and natural language query processing. The DA services are deployed as a centralized platform with VPA and HPA scaling based on demand and traffic.

### Natural Language Query Processing

**Architecture:**
- **Text-to-Vector Mapping:** ONNX Runtime with lightweight embedding model (e.g., `all-MiniLM-L12-v2`) for semantic search.
- **Generative AI (SLM):** Integration of a Small Language Model (e.g., Phi-3, Llama-3-8B-Quantized) via ONNX Runtime or local inference server for **Intent-to-SQL/JSON** translation.
- **Vector Database:** PostgreSQL with pgvector extension for efficient similarity search of *existing* dashboards and datasets.
- **Query Processing Flow:**
  1. User submits natural language query (e.g., "fetch trucks with repeated offenses over the past month. display in table format and a summary donut chart")
  2. **Semantic Search:** Backend uses embedding model to find relevant *existing* dashboards or datasets in `pgvector`.
  3. **Intent Translation:** If no exact dashboard exists, the SLM (Generative AI) parses the query to construct:
     - SQL Query (for data retrieval)
     - Superset Visualization Config (JSON)
  4. **Execution:**
     - SQL is executed against the read-replica.
     - Superset API is called to create a temporary "Ad-hoc" dashboard/chart.
  5. **Response:** Backend returns the embedded URL of the newly created (or found) dashboard.

**ONNX Runtime & AI Stack:**
- **Embedding Model:** `all-MiniLM-L12-v2` (Quantized) - Fast, low memory.
- **Generative Model (SLM):** `Phi-3-mini-4k-instruct` (ONNX) - Capable of basic SQL generation and JSON structuring.
- **Vector Database:** pgvector for storing embeddings of:
  - Dashboard Titles/Descriptions
  - Dataset Column Names/Descriptions
  - Report Metadata

### Apache Superset Integration

**Centralized DA Platform Architecture:**
- Apache Superset deployed as centralized service within `devops-k8s`.
- Service accessible to all BengoBox services via internal network.
- Scaling configured with HPA (Horizontal Pod Autoscaling) and VPA (Vertical Pod Autoscaling).
- Service URL configured via environment variables (`SUPERSET_BASE_URL`).

**Backend Integration Methods:**
- **REST API Client:** .NET HttpClient configured for Superset REST API calls.
- **Dynamic Dashboard Generation:** Backend uses the SLM's output to call Superset APIs (`POST /api/v1/dashboard/`, `POST /api/v1/chart/`) to create visualizations on-the-fly.
- **Guest Token Management:** Secure embedding via `POST /api/v1/security/guest_token/` with RLS (Row Level Security) clauses to ensure data isolation (e.g., Station A cannot see Station B's data).

**Integration Approach:**
1. **Initialization:** On application startup, backend syncs metadata (datasets, roles) to Superset.
2. **Ad-Hoc Query:**
   - User asks "Show me overload trends".
   - SLM determines "Line Chart" + "Group By Month" + "Sum(OverloadKg)".
   - Backend calls Superset API to create this chart.
   - Backend returns embedded URL.
3. **Pre-built Dashboards:** Standard dashboards are created during deployment and linked via the "Semantic Search" layer.

**Superset REST API Endpoints Used:**
- `/api/v1/security/login` - Authentication
- `/api/v1/database/` - Data source management
- `/api/v1/dashboard/` - Dashboard CRUD operations
- `/api/v1/chart/` - Chart CRUD operations
- `/api/v1/dataset/` - Dataset management
- `/api/v1/security/guest_token/` - Guest token generation for embedded dashboards

**Data Flow:**
- Natural language query → Frontend → Backend API
- Backend: Embedding Model → Vector Search (Find existing?)
- Backend: SLM → Intent Translation (Generate new?)
- Backend: Superset API → Create/Get Dashboard
- Backend: Embedded URL generation → Frontend → Superset SDK rendering

**Error Handling:**
- Retry logic for Superset API calls using Polly policies
- Circuit breaker pattern for Superset connectivity issues
- Fallback to static dashboards if dynamic creation fails
- Comprehensive logging of all Superset API interactions

### Vector Database Setup

**PostgreSQL pgvector Extension:**
- Enable pgvector extension: `CREATE EXTENSION IF NOT EXISTS vector;`
- Add vector columns to relevant tables for semantic search
- Create vector indexes using HNSW or IVFFlat indexes for efficient similarity search
- Vector columns store embeddings for searchable text fields (vehicle descriptions, violation reasons, case notes, etc.)

**Vector Indexes:**
- HNSW index for high-dimensional vectors (recommended for production)
- IVFFlat index for lower-dimensional or smaller datasets
- Index creation: `CREATE INDEX ON table_name USING hnsw (embedding_column vector_cosine_ops);`

### Centralized DA Platform

**Deployment Strategy:**
- Apache Superset deployed as centralized service within devops-k8s
- Service accessible to all BengoBox services
- Scaling configured with:
  - **HPA (Horizontal Pod Autoscaling):** Scale pods based on CPU/memory metrics
  - **VPA (Vertical Pod Autoscaling):** Adjust pod resource requests/limits based on usage patterns

**Service Integration:**
- All services in BengoBox folder can integrate with centralized DA platform
- Superset SDK used to bootstrap DA dashboards on service-specific frontends
- Shared dashboard templates for common analytics patterns

For detailed integration instructions, refer to [integration.md](./integration.md).

---

## Performance & Concurrency

### Indexing Strategy

**Hot Query Paths:**
- `weighings(vehicle_id, weighed_at DESC)` → vehicle history
- `weighings(station_id, weighed_at DESC)` → station activity
- `weighings(ticket_no)` → unique lookup
- Partial index on `weighings(is_sent_to_yard)` WHERE TRUE → yard list queries
- GIN index on JSONB columns if heavy filtering
- Vector indexes (HNSW/IVFFlat) on embedding columns for semantic search

### Partitioning
- Monthly partitions on `weighings(weighed_at)` → archive old partitions after retention period
- Detach and drop partitions older than 2 years (configurable)

### Caching (Redis)
**Cache Keys:**
- `axle:configs` → all axle configurations (refresh on update)
- `fee:bands:eac:gvw` → EAC GVW fee bands
- `fee:bands:traffic:gvw` → Traffic GVW fee bands
- `station:config:{stationId}` → station settings (cameras, I/O, defaults)
- `currency:usd:kes:latest` → latest forex rate
- `vector:embeddings:{text_hash}` → cached query embeddings

**Cache TTL:**
- Static reference data: 24 hours (sliding, bust on CRUD)
- Dynamic data (forex): 1 hour
- Vector embeddings: 7 days (query result caching)

### Concurrency & Locking
- **Idempotency:** Client generates `weighing_id` (UUID) directly; backend uses this as Primary Key to prevent duplicates
- **Advisory Locks:** During reweigh cycles, acquire PostgreSQL advisory lock on `vehicle_id` to prevent simultaneous reweighs
- **Optimistic Concurrency:** EF Core row versioning (timestamp) on `weighings`, `prosecution_cases`

---

## Integrations

### Auth-Service (SSO)
- Validate JWTs against auth-service JWKS (cache keys, refresh on kid rotation).
- Keep `auth_service_user_id` on local `users`; never duplicate identity fields.
- Proxy login/refresh via backend (`/api/v1/auth/*`) to auth-service with retries and backoff.
- Background user sync job to reconcile roles/status; emit `truload.user.synced.v1` via outbox.
- Deny access when auth-service reports deactivation; cache last-known claims to allow offline verification for short windows.

### Notifications-Service
- Send user-facing alerts (payments, yard actions, device health) via notifications-service REST API with idempotency keys.
- Prefer templated notifications; include station/tenant context in payload.
- Record notification requests/responses for audit; retry via Polly with DLQ fallback.

### TruConnect Microservice
- **Type:** Node.js/Electron service running on client machine
- **Function:** Connects to scale indicator via Serial/RF/Ethernet, reads weights, exposes HTTP endpoint
- **Backend Handling:** Poll or SignalR stream from TruConnect; validate and store in `weight_stream_events`; lock stable values into `weighing_axles`

### eCitizen / Road Authority Payment Gateway
- **Invoice Creation:** POST to external API with case details, amount
- **Webhook Callback:** Receive payment confirmation → update `invoices.status`, insert `receipts`
- **Retry Logic:** Polly retry policy (3 attempts, exponential backoff)

### Case Management System (NTAC/OB)
- **NTAC Assignment:** Auto-generate or manual entry
- **OB Integration:** API call to police system (if available) or manual OB number entry
- **Data Exchange:** JSON over HTTPS with mutual TLS

### Apache Superset (DA Platform)
- **Connection:** Direct PostgreSQL read-only connection + REST API for dashboard management
- **Authentication:** SSO via JWT tokens from backend
- **Dashboard Generation:** Programmatic dashboard creation via Superset SDK

For detailed integration instructions, refer to [integration.md](./integration.md).

---

## Security & Compliance

### Authentication & Authorization

**Centralized SSO Integration:**
- TruLoad services utilize centralized `auth-service` for authentication
- Authentication requests routed to `auth-service` via HTTP client
- JWT tokens validated against `auth-service` public keys
- Token refresh handled via `auth-service` refresh endpoint
- Single Sign-On (SSO) enables seamless authentication across BengoBox services

**Application-Level User Management:**
- Local user entities maintained for app-specific data (shifts, station assignments, preferences)
- User synchronization with `auth-service` via background jobs
- Sync strategy: One-way sync from `auth-service` for user identity, two-way sync for app-specific attributes
- RBAC roles and permissions managed locally but synchronized with `auth-service` where applicable
- Avoid duplicate user entities by maintaining `auth_service_user_id` foreign key reference

**User Entity Relationship:**
- `users` table includes `auth_service_user_id` (UUID) referencing centralized auth service
- Local user management includes: shifts, station assignments, role mappings, app preferences
- Sync jobs run periodically to reconcile user data between services
- User creation/deactivation events published to auth-service for cross-service synchronization

**JWT Token Handling:**
- Access tokens from `auth-service` validated on each request
- Token claims include: user ID, email, roles, station ID (if assigned)
- Token refresh via `auth-service` refresh endpoint before expiry
- Token caching in Redis to reduce validation overhead

**RBAC Policies:**
- Application-specific policies: `CanProsecute`, `CanReleaseVehicle`, `CanManageStation`, etc.
- Policy definitions stored locally but respect centralized role hierarchy
- Permission checks combine centralized roles with local app permissions

### Data Encryption
- **At Rest:** PostgreSQL transparent data encryption (TDE) or disk-level encryption
- **In Transit:** TLS 1.3 for all API calls
- **Sensitive Fields:** Encrypt camera passwords, PLC credentials using AES-256 (store key in K8s secret)

### Audit Trail
- **Middleware:** Intercept all POST/PUT/DELETE requests → log to `audit_logs`
- **Immutable Logs:** Append-only; never delete (retention: 7 years per compliance)

### Backup & Restore
- **Database:** Daily PostgreSQL backups via CronJob (pg_dump)
- **Media/Docs:** S3-compatible storage (MinIO or AWS S3) with versioning

---

## DevOps & Deployment

### Docker Build
- Multi-stage Dockerfile:
  - Stage 1: `mcr.microsoft.com/dotnet/sdk:8.0` → restore, build, publish
  - Stage 2: `mcr.microsoft.com/dotnet/aspnet:8.0` → runtime
- Health endpoint: `/health` (returns 200 OK if DB connectable, Redis pingable)

### Kubernetes Manifests (via centralized devops-k8s)
- ArgoCD app: `devops-k8s/apps/truload-backend/app.yaml`
- Helm values: `devops-k8s/apps/truload-backend/values.yaml`
- Chart: `devops-k8s/charts/app` (shared template)

### CI/CD Pipeline (GitHub Actions)
- **Trigger:** Push to `main` branch under `TruLoad/truload-backend/**`
- **Steps:**
  1. Checkout code
  2. Install devops tools (via central action)
  3. Run `build.sh` (Trivy scan, Docker build/push, K8s apply, Helm values update)
  4. Tag `:latest` image
  5. Health check via ingress

### Observability
- **Logs:** Serilog → Seq or ELK
- **Metrics:** Prometheus scrape `/metrics` endpoint (app.UsePrometheusMetrics)
- **Traces:** OpenTelemetry → Jaeger
- **Dashboards:** Grafana with pre-built panels (request rate, error rate, DB query duration)

---

## Sprint Delivery Plan

For detailed sprint tasks and deliverables, refer to the [sprints](./sprints/) folder.

**Sprint Overview:**
- **Sprint 1:** User Management & Security (Weeks 1-2) — complete auth-service SSO, user/shift, audit foundation first.
- **Sprint 1.5 (Current):** Axle System Foundation — implement simplified axle configuration system with reference tables and seeding
  - [ ] Create models: `TyreType`, `AxleGroup`, `AxleConfiguration` (unified, is_standard flag), `AxleWeightReference`, `AxleLoadLimit`, `AxleFeeSchedule`
  - [ ] Update `DbContext` with entity configurations, indexes, relationships
  - [ ] Generate EF Core migration `AddAxleSystem`
  - [ ] Implement modular `WeighingOperationsSeeder` (manual CSV parsing, no CsvHelper dependency)
    - Seed 3 tyre types (S, D, W)
    - Seed 10 axle groups (S1, SA4, SA6, TAG8, TAG8B, TAG12, QAG16, WWW, SSS, S4)
    - Parse and seed ~612 standard axle configurations from `AXLECONFIG_DATA.csv` (is_standard=TRUE)
    - Parse and seed ~1233 axle weight references from CSV
    - Seed axle fee schedules for EAC and Traffic Act frameworks
  - [ ] Implement modular validation service for axle compliance checks (EAC vs Traffic Act tolerance)
  - [ ] Test seeding idempotency and relationship integrity
- **Sprint 2:** Data Analytics (ONNX, Vector DB, Superset) (Weeks 3-4) — deliver Superset guest-token API and embed readiness before heavier modules.
- **Sprint 3:** Weighing Setup (Weeks 5-6)
- **Sprint 4:** Weighing Core (Weeks 7-8)
- **Sprint 5:** Yard & Tags (Weeks 9-10)
- **Sprint 6:** Case Register & Special Release (Weeks 11-12)
- **Sprint 7:** Prosecution EAC (Weeks 13-14)
- **Sprint 8:** Prosecution Traffic & Court Escalation (Weeks 15-16)
- **Sprint 9:** Case Management (Weeks 17-18)
- **Sprint 10:** Inspection (Week 19)
- **Sprint 11:** Reporting & Analytics Integration (Weeks 20-21)
- **Sprint 12:** Settings & Technical (Week 22)
- **Sprint 13:** Polish & Testing (Week 23-24)

### Axle System Design (Sprint 1.5)

**Key Principle:** Single unified `axle_configurations` table (not separate `derived_axle_configurations`).

**Axle Configuration Types:**
- **Standard (is_standard=TRUE):** EAC-defined immutable patterns (e.g., "2*", "3A", "4B", "5C")
  - Simple codes, created during seeding, created_by_user_id = NULL
  - Cannot be modified or deleted, always available for weighing
- **Derived (is_standard=FALSE):** User-created custom patterns (e.g., "5*S|DD|DD|", "3*S|DW||")
  - Complex pipe notation encoding tyre types per position
  - created_by_user_id tracks creator, guided frontend validation ensures compliance
  - Users can create derived configs from frontend with compliance checks enforced on backend

**Data Flow:**
1. **CSV Parsing:** `WeighingOperationsSeeder` reads `AXLECONFIG_DATA.csv` (~898 rows)
   - Splits into AXLEWEIGHTCONFIG section (~612 standard configs) → insert as is_standard=TRUE
   - Splits into AXLEWEIGHTREFF section (~1233 references) → insert into axle_weight_references
   - Manual parsing (no CsvHelper dependency to avoid build errors)

2. **Reference Tables:** Seed tyre_types (3), axle_groups (10), and axle_fee_schedules per legal framework

3. **Weighing Operations:** When weighing a vehicle:
   - Lookup axle_configuration by vehicle_class axle_pattern
   - Load axle_weight_references for that configuration
   - Apply legal framework (EAC vs Traffic Act) via axle_fee_schedules
   - Calculate compliance status using tolerance settings

**Validation Rules:**
- Standard configs: Use EAC tolerance (5%) for compliance checks
- Derived configs: Validate user input against vehicle class constraints before creation
- Fee Calculation: max(GVW_fee, sum(per_axle_fees)) from applicable fee schedule
- Demerit Points: Track separately for prosecution decisions

Each sprint document in the [sprints](./sprints/) folder contains:
- Detailed task breakdown with checkboxes
- Acceptance criteria
- Dependencies
- Estimated effort

---

## Password Hashing & Cross-Service Compatibility

### Overview

**Implemented:** December 10, 2025

All BengoBox services now use **Argon2id password hashing** with **identical parameters** to ensure password hashes can be verified across services. This is critical for bidirectional user sync between TruLoad backend and the centralized auth-service.

### Hash Format Specification

```
$argon2id$v=19$m=65536,t=3,p=2$<base64-salt>$<base64-hash>
```

**Parameters:**
- **Algorithm**: Argon2id (memory-hard, side-channel resistant)
- **Version**: 19 (Argon2 v1.3)
- **Memory (m)**: 65536 KB (64 MiB)
- **Iterations (t)**: 3 (time cost)
- **Parallelism (p)**: 2 threads
- **Key Length**: 32 bytes
- **Salt Length**: 16 bytes (cryptographically random)
- **Encoding**: Base64 without padding (RFC 4648 base64url)

### Implementation

#### Go Services (Auth-Service, Ordering, Notifications)

```go
import passwordhasher "github.com/Bengo-Hub/shared-password-hasher"

hasher := passwordhasher.NewHasher()
hash, _ := hasher.Hash("ChangeMe123!")
```

#### .NET Services (TruLoad Backend)

```csharp
using TruLoad.Backend.Infrastructure.Security;

var hasher = new PasswordHasher();
string hash = hasher.HashPassword("ChangeMe123!");
```

**Location**: `Infrastructure/Security/PasswordHasher.cs`

### Bidirectional User Sync Pattern

#### Scenario 1: User Exists Locally but NOT in Auth-Service

1. TruLoad backend checks local database → user found
2. Queries auth-service → user not found
3. **Sync TO auth-service:** Hash password, create tenant, create user with pre-hashed password
4. Proxy login to auth-service
5. Return JWT token

#### Scenario 2: User Exists in Auth-Service but NOT Locally

1. Proxy login to auth-service → success, JWT returned
2. **Sync FROM auth-service:** Parse JWT, create/get organization, create local user, assign role
3. Return JWT token

See `docs/PASSWORD_HASHING_IMPLEMENTATION.md` for complete implementation details.

---

## References

- [Database Schema (ERD)](./erd.md) - Updated with Permission and RolePermission entities
- [RBAC Implementation Plan](./RBAC_IMPLEMENTATION_PLAN.md) - **NEW:** Complete 5-phase roadmap with production auth-service integration and 77-permission model
- [Password Hashing Implementation](./PASSWORD_HASHING_IMPLEMENTATION.md) - **NEW:** Argon2id standardization and bidirectional sync
- [Integration Guide](./integration.md)
- [Sprint Plans](./sprints/)
  - [Sprint 1: User Management & Security](./sprints/sprint-01-user-management-security.md) - Updated with Permission Model (Phase 1) and Authorization Policies (Phase 2)
- [FRD Document](../../resources/Master%20FRD%20KURAWEIGH.docx.md)
