# TruLoad Backend - Integration Guide

## Overview

This document provides detailed integration information for all external services and systems integrated with the TruLoad backend, including Apache Superset, Vector Database (pgvector), ONNX Runtime, centralized auth-service, and other third-party services.

---

## Table of Contents

1. [Centralized Auth-Service Integration](#centralized-auth-service-integration)
2. [Apache Superset Integration](#apache-superset-integration)
3. [Vector Database (pgvector) Integration](#vector-database-pgvector-integration)
4. [ONNX Runtime Integration](#onnx-runtime-integration)
5. [TruConnect Microservice Integration](#truconnect-microservice-integration)
6. [eCitizen Payment Gateway Integration](#ecitizen-payment-gateway-integration)
7. [Third-Party API Integrations](#third-party-api-integrations)

---

## Centralized Auth-Service Integration

### Overview

TruLoad backend integrates with the centralized `auth-service` for authentication and user identity management. Application-level user management (roles, shifts, permissions) is maintained locally but synchronized with the auth-service to avoid duplication.

### Architecture

**Authentication Flow:**
1. Frontend sends authentication request to TruLoad backend
2. TruLoad backend forwards request to centralized auth-service
3. Auth-service validates credentials and returns JWT tokens
4. TruLoad backend returns tokens to frontend
5. Subsequent requests include JWT token in Authorization header
6. TruLoad backend validates token with auth-service (or uses cached public key)

**User Synchronization:**
- One-way sync for user identity from auth-service
- Two-way sync for app-specific attributes (shifts, station assignments)
- Background sync jobs run periodically to reconcile user data

### Implementation Details

**Configuration:**
- Auth-service base URL: `AUTH_SERVICE_BASE_URL` (environment variable)
- Public key endpoint: `/api/v1/auth/public-key` (for JWT validation)
- User sync endpoint: `/api/v1/users/sync` (for user synchronization)
- Token refresh endpoint: `/api/v1/auth/refresh` (for token refresh)

**Database Schema:**
- `users` table includes `auth_service_user_id` (UUID) referencing auth-service
- Local fields: shifts, station assignments, role mappings, preferences
- Sync status tracked via `sync_status` and `sync_at` fields

**User Entity Relationship:**
- Local user maintains reference to auth-service user via `auth_service_user_id`
- User creation events from auth-service trigger local user creation
- User deactivation events from auth-service trigger local user deactivation

**Sync Jobs:**
- Periodic sync job (every 15 minutes) syncs user data from auth-service
- Event-driven sync on user creation/deactivation from auth-service
- Conflict resolution: Auth-service is source of truth for identity, local service for app-specific data

**JWT Token Handling:**
- Access tokens validated on each request using auth-service public key
- Token claims include: user ID, email, roles, station ID (if assigned)
- Token refresh handled via auth-service refresh endpoint
- Token caching in Redis to reduce validation overhead

### API Endpoints

**TruLoad Backend Endpoints:**
- `POST /api/v1/auth/login` - Forwards to auth-service, handles response
- `POST /api/v1/auth/refresh` - Forwards refresh request to auth-service
- `GET /api/v1/auth/user` - Returns current user from local database
- `POST /api/v1/users/sync` - Manually trigger user sync (admin only)

**Error Handling:**
- Auth-service unavailability: Return 503 Service Unavailable
- Invalid tokens: Return 401 Unauthorized
- Sync failures: Log error, retry with exponential backoff

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
- User authentication via JWT tokens passed to Superset for SSO
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

TruConnect is a Node.js/Electron microservice running on client machines that connects to scale indicators and exposes weight data via HTTP endpoints.

### Architecture

**Service Configuration:**
- Service URL: `TRUCONNECT_URL` (typically `http://localhost:3001`)
- Polling interval: 500ms (configurable)
- Timeout: 5 seconds
- Retry policy: Exponential backoff (3 retries)

**Communication:**
- HTTP GET requests to `/api/weights/stream`
- WebSocket fallback (if available)
- Local-only: Service runs on client machine, not accessible from network

### Implementation Details

**Weight Data Format:**
```json
{
  "deck": 1,
  "weight": 7950,
  "stable": true,
  "timestamp": "2025-10-28T12:34:56Z"
}
```

**Integration Points:**
- Frontend polls TruConnect directly (client-side)
- Backend receives finalized weight data from frontend
- Backend stores raw weight streams in `weight_stream_events` table for audit
- Backend stores finalized weights in `weighing_axles` table

**Error Handling:**
- TruConnect unavailability: Frontend shows "Scales Off" message
- Connection timeout: Retry with exponential backoff
- Invalid data: Log error, request retry

---

## eCitizen Payment Gateway Integration

### Overview

Integration with eCitizen payment gateway for invoice creation, payment processing, and receipt confirmation.

### Architecture

**Service Configuration:**
- Gateway URL: `ECITIZEN_GATEWAY_URL` (environment variable)
- API Key: `ECITIZEN_API_KEY` (K8s secret)
- Webhook URL: `ECITIZEN_WEBHOOK_URL` (callback endpoint)

**Authentication:**
- API key authentication via Authorization header
- Webhook signature verification for callbacks

### Implementation Details

**Invoice Creation:**
- POST request to `/api/v1/invoices/create`
- Request includes: case ID, amount, description, customer details
- Response includes: invoice ID, payment URL, invoice number
- Invoice stored in `invoices` table with external reference

**Payment Webhook:**
- Webhook endpoint: `/api/v1/payments/webhook/ecitizen`
- Signature verification: Validate webhook signature
- Payment confirmation: Update invoice status, create receipt record
- Background jobs: Trigger load correction memo and reweigh workflow

**Error Handling:**
- Retry logic: Polly with exponential backoff (3 retries)
- Circuit breaker: Opens after 5 consecutive failures
- Manual reconciliation: Admin interface for failed payments

---

## Third-Party API Integrations

### NTSA Vehicle Search API

**Purpose:** Vehicle registration lookup and verification

**Configuration:**
- API URL: `NTSA_API_URL` (environment variable)
- API Key: `NTSA_API_KEY` (K8s secret)

**Endpoints:**
- `GET /api/v1/vehicles/search?regNo={regNo}` - Search vehicle by registration

**Error Handling:**
- API unavailability: Cache last known vehicle data
- Rate limiting: Respect rate limits, queue requests
- Invalid responses: Log error, return null

### KeNHA Tags API

**Purpose:** Tag synchronization with KeNHA system

**Configuration:**
- API URL: `KENHA_TAGS_API_URL` (environment variable)
- API Key: `KENHA_TAGS_API_KEY` (K8s secret)

**Endpoints:**
- `POST /api/v1/tags` - Create tag in KeNHA system
- `GET /api/v1/tags/{tagId}` - Get tag status
- `PUT /api/v1/tags/{tagId}/close` - Close tag

**Error Handling:**
- Retry logic for failed tag submissions
- Queue tags for later submission if API unavailable
- Manual tag export interface for reconciliation

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
- Mock auth-service responses
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
- Auth-service unavailability
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
- JWT tokens validated with auth-service public key
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

