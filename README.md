# TruLoad - Intelligent Weighing and Enforcement Solution

Cloud-hosted enforcement and weighing solution enabling roadside officers to capture vehicle weights, verify compliance with EAC Vehicle Load Control Act (2016) or Kenya Traffic Act (Cap 403), and manage enforcement actions.

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Field Officers & Back Office            │
│              (Browsers, Tablets, PWA Installed)             │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Next.js 15 PWA Frontend                   │
│  • Offline-first with IndexedDB queue                       │
│  • TanStack Query + Zustand                                 │
│  • Tailwind + Shadcn UI                                     │
│  • Real-time weight display                                 │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    .NET 8 Backend API                        │
│  • Modular architecture (User, Weighing, Prosecution, etc.) │
│  • EF Core 8 + PostgreSQL 16                                │
│  • Redis caching + RabbitMQ events                          │
│  • JWT auth + RBAC                                          │
└─────────────────────────────────────────────────────────────┘
                              │
         ┌────────────────────┼────────────────────┐
         ▼                    ▼                    ▼
┌────────────────┐  ┌────────────────┐  ┌─────────────────┐
│   PostgreSQL   │  │     Redis      │  │    RabbitMQ     │
│   (Primary DB) │  │   (Caching)    │  │  (Messaging)    │
└────────────────┘  └────────────────┘  └─────────────────┘
                              │
         ┌────────────────────┼────────────────────┐
         ▼                    ▼                    ▼
┌────────────────┐  ┌────────────────┐  ┌─────────────────┐
│  TruConnect    │  │    eCitizen    │  │ Case Management │
│  (Microservice)│  │   (Payments)   │  │   (NTAC/OB)     │
└────────────────┘  └────────────────┘  └─────────────────┘
```

## 📦 Project Structure

```
TruLoad/
├── truload-backend/          # .NET 8 Backend API
├── truload-frontend/         # Next.js 15 PWA Frontend
├── TruConnect/              # Node.js/Electron weight acquisition microservice
├── resources/               # Specifications, SQL samples, narrations
└── README.md               # This file
```

## 🚀 Quick Start

### Backend

```bash
cd truload-backend

# Local development
dotnet restore
dotnet run

# Production deployment (via CI/CD)
./build.sh
```

See [Backend README](truload-backend/README.md) for details.

### Frontend

```bash
cd truload-frontend

# Install dependencies
pnpm install

# Run dev server
pnpm dev

# Production deployment (via CI/CD)
./build.sh
```

See [Frontend README](truload-frontend/README.md) for details.

## 📚 Documentation

### Specifications
- [KURAWEIGH Modules & Key Process Flow](resources/KURAWEIGH%20MODULES%20%26%20Key%20Process%20Flow%20Specification%20.docx.md)
- [Detailed Prosecution Process Narration](resources/Detailed%20prosecutio%20process%20narration.md)
- [Axle & Act Tables SQL Samples](resources/axle_eact_traffic_act_tables.sql)

### Implementation Plans
- [Backend Implementation Plan](truload-backend/docs/plan.md) - Complete database schema, EAC/Traffic Act rules, 16-week sprint plan
- [Backend ERD](truload-backend/docs/erd.md) - Complete database schema with entities, relationships, and indexes
- [Backend Integration Guide](truload-backend/docs/integration.md) - External service integrations
- [Frontend Implementation Plan](truload-frontend/docs/plan.md) - PWA strategy, offline sync, UX flows, module breakdown
- [Frontend Integration Guide](truload-frontend/docs/integration.md) - Backend API, Superset SDK, and external integrations

### DevOps
- Centralized infrastructure: [Bengo-Hub/devops-k8s](https://github.com/Bengo-Hub/devops-k8s)
- ArgoCD applications: `devops-k8s/apps/truload-{backend|frontend}/`
- CI/CD: GitHub Actions in each app (`.github/workflows/deploy.yml`)

## 🎯 Key Features

### Core Modules
1. **Weighing Module** - Static, WIM, Mobile/Axle modes; TruConnect integration
2. **Prosecution Module** - EAC/Traffic Act charging, invoicing, court escalation
3. **Special Release** - Tolerance/permit-based releases, manual authorizations
4. **Vehicle Inspection** - Dimensional compliance (wide load checks)
5. **Yard Management** - Prohibition orders, redistribution, offload workflows
6. **Reporting** - Comprehensive registers, analytics, exports
7. **User Management** - Authentication, roles, shifts, RBAC
8. **Settings** - Stations, cameras, I/O devices, prosecution defaults

### Legal Compliance
- **EAC Act (2016):** Charges higher of GVW vs axle overload; 5% axle tolerance; USD fee bands
- **Traffic Act (Cap 403):** GVW-only charging; KSh fee bands with USD conversion
- **Permits:** Extended limits for 2A (+3000 kg axle, +1000 kg GVW), 3A (+3000 kg axle, +2000 kg GVW)
- **Tolerances:** Configurable operational tolerance (default ≤200 kg) for auto special releases

### Technical Capabilities
- **Offline-first:** IndexedDB queue, background sync, conflict resolution
- **High Performance:** Partitioned tables, Redis caching, read replicas
- **Real-time:** TruConnect weight streaming, SignalR for live updates
- **Scalable:** HPA/VPA, multi-replica deployments, load balancing
- **Auditable:** Immutable audit logs, document generation (PDF), traceability

## 🚢 Deployment

Both apps use a **centralized DevOps workflow**:

1. **Local `build.sh`** handles app-specific build, scan, push
2. **Updates ArgoCD values** in `devops-k8s` repo
3. **ArgoCD auto-syncs** new images to Kubernetes
4. **Monitoring** via Prometheus + Grafana

### CI/CD Flow

```
Push to main
    │
    ├──> truload-backend/.github/workflows/deploy.yml
    │    └──> Calls truload-backend/build.sh
    │         └──> Updates devops-k8s/apps/truload-backend/values.yaml
    │              └──> ArgoCD syncs to K8s
    │
    └──> truload-frontend/.github/workflows/deploy.yml
         └──> Calls truload-frontend/build.sh
              └──> Updates devops-k8s/apps/truload-frontend/values.yaml
                   └──> ArgoCD syncs to K8s
```

### Required GitHub Secrets

| Secret | Purpose |
|--------|---------|
| `GH_PAT` | Cross-repo push to devops-k8s (required) |
| `KUBE_CONFIG` | Base64-encoded kubeconfig for K8s access |
| `REGISTRY_USERNAME` | Container registry username |
| `REGISTRY_PASSWORD` | Container registry password |
| `POSTGRES_PASSWORD` | PostgreSQL password |
| `REDIS_PASSWORD` | Redis password |
| `RABBITMQ_PASSWORD` | RabbitMQ password (backend only) |
| `GIT_USER` | Git committer name |
| `GIT_EMAIL` | Git committer email |

## 🧪 Testing

### Backend
```bash
cd truload-backend
dotnet test
```

### Frontend
```bash
cd truload-frontend
pnpm test
pnpm test:e2e
```

## 📊 Monitoring

- **Logs:** Serilog (backend), structured JSON
- **Metrics:** Prometheus scraping `/metrics`
- **Traces:** OpenTelemetry → Jaeger
- **Dashboards:** Grafana (linked in devops-k8s)

## 🤝 Contributing

See individual contribution guides:
- [Backend Contributing](truload-backend/CONTRIBUTING.md)
- [Frontend Contributing](truload-frontend/CONTRIBUTING.md)

## 📄 License

MIT License - See [Backend LICENSE](truload-backend/LICENSE) and [Frontend LICENSE](truload-frontend/LICENSE)

## 🔗 Related Projects

- [Centralized DevOps](https://github.com/Bengo-Hub/devops-k8s) - Shared Kubernetes infrastructure
- [BengoERP](../BengoERP) - Sister ERP project using same devops patterns

## 📞 Support

For issues or questions:
- Backend: Open issue in repo under `truload-backend/`
- Frontend: Open issue in repo under `truload-frontend/`

---

**Built with ❤️ for road authorities and enforcement agencies**

