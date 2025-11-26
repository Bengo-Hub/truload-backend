# Sprint 1: User Management & Security

**Duration:** Weeks 1-2  
**Module:** User Management & Security  
**Status:** Planning

---

## Overview

Implement foundation for user management, authentication integration with centralized auth-service, role-based access control (RBAC), shift management, and comprehensive audit logging.

---

## Objectives

- Set up project structure and core dependencies
- Implement authentication integration with centralized auth-service
- Create user management entities with auth-service sync
- Implement role-based access control (RBAC)
- Create shift management functionality
- Implement comprehensive audit logging middleware
- Seed default admin user and roles

---

## Tasks

### Project Setup & Infrastructure

- [ ] Initialize .NET 8 Web API project
- [ ] Set up solution structure with modular architecture
- [ ] Configure Entity Framework Core 8 with PostgreSQL
- [ ] Set up dependency injection container
- [ ] Configure Serilog for structured logging
- [ ] Set up Swagger/OpenAPI documentation
- [ ] Configure CORS for frontend integration
- [ ] Set up health check endpoint
- [ ] Configure environment variables and secrets management

### Database Schema

- [ ] Create EF Core DbContext with PostgreSQL connection
- [ ] Create User entity with auth-service integration fields
- [ ] Create Role entity
- [ ] Create UserRole junction entity
- [ ] Create Shift entity
- [ ] Create UserShift entity
- [ ] Create AuditLog entity
- [ ] Create initial migration for user management schema
- [ ] Apply migration to database
- [ ] Seed default admin user and roles

### Auth-Service Integration

- [ ] Create HttpClient configuration for auth-service
- [ ] Implement authentication endpoint proxy (`POST /api/v1/auth/login`)
- [ ] Implement token refresh endpoint proxy (`POST /api/v1/auth/refresh`)
- [x] Configure OIDC Authentication with `auth-service` (Authority/Audience)
- [ ] Implement user sync service for auth-service synchronization
- [ ] Implement background sync job for user data reconciliation
- [ ] Create user profile endpoint (`GET /api/v1/auth/user`)
- [ ] Handle auth-service unavailability gracefully

### User Management

- [ ] Create user repository pattern
- [ ] Implement user CRUD operations
- [ ] Create user DTOs and mapping profiles
- [ ] Implement user validation (FluentValidation)
- [ ] Create user controller with CRUD endpoints
- [ ] Implement user search and filtering
- [ ] Create user sync status tracking
- [ ] Handle user creation/deactivation events from auth-service

### Role Management

- [ ] Create role repository pattern
- [ ] Implement role CRUD operations
- [ ] Create role DTOs and mapping profiles
- [ ] Implement role validation (FluentValidation)
- [ ] Create role controller with CRUD endpoints
- [ ] Implement user-role assignment endpoints
- [ ] Create role-based authorization policies
- [ ] Seed default roles (SuperAdmin, StationManager, Operator, Inspector)

### Shift Management

- [ ] Create shift repository pattern
- [ ] Implement shift CRUD operations
- [ ] Create shift DTOs and mapping profiles
- [ ] Implement shift validation (FluentValidation)
- [ ] Create shift controller with CRUD endpoints
- [ ] Implement user-shift assignment endpoints
- [ ] Create shift rotation group management
- [ ] Implement day mask validation for shift schedules

### Audit Logging

- [ ] Create audit logging middleware
- [ ] Intercept all POST/PUT/DELETE requests
- [ ] Log actor, action, entity, and changes (before/after)
- [ ] Include IP address and user agent in audit logs
- [ ] Create audit log repository pattern
- [ ] Implement audit log query endpoints
- [ ] Create audit log filtering and search
- [ ] Ensure audit logs are immutable (append-only)

### Security & Authorization

- [ ] Configure JWT authentication middleware
- [ ] Implement custom authorization policies
- [ ] Create RBAC policy handlers
- [ ] Implement role-based route protection
- [ ] Create authorization attributes for controllers
- [ ] Configure password policies (if applicable)
- [ ] Implement rate limiting for authentication endpoints
- [ ] Set up secure cookie configuration

### API Documentation

- [ ] Document all API endpoints with Swagger
- [ ] Add request/response examples
- [ ] Document authentication flow
- [ ] Document error responses
- [ ] Create API versioning strategy
- [ ] Generate OpenAPI specification

### Testing

- [ ] Write unit tests for user repository
- [ ] Write unit tests for role repository
- [ ] Write unit tests for shift repository
- [ ] Write unit tests for audit logging middleware
- [ ] Write integration tests for auth-service integration
- [ ] Write integration tests for authentication flow
- [ ] Write integration tests for user CRUD operations
- [ ] Write integration tests for role-based authorization
- [ ] Set up test database with Testcontainers

---

## Acceptance Criteria

- [ ] All API endpoints documented and tested
- [ ] Authentication integration with centralized auth-service working
- [ ] User sync with auth-service implemented and tested
- [ ] Role-based access control (RBAC) implemented and tested
- [ ] Shift management functionality complete
- [ ] Audit logging middleware intercepts all mutations
- [ ] Default admin user and roles seeded in database
- [ ] Health check endpoint returns 200 OK when database is connected
- [ ] All tests passing (unit, integration)
- [ ] Code review completed and approved

---

## Dependencies

- PostgreSQL 16+ database instance
- Centralized auth-service available
- Redis instance for caching (optional for Sprint 1)

---

## Estimated Effort

**Total:** 80-100 hours

- Project Setup: 8-10 hours
- Database Schema: 10-12 hours
- Auth-Service Integration: 16-20 hours
- User Management: 12-15 hours
- Role Management: 8-10 hours
- Shift Management: 10-12 hours
- Audit Logging: 8-10 hours
- Security & Authorization: 8-10 hours
- Testing: 10-12 hours

---

## Risks & Mitigation

**Risk:** Auth-service unavailability blocking authentication  
**Mitigation:** Implement graceful degradation, cache auth-service public keys, allow offline token validation with local cache

**Risk:** User sync conflicts between auth-service and local service  
**Mitigation:** Auth-service is source of truth for identity, local service for app-specific data, implement conflict resolution strategy

**Risk:** Performance impact of audit logging on all mutations  
**Mitigation:** Implement asynchronous audit logging, use background jobs for log processing

---

## Notes

- User entity maintains reference to auth-service user via `auth_service_user_id`
- Local user management includes shifts, station assignments, role mappings
- Sync jobs run periodically (every 15 minutes) to reconcile user data
- Audit logs are immutable and append-only for compliance
- Default admin user created via migration seed data

---

## Deliverables

1. Working authentication integration with centralized auth-service
2. User management API with CRUD operations
3. Role-based access control (RBAC) implementation
4. Shift management functionality
5. Comprehensive audit logging middleware
6. Database schema with all user management entities
7. API documentation (Swagger)
8. Unit and integration tests
9. Seed data for default admin and roles
