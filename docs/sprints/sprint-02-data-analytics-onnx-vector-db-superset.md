# Sprint 2: Data Analytics (ONNX, Vector DB, Superset)
**Duration:** Weeks 3-4
**Module:** Reporting & Analytics
**Status:** Planning

## Goal
Implement the centralized Data Analytics pipeline, enabling natural language querying via local AI models (ONNX), semantic search with pgvector, and dynamic dashboard generation using Apache Superset.

## Deliverables
- [ ] **Vector Database Setup:** PostgreSQL pgvector extension enabled and configured.
- [ ] **Embedding Service:** Local ONNX Runtime service for generating text embeddings.
- [ ] **Semantic Search:** Implementation of vector similarity search for finding relevant data/dashboards.
- [ ] **Superset Integration:** Backend service to manage Superset assets (dashboards, datasets) via API.
- [ ] **Query Engine:** Logic to translate NL intent into SQL or Superset visualizations (using SLM or structured parser).
- [ ] **Dashboard Embedding:** Secure generation of guest tokens for frontend embedding.

## Tasks

### 1. Vector Database & Embeddings
- [ ] Enable `vector` extension on PostgreSQL. <!-- id: 1 -->
- [ ] Add `embedding` vector columns to `dashboards`, `datasets`, and `reports` tables. <!-- id: 2 -->
- [ ] Implement `EmbeddingService` using `Microsoft.ML.OnnxRuntime`. <!-- id: 3 -->
- [ ] Integrate a lightweight embedding model (e.g., `all-MiniLM-L12-v2`) into the build. <!-- id: 4 -->
- [ ] Create data seeding job to generate embeddings for existing metadata. <!-- id: 5 -->

### 2. Natural Language Processing (NLP)
- [ ] Implement `QueryIntentParser` to extract entities (Time, Station, Vehicle Type) from NL input. <!-- id: 6 -->
- [ ] **Feasibility Check:** Evaluate integrating a small quantized LLM (e.g., Phi-3-mini or Llama-3-8B-Quantized) for NL-to-SQL translation if rule-based parsing is insufficient. <!-- id: 7 -->
- [ ] Implement `VectorSearchService` to find most relevant datasets/dashboards based on user query. <!-- id: 8 -->

### 3. Apache Superset Integration
- [ ] Create `SupersetClient` for interacting with Superset REST API. <!-- id: 9 -->
- [ ] Implement `DashboardManager` to programmatically create/update dashboards. <!-- id: 10 -->
- [ ] Implement `SecurityManager` to handle Superset Guest Tokens (RLS). <!-- id: 11 -->
- [ ] Create "Template Dashboards" in Superset for common scenarios (Weighing, Prosecution). <!-- id: 12 -->

### 4. API Endpoints
- [ ] `POST /api/v1/analytics/query`: Accepts NL text, returns embedded dashboard URL or data. <!-- id: 13 -->
- [ ] `GET /api/v1/analytics/dashboards`: Lists available dashboards. <!-- id: 14 -->
- [ ] `POST /api/v1/analytics/embed`: Generates signed guest token for a specific dashboard. <!-- id: 15 -->

## Dependencies
- Centralized `devops-k8s` Superset deployment.
- `auth-service` for user synchronization.
- PostgreSQL 16+ with pgvector.

## Risks
- **Performance:** Running ONNX models alongside the API might consume significant CPU. *Mitigation: Use separate microservice or VPA.*
- **Accuracy:** NL-to-SQL is error-prone. *Mitigation: Fallback to "Recommended Dashboards" instead of generating new ones.*
