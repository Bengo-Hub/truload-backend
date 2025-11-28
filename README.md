# TruLoad Backend

**Intelligent Weighing and Enforcement Solution - Backend API**

Cloud-hosted enforcement and weighing backend with offline-aware clients. Core flows and legal rules follow EAC Vehicle Load Control Act (2016) and Kenya Traffic Act (Cap 403).

## 📚 Documentation

### Implementation & Architecture
- **[Implementation Plan](docs/plan.md)** - Comprehensive technical specification, database schema, modules, sprints, and API design
- **[Entity Relationship Diagram (ERD)](docs/erd.md)** - Complete database schema with entities, relationships, indexes, and vector columns
- **[Integration Guide](docs/integration.md)** - External service integrations (Superset, pgvector, ONNX, auth-service, TruConnect, eCitizen)
- **[Sprint Documentation](docs/sprints/)** - Detailed sprint plans and implementation guides

### Code Structure
- **[Data Layer Guide](Data/README.md)** - DbContext, migrations, entity configurations, seed data
- **[Modules Guide](Modules/README.md)** - Module structure, design principles, inter-module communication

### API Documentation
- **Swagger UI:** Available at `/swagger` when running the application

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- PostgreSQL 16+ (local or cloud instance)
- Redis 7+ (local or cloud instance)
- RabbitMQ 3.13+ (optional, for async tasks)

### Local Development

**Option 1: Using Cloud/Remote Databases** (Recommended)

```bash
# Update appsettings.Development.json with your database connection strings

# Restore dependencies
dotnet restore

# Run database migrations
dotnet ef database update

# Run the API
dotnet run

# API will be available at https://localhost:7001 (or configured port)
# Swagger UI at https://localhost:7001/swagger
```

**Option 2: Using Docker for Databases Only**

```bash
# Run PostgreSQL, Redis, RabbitMQ in Docker
docker run -d --name truload-postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:16-alpine
docker run -d --name truload-redis -p 6379:6379 redis:7-alpine redis-server --requirepass redis
docker run -d --name truload-rabbitmq -e RABBITMQ_DEFAULT_USER=user -e RABBITMQ_DEFAULT_PASS=rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3.13-management-alpine

# Then run the API
dotnet restore
dotnet ef database update
dotnet run
```

**Option 3: Using Production-like Environment**

```bash
# Build and run the Docker container
docker build -t truload-backend:dev .
docker run -p 8080:8080 truload-backend:dev
```

## 🏗️ Project Structure

```
truload-backend/
├── Controllers/          # API endpoints by module
├── Modules/             # Feature modules (User, Weighing, Prosecution, etc.)
│   ├── User/
│   ├── Weighing/
│   ├── Prosecution/
│   └── ...
├── Data/                # DbContext, migrations, configurations
├── Services/            # Business logic, domain services
├── Infrastructure/      # External integrations (TruConnect, eCitizen, etc.)
├── Shared/              # Common utilities, DTOs, responses
├── docs/                # Documentation
├── KubeSecrets/         # Kubernetes secrets (not committed with real values)
├── .github/workflows/   # CI/CD pipelines
├── Dockerfile           # Container definition
└── build.sh             # Build & deploy script
```

## 🔧 Configuration

Runtime configuration via Kubernetes secrets (see `KubeSecrets/devENV.yml` template) or local `appsettings.Development.json`:

- **Database:** PostgreSQL connection string
- **Redis:** Cache connection string
- **RabbitMQ:** Message broker settings
- **JWT:** Secret key for token signing
- **CORS:** Allowed frontend origins
- **TruConnect:** Local microservice base URL

## 🧪 Testing

```bash
# Run unit tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## 🚢 Deployment

Deployment is automated via GitHub Actions and ArgoCD:

1. Push to `main` branch triggers `.github/workflows/deploy.yml`
2. `build.sh` runs Trivy scan, builds Docker image, pushes to registry
3. Updates `devops-k8s/apps/truload-backend/values.yaml` with new image tag
4. ArgoCD auto-syncs and deploys to Kubernetes cluster

Manual deployment:

```bash
./build.sh
```

## 📊 Modules

1. **User Management** - Authentication, roles, shifts, audit logs
2. **Weighing** - Static/WIM/Axle modes, TruConnect integration, compliance evaluation
3. **Prosecution** - Case management, EAC/Traffic Act charging, invoicing
4. **Yard & Tags** - Vehicle detention, prohibition orders, tag lifecycle
5. **Special Release** - Tolerance/permit-based releases
6. **Vehicle Inspection** - Dimensional compliance checks
7. **Reporting** - Registers, analytics, exports
8. **Settings** - Cameras, stations, I/O devices, prosecution defaults
9. **Security** - Audit trails, backups, password policies

## 🤝 Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines.

## 📄 License

See [LICENSE](LICENSE) for license information.

## 🔗 Related Projects

- [TruLoad Frontend](../truload-frontend) - Next.js 15 PWA client
- [Centralized DevOps](https://github.com/Bengo-Hub/devops-k8s) - Shared K8s infrastructure

## 📞 Support

For issues or questions, please open an issue in the GitHub repository.

