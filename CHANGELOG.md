# Changelog

All notable changes to TruLoad Backend will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- Upgraded to .NET 10 LTS from .NET 8 (January 2026)
- Upgraded Entity Framework Core to 10.0
- Upgraded Npgsql.EntityFrameworkCore.PostgreSQL to 10.0
- Updated all ASP.NET Core packages to 10.0
- Enhanced LINQ support with native left/right joins
- Improved performance with .NET 10 JIT optimizations

### Added
- Initial project setup with .NET 10 LTS
- pgvector support for semantic search (7 tables with vector embeddings)
- Table partitioning for weighing_transactions (monthly range partitions)
- 6 materialized views for dashboard performance
- 8 regular views for real-time filtered data
- HNSW indexes for vector similarity search
- Database schema design for all modules
- User authentication with JWT
- Health check endpoints
- Docker support
- Kubernetes deployment configuration
- CI/CD pipeline via GitHub Actions
- Integration with centralized devops-k8s
- Comprehensive .NET 10 upgrade analysis documentation

### Planned
- Weighing module implementation (Static, WIM, Axle modes)
- Prosecution module with EAC/Traffic Act charging
- Special Release workflows
- Vehicle Inspection module
- Comprehensive reporting
- TruConnect microservice integration

## [0.1.0] - 2025-10-28

### Added
- Project initialization
- Basic folder structure
- Documentation framework
- Build and deployment scripts

