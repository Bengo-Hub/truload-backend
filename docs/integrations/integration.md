# TruLoad Backend - Integration Guide

## Overview

This document provides detailed integration information for external services and systems integrated with the TruLoad backend, including Apache Superset, Vector Database (pgvector), ONNX Runtime, notifications-service, and other third-party services.

---

## Table of Contents

1. [Notifications Service Integration](#notifications-service-integration)
2. [Apache Superset Integration](#apache-superset-integration)
3. [Vector Database (pgvector) Integration](#vector-database-pgvector-integration)
4. [ONNX Runtime Integration](#onnx-runtime-integration)
5. [TruConnect Microservice Integration](#truconnect-microservice-integration)
6. [eCitizen Payment Gateway Integration](#ecitizen-payment-gateway-integration)
7. [Third-Party API Integrations](#third-party-api-integrations)

---

## Notifications Service Integration

### Overview

TruLoad backend integrates with the centralized `notifications-service` for managing all notification delivery (email, SMS, push notifications). All notification requests are sent to notifications-service via REST API.

### Architecture

**Service Configuration:**
- Notifications service base URL: `NOTIFICATIONS_SERVICE_URL` (environment variable)
- Default: `http://notifications-service.notifications.svc.cluster.local:8080`
- Retry policy: Polly with exponential backoff (3 retries)
- Circuit breaker: Opens after 5 consecutive failures
- Timeout: 10 seconds

**Communication:**
- HTTP POST requests to `/api/v1/notifications/send`
- Payload includes: recipient, template, data, channel (email/sms/push)
- Async fire-and-forget pattern for non-critical notifications
- Synchronous with retry for critical notifications (password reset, case escalation)

### Implementation Details

**Notification Types:**
- User registration confirmation
- Password reset requests
- Case assignment notifications
- Court hearing reminders
- System alerts (scale calibration expired, etc.)

**API Endpoints:**

**POST /api/v1/notifications/send**
```json
{
  "recipient": "user@example.com",
  "template": "password-reset",
  "channel": "email",
  "data": {
    "resetLink": "https://truload.app/reset?token=...",
    "expiresIn": "1 hour"
  }
}
```

**Error Handling:**
- Notifications-service unavailability: Log error, queue for retry
- Invalid template: Return 400 Bad Request
- Delivery failure: Notifications-service handles retry logic

---

## Apache Superset Integration

### Overview

TruLoad backend integrates with the centralized Apache Superset instance for BI dashboards, analytics, and natural language query processing. Superset is deployed as a centralized service accessible to all BengoBox services.

### Architecture

**Service Configuration:**
- Superset base URL: `SUPERSET_BASE_URL` (environment variable)
- Superset admin username: `SUPERSET_ADMIN_USERNAME` (K8s secret)
- Superset admin password: `SUPERSET_ADMIN_PASSWORD` (K8s secret)
- Superset API version: v1

**Authentication:**
- Admin credentials used for backend-to-Superset communication
- User authentication via JWT tokens from TruLoad backend
- Guest tokens generated for embedded dashboards

### Integration Methods

**1. REST API Client**

Backend uses .NET `HttpClient` configured for Superset REST API calls.

**Base Configuration:**
- Base URL: `SUPERSET_BASE_URL/api/v1`
- Default headers: Content-Type: application/json
- Authentication: Bearer token from Superset login endpoint
- Retry policy: Polly with exponential backoff (3 retries)
- Circuit breaker: Opens after 5 consecutive failures

**Key API Endpoints:**

**Authentication:**
- `POST /api/v1/security/login` - Login with admin credentials
- `POST /api/v1/security/refresh` - Refresh access token
- `POST /api/v1/security/guest_token/` - Generate guest token for embedding

**Data Sources:**
- `GET /api/v1/database/` - List all data sources
- `POST /api/v1/database/` - Create new data source
- `PUT /api/v1/database/{id}` - Update data source
- `DELETE /api/v1/database/{id}` - Delete data source

**Dashboards:**
- `GET /api/v1/dashboard/` - List all dashboards
- `POST /api/v1/dashboard/` - Create new dashboard
- `PUT /api/v1/dashboard/{id}` - Update dashboard
- `GET /api/v1/dashboard/{id}` - Get dashboard details
- `POST /api/v1/dashboard/{id}/copy` - Copy dashboard

**Charts:**
- `GET /api/v1/chart/` - List all charts
- `POST /api/v1/chart/` - Create new chart
- `PUT /api/v1/chart/{id}` - Update chart
- `GET /api/v1/chart/{id}` - Get chart details

**Datasets:**
- `GET /api/v1/dataset/` - List all datasets
- `POST /api/v1/dataset/` - Create new dataset
- `PUT /api/v1/dataset/{id}` - Update dataset

**2. Database Direct Connection**

Superset connects directly to PostgreSQL database via read-only user for data access.

**Connection Configuration:**
- Database type: PostgreSQL
- Connection string: Provided to Superset via data source API
- Read-only user: `superset_readonly` (created in PostgreSQL)
- Permissions: SELECT only on all tables, no write access
- SSL: Required for production connections

**Data Source Creation:**
- Data source created programmatically on application startup
- Connection tested before marking as active
- Data source updated if connection parameters change

**3. Natural Language Query Processing**

Backend processes natural language queries, generates visualizations, and creates Superset dashboards.

**Query Processing Flow:**
1. User submits natural language query via frontend
2. Backend processes query through ONNX Runtime to generate vector embedding
3. Vector embedding used for semantic search in PostgreSQL (pgvector)
4. Query intent parsed to extract:
   - Data retrieval criteria (filters, date ranges, aggregations)
   - Display format requirements (table, chart type, visualization)
5. SQL query constructed based on semantic matches and intent
6. Data retrieved from PostgreSQL
7. Visualization configuration created for Superset
8. Superset chart/dashboard created or updated via REST API
9. Embedded URL generated and returned to frontend

**Query Examples:**
- "fetch trucks with repeated offenses over the past month. display in table format and a summary donut chart"
- "show me overload statistics by station for the last quarter"
- "display GVW violations grouped by vehicle type as a bar chart"

**Dashboard Generation:**
- Dashboards created programmatically using Superset REST API
- Dashboard-to-module mapping maintained in local database
- Dashboards updated when underlying data changes
- Dashboard templates reused for consistent visualization patterns

**Embedded Dashboard URLs:**
- Guest tokens generated for each dashboard view
- URLs include dashboard ID, filters, and time range
- URLs are temporary and expire after configured duration
- Frontend renders dashboards using Superset SDK iframe

### Implementation Details

**Initialization:**
- On application startup:
  1. Authenticate with Superset using admin credentials
  2. Create/update data sources pointing to PostgreSQL
  3. Create/update dashboard definitions for each module
  4. Maintain dashboard-to-module mapping in local database

**Dashboard Bootstrap:**
- Frontend requests dashboard URL from backend
- Backend generates secure embedded URL with user authentication token
- URL includes dashboard ID, filters, and time range
- Frontend renders dashboard using Superset SDK iframe

**Error Handling:**
- Retry logic for Superset API calls using Polly policies
- Circuit breaker pattern for Superset connectivity issues
- Fallback to static dashboards if dynamic creation fails
- Comprehensive logging of all Superset API interactions

**Monitoring:**
- Track Superset API call latency
- Monitor dashboard creation/update failures
- Alert on Superset service unavailability

---

## Vector Database (pgvector) Integration

### Overview

PostgreSQL database enhanced with pgvector extension for efficient storage and similarity search of high-dimensional vector embeddings generated from text queries and entity descriptions.

### Architecture

**Extension Configuration:**
- Extension: `pgvector` (version 0.5.0 or later)
- Vector data type: `vector` (configurable dimensions)
- Default dimensions: 384 (for all-MiniLM-L12-v2 model)

**Vector Columns:**
- Vector columns added to relevant tables for semantic search
- Embeddings generated server-side using ONNX Runtime
- Embeddings updated on text field changes
- Background jobs refresh embeddings periodically

### Implementation Details

**Database Setup:**
1. Enable pgvector extension: `CREATE EXTENSION IF NOT EXISTS vector;`
2. Add vector columns to relevant tables:
   - `vehicles.description_embedding` VECTOR(384)
   - `weighings.violation_reason_embedding` VECTOR(384)
   - `prosecution_cases.case_notes_embedding` VECTOR(384)
   - `vehicle_tags.reason_embedding` VECTOR(384)
3. Create vector indexes using HNSW (Hierarchical Navigable Small World) algorithm

**Index Creation:**
- HNSW index for high-dimensional vectors (recommended for production)
- IVFFlat index for lower-dimensional or smaller datasets
- Cosine similarity operator (`vector_cosine_ops`) for similarity search
- Example: `CREATE INDEX ON vehicles USING hnsw (description_embedding vector_cosine_ops);`

**Vector Embedding Generation:**
- Embeddings generated server-side using ONNX Runtime
- Model: all-MiniLM-L12-v2 (384 dimensions)
- Embeddings updated on text field changes
- Background jobs refresh embeddings periodically

**Similarity Search Queries:**
- Cosine similarity: `1 - (embedding <=> query_embedding)`
- Distance threshold: Configurable (e.g., < 0.8 for high similarity)
- Example query:
  ```sql
  SELECT * FROM vehicles
  WHERE description_embedding <=> query_embedding < 0.2
  ORDER BY description_embedding <=> query_embedding
  LIMIT 10;
  ```

**Performance Optimization:**
- Vector indexes (HNSW) for efficient similarity search
- Caching of frequently used query embeddings in Redis
- Batch processing of embedding updates
- Parallel similarity searches where applicable

### Maintenance

**Embedding Updates:**
- On text field changes: Update embedding immediately
- Periodic refresh: Background job runs daily to refresh all embeddings
- Batch updates: Process multiple embeddings in parallel

**Index Maintenance:**
- HNSW indexes are self-maintaining
- Reindex if index size grows beyond recommendations
- Monitor index size and query performance

---

## ONNX Runtime Integration

### Overview

ONNX Runtime is used to generate vector embeddings from natural language queries and entity descriptions. The selected model runs locally on the deployment server for low latency and privacy.

### Architecture

**Model Selection:**
- Model: all-MiniLM-L12-v2 (Microsoft)
- Dimensions: 384
- License: Apache 2.0
- Size: ~80MB
- Performance: Optimized for local deployment, low latency

**Alternative Models (if needed):**
- all-MiniLM-L6-v2 (smaller, faster, 384 dimensions)
- paraphrase-MiniLM-L6-v2 (optimized for semantic similarity, 384 dimensions)

### Implementation Details

**Library:**
- NuGet package: `Microsoft.ML.OnnxRuntime` (version 1.16.0 or later)
- Runtime: In-process (no external service required)
- Thread-safe: Model can be shared across requests

**Model Loading:**
- Model file loaded on application startup
- Model stored in `Models/` directory or embedded resource
- Model cached in memory for performance
- Model reloaded on file change (hot reload support)

**Text-to-Vector Processing:**
1. Input text preprocessed (normalization, tokenization)
2. Text passed to ONNX model
3. Model returns vector embedding (384 dimensions)
4. Embedding normalized (L2 normalization) if required
5. Embedding stored/used for similarity search

**Performance Optimization:**
- Model loaded once at startup
- Batch processing for multiple texts
- Caching of embeddings in Redis (7-day TTL)
- Parallel processing for large batches

**Error Handling:**
- Model loading failures: Application fails to start
- Inference failures: Log error, return null embedding
- Retry logic for transient failures

### Usage Examples

**Single Text Embedding:**
```csharp
// Pseudo-code
var embeddingService = new EmbeddingService();
var text = "fetch trucks with repeated offenses";
var embedding = await embeddingService.GenerateEmbeddingAsync(text);
// Returns: float[] of 384 dimensions
```

**Batch Text Embedding:**
```csharp
var texts = new[] { "text1", "text2", "text3" };
var embeddings = await embeddingService.GenerateEmbeddingsAsync(texts);
// Returns: List<float[]> of 384-dimension vectors
```

**Similarity Search:**
```csharp
var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query);
var similarEntities = await dbContext.Vehicles
    .Where(v => v.DescriptionEmbedding.CosineDistance(queryEmbedding) < 0.2)
    .OrderBy(v => v.DescriptionEmbedding.CosineDistance(queryEmbedding))
    .Take(10)
    .ToListAsync();
```

---

## TruConnect Microservice Integration

### Overview

TruConnect is a Node.js/Electron middleware running on client machines that connects to scale indicators (ZM, Cardinal, Cardinal2, 1310, Haenni, PAW, **MCGS mobile scale**, etc.) and provides weight data to TruLoad via real-time WebSocket or HTTP polling.

### Connection Modes (Updated Sprint 22.1)

The frontend always connects directly to the local TruConnect middleware. There is no backend WebSocket relay.

**1. Real-time WebSocket (DEFAULT)**
- TruConnect runs WebSocket server on port 3030
- TruLoad frontend connects as WebSocket client (ws://localhost:3030)
- Two-way communication enabled
- Weights pushed in real-time (<50ms latency)
- Recommended for production use
- Connection is always local — no backend relay

**2. API Polling (FALLBACK)**
- Enabled when WebSocket is unavailable
- TruConnect exposes HTTP API on port 3031
- Polling interval: 500ms (configurable)
- Use when WebSocket blocked by firewall or browser restrictions

### Two-Way Communication Protocol

TruConnect implements bidirectional WebSocket communication:

**TruLoad → TruConnect Messages:**

```typescript
// Registration (sent on connect)
{ type: 'register', stationCode: 'ROMIA', bound: 'A', clientType: 'truload' }

// Vehicle plate (from ANPR or manual entry)
{ type: 'plate', plate: 'KAA 123X', source: 'anpr' | 'manual' }

// Bound switch (bidirectional stations)
{ type: 'bound-switch', newBound: 'B', newStationCode: 'ROKSA' }

// Status request
{ type: 'status-request' }
```

**TruConnect → TruLoad Messages:**

```typescript
// Weight update (continuous stream)
{
  type: 'weights',
  mode: 'multideck' | 'mobile',
  stationCode: 'ROMIA',
  bound: 'A',
  decks: [{ index: 1, weight: 6500, stable: true }, ...],
  gvw: 31600,
  status: 'stable',
  vehicleOnDeck: true,
  timestamp: '2026-01-28T10:00:00Z'
}

// Connection status
{
  type: 'status',
  connected: true,
  simulation: false,
  indicators: [{ id: 'ZM1', type: 'ZM', connected: true }]
}

// Registration acknowledgment
{ type: 'ack', registered: true, stationCode: 'ROMIA', bound: 'A' }
```

### Station Code & Bound Architecture

TruLoad supports complex weighbridge naming conventions:

**1. Simple Bidirectional (Single deck per bound)**
```
Station: Rongo Weighbridge
Base Code: ROMI
Bound A: ROMIA (Nairobi bound)
Bound B: ROKSA (Kisumu bound)
Pattern: Last letter indicates bound
```

**2. Multi-Deck Per Bound (Athi River style)**
```
Station: Athi River Mombasa Bound
Station Code: ATMB
Deck A: ATMBA (First deck)
Deck B: ATMBB (Second deck)
Pattern: [STATION_CODE][DECK_LETTER]
```

**3. Non-Directional (Single bound)**
```
Station: Webuye
Code: WBMLA
No bound switching
```

### Connection Flow

1. TruLoad opens WebSocket to TruConnect (ws://localhost:8080)
2. TruLoad sends `register` with station code and current bound
3. TruConnect acknowledges with `ack`
4. TruConnect streams weights continuously
5. When plate captured, TruLoad sends `plate` message
6. TruConnect uses plate for auto-weigh data
7. On bound change, TruLoad sends `bound-switch`
8. TruConnect updates station code in subsequent messages

### Weight Data Formats

**Multideck Mode (Static Weighbridge)**
```json
{
  "mode": "multideck",
  "stationCode": "ROMIA",
  "bound": "A",
  "decks": [
    { "index": 1, "weight": 6500, "stable": true },
    { "index": 2, "weight": 8200, "stable": true },
    { "index": 3, "weight": 9100, "stable": true },
    { "index": 4, "weight": 7800, "stable": true }
  ],
  "gvw": 31600,
  "status": "stable",
  "vehicleOnDeck": true,
  "timestamp": "2026-01-28T10:00:00Z"
}
```

**Mobile Mode (Axle-by-Axle / Haenni / PAW / MCGS)**
```json
{
  "mode": "mobile",
  "stationCode": "WBMLA",
  "currentAxle": 2,
  "totalAxles": 5,
  "axles": [
    { "axle": 1, "weight": 6500, "captured": true },
    { "axle": 2, "weight": 8200, "captured": false, "live": true }
  ],
  "gvw": 14700,
  "timestamp": "2026-01-28T10:00:00Z"
}
```

> **Normalization Guarantee:** All supported mobile scales (Haenni API, PAW serial/UDP, and MCGS serial frames `=SG+0000123kR`) are normalized by TruConnect into this unified mobile payload structure before reaching the backend. Backend code (`AutoweighCaptureRequest` and `WeighingService.ProcessAutoweighAsync`) remains protocol-agnostic and does not need to know which physical device produced the weights.

### Integration Points

- Frontend connects to TruConnect via WebSocket (client-side)
- Frontend sends finalized weighing to backend via REST API
- Backend stores weighing in `weighings` table
- Backend stores axle details in `weighing_axles` table
- Backend validates against EAC/Traffic Act rules

### Error Handling

- **TruConnect unavailable**: Frontend shows "Scales Offline" indicator
- **WebSocket disconnect**: Auto-reconnect with exponential backoff
- **Invalid data**: Log error, skip message, continue
- **Simulation mode**: TruConnect can run without physical scales

---

## eCitizen Payment Gateway Integration

### Overview

Integration with eCitizen/Pesaflow payment gateway for invoice creation, payment processing, and receipt confirmation. **Fully implemented (February 2026).**

### Architecture

**Credential Storage (IntegrationConfig Table):**
- All credentials stored encrypted in `integration_configs` table
- Encryption: AES-GCM (256-bit key from K8s secret)
- Provider: `ecitizen_pesaflow`
- Fields: base_url, callback_url, webhook_url, encrypted_credentials, endpoints_json
- Service: `IntegrationConfigService` with `IEncryptionService` (AES-GCM)

**Key Files:**
- `Services/Implementations/Financial/ECitizenService.cs` — Push invoice, initiate checkout
- `Controllers/Financial/ECitizenWebhookController.cs` — IPN webhook handler
- `Controllers/Financial/PaymentController.cs` — Payment-related endpoints
- `Models/System/IntegrationConfig.cs` — Encrypted credential model
- `Infrastructure/Security/AesGcmEncryptionService.cs` — AES-GCM encryption
- `Configuration/ECitizenOptions.cs` — Configuration options
- `Data/Seeders/SystemConfiguration/IntegrationConfigSeeder.cs` — Seeds test config

### Implementation Details

**Push Invoice to Pesaflow (Create Invoice API):**
- Endpoint: `POST /api/v1/invoices/{invoiceId}/pesaflow`
- Service: `ECitizenService.CreatePesaflowInvoiceAsync()`
- Pesaflow API: `POST {BaseUrl}/api/invoice/create` (JSON body, Bearer token auth)
- Flow: Decrypt credentials → Get OAuth token → Build JSON payload with `account_id`, `client_invoice_ref`, `items[]`, etc. → Store `PesaflowInvoiceNumber` on invoice
- Required body fields: `account_id`, `amount_expected`, `amount_net`, `client_invoice_ref`, `currency`, `items[]`, `name`, `notification_url`, `callback_url`
- No `secure_hash` needed (Bearer token auth is used instead)

**Online Checkout (Iframe/STK):**
- Endpoint: `POST /api/v1/invoices/{invoiceId}/checkout`
- Service: `ECitizenService.InitiateCheckoutAsync()`
- Pesaflow API: `POST {BaseUrl}/PaymentAPI/iframev2.1.php` (form-encoded, with `secureHash`)
- Returns iframe HTML or redirect URL for immediate payment

**IPN Webhook (Payment Confirmation):**
- Endpoint: `POST /api/v1/payments/webhook/ecitizen-pesaflow`
- Signature verification: HMAC-SHA256 against `token_hash` (mandatory - unsigned webhooks rejected)
- Idempotency: `TransactionReference` (payment_reference) prevents duplicate receipts
- On success: Update invoice status → Create receipt → Auto-create LoadCorrectionMemo

**Manual Payment Recording:**
- Endpoint: `POST /api/v1/invoices/{invoiceId}/payments`
- Service: `ReceiptService.RecordPaymentAsync()`
- When invoice fully paid:
  1. Invoice.Status = "paid"
  2. ProsecutionCase.Status = "paid"
  3. **LoadCorrectionMemo auto-created** (new auto-trigger)

### Auto-Triggers from Payment

**Payment → LoadCorrectionMemo (ReceiptService):**
- When invoice status changes to "paid"
- Traces: Invoice → ProsecutionCase → CaseRegister → WeighingTransaction
- Creates LoadCorrectionMemo with overload details, issued by payment officer
- Memo number format: `LCM-{YEAR}-{SEQUENCE}`
- Fail-safe: try-catch, logged but doesn't block payment

**Reweigh → Memo Update (WeighingService):**
- When reweigh initiated: Links reweighWeighingId to existing memo
- Relief truck info stored on memo (reliefTruckRegNumber, reliefTruckEmptyWeightKg)

**Compliant Reweigh → Case Close (WeighingService):**
- When reweigh is compliant:
  1. Case closed with rich narration (Invoice/Receipt/Fine details)
  2. Yard entry auto-released
  3. Memo.ComplianceAchieved = true
  4. ComplianceCertificate auto-generated (linked to memo)

### Error Handling
- Pesaflow API down: Graceful skip with 400 response (invoice remains pending)
- Webhook delivery failure: Manual reconciliation via admin
- Duplicate payments: Idempotency key prevents double-processing
- Payment failure: Receipt not created, invoice stays pending

---

## Third-Party API Integrations

### NTSA Vehicle Search API (Implemented February 2026)

**Purpose:** Vehicle registration lookup, owner details, inspection status, and caveat info from NTSA database. Integrated into case register workflows.

**Architecture:**
- **Service:** `NTSAService` (`Services/Implementations/Integration/NTSAService.cs`, 344 lines)
- **Interface:** `INTSAService` (`Services/Interfaces/Integration/INTSAService.cs`)
- **DTOs:** `NTSAVehicleSearchResult` (`DTOs/Integration/IntegrationDtos.cs`)
- **Controller:** `ExternalIntegrationController` (`Controllers/Integration/ExternalIntegrationController.cs`)
- **DI Registration:** `Program.cs` — `AddHttpClient<INTSAService, NTSAService>(c => c.Timeout = 30s)`

**Credential Storage (IntegrationConfig Table):**
- Provider: `ntsa`
- Credentials encrypted via AES-256-GCM
- Seeded as **inactive** in development (awaiting live NTSA API credentials)
- Activated via admin API: `PUT /api/v1/system/integrations/ntsa`

**API Pattern (KenLoad V2 style):**
- POST to NTSA API with `{ "regno": "KAA123X" }` body
- Bearer token authentication
- Results cached in Redis (24-hour TTL per vehicle)
- Supports both POST (default) and GET template URL formats

**TruLoad API Endpoints:**
- `GET /api/v1/integration/ntsa/vehicle/{regNo}` - Search vehicle (requires `case.read` permission)
- `GET /api/v1/integration/health` - Health check for all integrations (requires `config.read`)

**Response Fields:**
- Owner: first/last name, type, address, town, phone
- Vehicle: chassis no, make, model, body type, year, registration date, logbook
- Inspection: center, date, expiry, status
- Caveat: reason, status, type

**Error Handling:**
- API unavailability: Returns 503 with message, graceful degradation
- Redis cache miss: Falls through to API call
- Invalid/error response: Marks `Found = false`, logs warning
- Timeout: 30-second HttpClient timeout, returns null

**Configuration (appsettings):**
```json
"NTSA": {
  "BaseUrl": "https://api.ntsa.go.ke",
  "Endpoints": {
    "VehicleSearch": "/vsearch/sp/qregno",
    "VehicleDetails": "/api/vehicle/details?reg_no={reg_no}&api_key={api_key}"
  },
  "ApiKey": "AWAITING_LIVE_CREDENTIALS"
}
```

---

### KeNHA Vehicle Tag Verification API (Implemented February 2026)

**Purpose:** Verify if a vehicle has an existing KeNHA tag/prohibition. Triggered automatically during weighing capture when KeNHA integration is active.

**Architecture:**
- **Service:** `KeNHAService` (`Services/Implementations/Integration/KeNHAService.cs`, 294 lines)
- **Interface:** `IKeNHAService` (`Services/Interfaces/Integration/IKeNHAService.cs`)
- **DTOs:** `KeNHATagVerificationResult`, `KeNHATagAlertDto` (`DTOs/Integration/IntegrationDtos.cs`, `DTOs/Weighing/WeighingTransactionDto.cs`)
- **Controller:** `ExternalIntegrationController` + `WeighingController` (background check)
- **DI Registration:** `Program.cs` — `AddHttpClient<IKeNHAService, KeNHAService>(c => c.Timeout = 15s)`

**Credential Storage (IntegrationConfig Table):**
- Provider: `kenha`
- Credentials encrypted via AES-256-GCM
- Seeded as **inactive** in development (awaiting live KeNHA API credentials)
- Activated via admin API: `PUT /api/v1/system/integrations/kenha`

**Weighing Integration Flow:**
1. Operator enters vehicle reg no on capture screen
2. `POST /api/v1/weighing-transactions` is called
3. **Three concurrent tasks execute:**
   - `InitiateWeighingAsync()` — creates weighing transaction
   - `CheckKeNHATagAsync()` — queries KeNHA API for tags (background, non-blocking)
   - `CheckVehicleTagsAsync()` — queries local TruLoad tags
4. Response includes `KeNHATagAlert` (if KeNHA tag found) and `OpenTags` (local tags)
5. Frontend shows both tag types on the decision page

**Alert Levels:**
- `critical` — KeNHA tag status is "open" (active prohibition)
- `warning` — KeNHA tag status is unknown
- `info` — KeNHA tag status is "closed" (historical)

**TruLoad API Endpoints:**
- `GET /api/v1/weighing-transactions/kenha-tag-check/{regNo}` - Standalone tag check (requires `weighing.read`)
- `GET /api/v1/integration/kenha/tag/{regNo}` - Direct KeNHA tag verification (requires `weighing.read`)
- Background check integrated into `POST /api/v1/weighing-transactions`

**Response Fields:**
- hasTag, tagStatus, tagCategory, reason, station, tagDate, tagUid
- alertLevel, message (enriched for decision page display)

**Error Handling:**
- Integration unavailable: Returns null (never blocks weighing)
- API timeout: 15-second HttpClient limit, returns null
- Cache: Redis 1-hour TTL per vehicle
- Parsing: Handles both array and object KeNHA responses (KenLoad V2 compatibility)

**Configuration (appsettings):**
```json
"KeNHA": {
  "BaseUrl": "https://kenload.kenha.co.ke",
  "Endpoints": {
    "VerifyTag": "/api/v3/vehicle/tag/verify",
    "WeighbridgeData": "/api/weighbridge/data?api_key={api_key}"
  },
  "ApiKey": "AWAITING_LIVE_CREDENTIALS"
}
```

---

### KeNHA Permit API

**Purpose:** Permit lookup and validation

**Configuration:**
- API URL: `KENHA_PERMIT_API_URL` (environment variable)
- API Key: `KENHA_PERMIT_API_KEY` (K8s secret)

**Endpoints:**
- `GET /api/v1/permits/validate?permitNo={permitNo}` - Validate permit

**Error Handling:**
- Cache permit data for offline use
- Retry on API failures
- Manual permit entry if API unavailable

---

## Integration Testing

### Test Strategy

**Unit Tests:**
- Mock external service responses
- Test error handling and retry logic
- Test data transformation

**Integration Tests:**
- Use Testcontainers for local Superset instance
- Mock external service responses
- Test end-to-end query processing flow

**Contract Tests:**
- Verify API contract compliance
- Test webhook signature verification
- Test error response formats

---

## Monitoring & Observability

### Metrics

**Integration-Specific Metrics:**
- API call latency (p50, p95, p99)
- API call success/failure rates
- Embedding generation latency
- Vector search query performance
- Superset dashboard creation/update success rates

**Alerts:**
- External service unavailability
- Superset service unavailability
- Vector search performance degradation
- Embedding generation failures

### Logging

**Structured Logging:**
- All API calls logged with request/response details
- Error logs include full stack traces and context
- Integration-specific log levels (DEBUG, INFO, WARN, ERROR)

---

## Security Considerations

### Authentication & Authorization

- All external API calls authenticated with API keys or tokens
- API keys stored in K8s secrets, never in code
- JWT tokens validated with local signing key
- Webhook signatures verified for callbacks

### Data Privacy

- Vector embeddings stored locally, not shared with external services
- Sensitive data encrypted in transit (TLS 1.3)
- PII data masked in logs

### Rate Limiting

- Respect external service rate limits
- Implement client-side rate limiting where applicable
- Queue requests if rate limits exceeded

---

## References

- [Apache Superset REST API Documentation](https://superset.apache.org/docs/api)
- [pgvector Extension Documentation](https://github.com/pgvector/pgvector)
- [ONNX Runtime Documentation](https://onnxruntime.ai/)
- [all-MiniLM-L12-v2 Model Card](https://huggingface.co/sentence-transformers/all-MiniLM-L12-v2)

