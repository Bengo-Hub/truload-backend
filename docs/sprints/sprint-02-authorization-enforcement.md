# Sprint 2: Authorization & Permission Enforcement

**Duration:** Weeks 3-4  
**Module:** Role-Based Access Control (RBAC) Enforcement  
**Status:** Starting Dec 9, 2025

**Objective:** Implement fine-grained authorization using the Permission model from Phase 1. Create authorization policies, middleware, and attribute-based security for API endpoints.

---

## Phase 1 (Permission Model) Summary

✅ **Completed (26/26 tasks):**
- Permission entity with Code/Name/Category/Description/IsActive properties
- RolePermission junction table (composite key: RoleId + PermissionId)
- 77 permissions seeded across 8 categories:
  - **Weighing** (12): create, read, update, delete, approve, reweigh, calibrate, test, export, archive, view_history, assign
  - **Case** (15): create, read, update, delete, close, reopen, assign, transfer, view_all, view_own, escalate, export, archive, report, analytics
  - **Prosecution** (8): create, read, update, delete, file, view, export, archive
  - **User** (10): create, read, update, delete, assign_role, reset_password, disable, enable, export, audit
  - **Station** (12): create, read, update, delete, assign_device, manage_calibration, manage_scale, configure, test, export, audit, view_logs
  - **Configuration** (8): manage_permits, manage_fees, manage_tolerances, manage_settings, manage_categories, view_config, export_config, audit_config
  - **Analytics** (8): view_dashboards, create_reports, export_data, view_trends, view_metrics, manage_queries, schedule_reports, view_audit_logs
  - **System** (6): manage_roles, manage_permissions, audit_logs, health_check, system_config, backup
- Redis-cached PermissionService (1-hour TTL)
- PermissionsController API with 6 endpoints
- 55+ unit/integration tests (38 passing)

---

## Phase 2 Tasks

### 2.1 Authorization Policy Implementation

**2.1.1 - Create Authorization Policies**
- [ ] Create `IPermissionRequirement` interface for permission-based auth
- [ ] Implement `PermissionRequirementHandler` to check JWT claims against Permission model
- [ ] Create `HasPermissionAttribute` custom authorization attribute
- [ ] Create `HasAnyPermissionAttribute` for multiple permission checks (OR logic)
- [ ] Create `HasAllPermissionsAttribute` for multiple permission checks (AND logic)
- [ ] Register policies in Program.cs: `AuthPolicy.Permission`, `AuthPolicy.AnyPermission`, `AuthPolicy.AllPermissions`

**Acceptance Criteria:**
- Authorization policies accessible via `[Authorize(Policy = "Permission:weighing.create")]`
- Attribute support: `[HasPermission("weighing.create")]`
- User JWT must contain `auth_service_user_id` claim for permission lookup
- Failed authorization returns 403 Forbidden with error details

**2.1.2 - Create Permission Verification Service**
- [ ] Create `IPermissionVerificationService` interface
- [ ] Implement service methods:
  - `UserHasPermissionAsync(userId, permissionCode)` → returns bool
  - `UserHasAnyPermissionAsync(userId, permissionCodes[])` → returns bool
  - `UserHasAllPermissionsAsync(userId, permissionCodes[])` → returns bool
  - `GetUserPermissionsAsync(userId)` → returns List<Permission>
  - `GetUserRolesAsync(userId)` → returns List<Role>
- [ ] Use PermissionService for caching, auth-service for user-role lookup
- [ ] Register as Scoped service in DI

**Acceptance Criteria:**
- Service uses JWT claims to get `auth_service_user_id`
- Fallback to UserRepository if user not in JWT
- Results cached per-request to avoid redundant lookups
- Logs permission denials for audit trail

---

### 2.2 API Endpoint Protection

**2.2.1 - Protect Existing Endpoints with Permissions**
- [ ] PermissionsController: `GET /api/v1/permissions` → requires `system.view_config`
- [ ] PermissionsController: `GET /api/v1/permissions/{id}` → requires `system.view_config`
- [ ] PermissionsController: `GET /api/v1/permissions/category/{category}` → requires `system.view_config`
- [ ] PermissionsController: `GET /api/v1/roles/{roleId}/permissions` → requires `system.manage_roles`
- [ ] Add permission checks to existing User, Role, Station, Shift controllers:
  - User creation → `user.create`
  - User update → `user.update`
  - User deletion → `user.delete`
  - Role creation → `system.manage_roles`
  - Role update → `system.manage_roles`
  - Station creation → `station.create`

**Acceptance Criteria:**
- All controllers have [Authorize] attribute
- Specific endpoints use [HasPermission] or [Authorize(Policy = ...)]
- 403 Forbidden returned when user lacks permission
- 401 Unauthorized returned when JWT invalid/missing

**2.2.2 - Create Permission-Protected Audit Endpoints** (NEW)
- [ ] `GET /api/v1/audit-logs` → requires `system.audit_logs`
  - Filter by: userId, resource, action, dateRange
  - Pagination support (pageSize, pageNumber)
- [ ] `GET /api/v1/audit-logs/{id}` → requires `system.audit_logs`
- [ ] `POST /api/v1/audit-logs/export` → requires `system.audit_logs`
  - Export to CSV/JSON with date range filter

**Acceptance Criteria:**
- All endpoints protected with permission checks
- AuditLogRepository provides efficient filtering
- Pagination tested with large datasets

---

### 2.3 Authorization Middleware

**2.3.1 - Create Permission-Based Authorization Middleware**
- [ ] Create `PermissionAuthorizationMiddleware`
- [ ] Middleware checks:
  - Extract `auth_service_user_id` from JWT claims
  - Get route handler's required permissions from [HasPermission] attributes
  - Use PermissionVerificationService to check user permissions
  - Allow if user has required permission; deny with 403 otherwise
  - Log all permission checks (authorized/denied) for audit trail
- [ ] Register middleware in Program.cs after authentication/authorization middleware

**Acceptance Criteria:**
- Middleware only processes controller actions with [HasPermission] attributes
- Non-protected endpoints bypass middleware
- Permission checks logged with request ID for tracing
- Performance < 10ms per check (cached)

**2.3.2 - Create Centralized Authorization Error Handling**
- [ ] Create `AuthorizationExceptionHandler` (middleware or exception filter)
- [ ] Handle scenarios:
  - Missing JWT token → 401 with "Unauthorized"
  - Invalid JWT → 401 with "Invalid token"
  - Missing permission → 403 with specific permission name
  - User not found → 403 with "User not authorized"
- [ ] Return consistent error response format with error code and message

**Acceptance Criteria:**
- All auth errors follow same response format
- Error messages do not leak sensitive information
- Logs include request ID and user ID for debugging

---

### 2.4 Integration Testing

**2.4.1 - Unit Tests for Authorization Policies**
- [ ] Test `PermissionRequirementHandler` with various permission scenarios
- [ ] Test `HasPermissionAttribute` enforcement
- [ ] Test permission caching with cache hits/misses
- [ ] Test edge cases: null user, empty permissions, duplicate permissions

**Target:** 20+ tests, >85% code coverage for authorization layer

**2.4.2 - Integration Tests for Protected Endpoints**
- [ ] Test endpoint access with valid permission
- [ ] Test endpoint access with missing permission (403)
- [ ] Test endpoint access with invalid JWT (401)
- [ ] Test endpoint access without JWT (401)
- [ ] Test multiple permissions with AND/OR logic

**Target:** 30+ tests covering all permission-protected endpoints

**2.4.3 - End-to-End Authorization Flow**
- [ ] Create test fixture with test users, roles, permissions
- [ ] Test complete flow: auth-service → JWT → permission check → endpoint access
- [ ] Test role changes are reflected in permission checks
- [ ] Test permission cache invalidation on role updates

**Target:** 10+ end-to-end tests

---

### 2.5 Documentation & Deployment

**2.5.1 - Create Authorization Documentation**
- [ ] Document permission model and 77 permissions
- [ ] Create API endpoint authorization matrix (endpoint → required permissions)
- [ ] Document `[HasPermission]` attribute usage with examples
- [ ] Create troubleshooting guide for common auth issues

**2.5.2 - Update Swagger/OpenAPI Documentation**
- [ ] Add 401/403 response types to all protected endpoints
- [ ] Document permission requirements in Swagger descriptions
- [ ] Add JWT authentication scheme to Swagger UI

**2.5.3 - Prepare for Deployment**
- [ ] Verify all tests pass (>90% authorization layer coverage)
- [ ] Verify Redis cache operational in staging environment
- [ ] Load test authorization checks under high concurrency
- [ ] Create runbook for permission updates/hotfixes in production

---

## Task Dependencies

```
Phase 1 Complete (26/26) ✅
    ↓
2.1 Authorization Policies (6 tasks)
    ↓
2.2 API Endpoint Protection (7 tasks)
    ├→ 2.3 Authorization Middleware (4 tasks)
    ├→ 2.4 Integration Testing (6 tasks)
    └→ 2.5 Documentation (5 tasks)
```

---

## Success Criteria

- [ ] All 6 existing role-based roles protected with permission checks
- [ ] All new feature endpoints (weighing, case, prosecution, etc.) enforce permissions
- [ ] 80+ new tests for authorization layer (20+ unit, 30+ integration, 10+ e2e)
- [ ] Authorization response time < 50ms p95 (including cache lookups)
- [ ] All permission denials logged with request ID and user ID for audit trail
- [ ] Swagger/OpenAPI shows all auth requirements
- [ ] Zero security vulnerabilities in authorization code (OWASP Top 10 compliant)

---

## Estimated Effort

- **Policies & Verification Service:** 12 hours (tasks 2.1.1, 2.1.2)
- **Endpoint Protection:** 16 hours (tasks 2.2.1, 2.2.2)
- **Authorization Middleware:** 10 hours (tasks 2.3.1, 2.3.2)
- **Integration Testing:** 20 hours (task 2.4)
- **Documentation & Deployment:** 8 hours (task 2.5)

**Total:** ~66 hours (target: complete by end of Week 4)

---

## Notes

- All permission checks use cached data from PermissionService (Redis 1-hour TTL)
- User-role mappings fetched from auth-service via shared HTTP client (with Polly retries)
- Each permission check logged for audit trail and performance monitoring
- Authorization layer designed for high availability (circuit breaker on auth-service failures)
