# Changelog

All notable changes to TruLoad Backend will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Weighing Operations module with transaction management
- Vehicle management with registration validation
- Yard management for vehicle tracking
- Case Management module for prosecutions
- User Management with ASP.NET Core Identity integration
- System configuration endpoints
- Authentication endpoints with JWT support
- Authorization middleware with permission-based policies
- Comprehensive repository pattern implementation
- Service layer with business logic separation
- DTOs for clean API contracts
- Validation using FluentValidation
- Global error handling middleware
- Health check endpoints for monitoring
- Database schema with comprehensive ERD documentation
- Support for multiple weighing modes (Static, WIM, Axle)
- EAC Vehicle Load Control Act (2016) compliance rules
- Kenya Traffic Act (Cap 403) compliance rules
- Reweigh workflow with cycle limits
- Special release processing
- Prosecution case workflows

### Planned
- ONNX model integration for analytics
- Superset dashboard integration
- TruConnect microservice integration for scale data
- RabbitMQ event publishing for background tasks
- Redis caching for hot read paths
- Background job processing for heavy operations
- Webhook support for external integrations
- Advanced reporting and analytics
- Audit trail enhancements
- Multi-language support
- SMS/Email notifications

## [0.2.0] - 2026-02-02

### Changed
- Upgraded to .NET 10 LTS from .NET 8 (January 2026)
- Upgraded Entity Framework Core to 10.0
- Upgraded Npgsql.EntityFrameworkCore.PostgreSQL to 10.0
- Updated all ASP.NET Core packages to 10.0
- Enhanced LINQ support with native left/right joins
- Improved performance with .NET 10 JIT optimizations
- Refined modular architecture with clear feature boundaries
- Updated documentation with current implementation status

### Added
- pgvector support for semantic search (7 tables with vector embeddings)
- Table partitioning for weighing_transactions (monthly range partitions)
- 6 materialized views for dashboard performance
- 8 regular views for real-time filtered data
- HNSW indexes for vector similarity search
- Integration with centralized devops-k8s repository
- Comprehensive .NET 10 upgrade analysis documentation
- Collaboration guidelines and coding standards
- Security policy and incident response procedures

## [0.1.0] - 2025-10-28

### Added
- Project initialization with .NET 8
- Basic modular folder structure
- Controllers for Auth, System, UserManagement, WeighingOperations, CaseManagement, Yard
- Database migrations setup
- Docker and Kubernetes configuration
- CI/CD pipeline via GitHub Actions
- Documentation framework (README, ERD, Implementation Plan)
- Build and deployment scripts

