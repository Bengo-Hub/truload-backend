# TruLoad Backend - RBAC Implementation Plan

**Status:** Planning Phase  
**Updated:** December 9, 2025  
**Integration:** Production Auth-Service (https://auth.codevertexitsolutions.com)

---

## Executive Summary

This document outlines the comprehensive RBAC (Role-Based Access Control) implementation for TruLoad Backend, integrating with the centralized auth-service for authentication while implementing fine-grained, domain-aware permissions locally.

**Key Objectives:**
- Integrate with production auth-service (OAuth2/OIDC) for authentication
- Implement 77 domain-specific permissions across 8 categories
- Create type-safe Permission and RolePermission data models
- Establish Redis-backed permission caching for performance
- Protect all 90+ API endpoints with granular authorization policies
- Support resource-level access control (e.g., officers only access own station weighings)
- Provide comprehensive audit trail of permission usage

---

## Architecture Overview

### Authentication vs Authorization

**Authentication (Auth-Service Domain):**
- Email/password login → Auth-Service OAuth2 `/api/v1/auth/login`
- JWT token issuance with standard claims (sub, email, aud, iss, exp)
- JWKS public keys: `https://auth.codevertexitsolutions.com/api/v1/.well-known/jwks.json`
- Token validation via signature verification
- Token refresh via `https://auth.codevertexitsolutions.com/api/v1/auth/refresh`

**Authorization (TruLoad Backend Domain):**
- Local permission model: 77 permissions in 8 categories
- Role-Permission mapping via `role_permissions` junction table
- Claims enrichment: Extract user's local roles from TruLoad database
- Policy evaluation: `CanCreateWeighing`, `CanApproveCase`, etc.
- Resource ownership checks: Officers can only modify own-created weighings
- Audit trail: Track all permission-gated operations

### Three-Layer Authorization

```
Layer 1: JWT Validation
  ↓ (Valid JWT)
Layer 2: Claims Extraction
  ↓ (Extract sub, email, roles from JWT)
Layer 3: Permission Resolution
  ↓ (Query local database for user's permissions)
Layer 4: Policy Evaluation
  ↓ (Check [Authorize(Policy="...")] attributes)
Layer 5: Resource Ownership
  ↓ (Verify user created or owns the resource)
Layer 6: Audit Logging
  ✓ (Log permission-gated operation)
```

---

## Production Auth-Service Integration

### Configuration

Update `appsettings.json`:

```json
{
  "Authentication": {
    "Authority": "https://auth.codevertexitsolutions.com",
    "Audience": "truload-backend",
    "RequireHttpsMetadata": true,
    "JwksUri": "https://auth.codevertexitsolutions.com/api/v1/.well-known/jwks.json",
    "TokenEndpoint": "https://auth.codevertexitsolutions.com/api/v1/token",
    "UserInfoEndpoint": "https://auth.codevertexitsolutions.com/api/v1/userinfo",
    "JwksCacheDuration": "3600",
    "JwksRefreshThreshold": "300"
  }
}
```

### Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/v1/auth/login` | POST | Email/password authentication |
| `/api/v1/auth/refresh` | POST | Token refresh (rotate refresh token) |
| `/api/v1/.well-known/openid-configuration` | GET | OIDC discovery |
| `/api/v1/.well-known/jwks.json` | GET | JWKS public keys (cached locally) |
| `/api/v1/userinfo` | GET | User profile (requires Bearer token) |

### Local Development

For local development, auth-service runs at `http://localhost:4101`:

```json
{
  "Authentication": {
    "Authority": "http://localhost:4101",
    "RequireHttpsMetadata": false
  }
}
```

---

## Permission Model Design

### Entities

#### Permission

Represents a granular capability (e.g., "Create Weighing", "Approve Case").

```csharp
public class Permission
{
    public Guid Id { get; set; }
    public string Code { get; set; }           // "weighing.create"
    public string Name { get; set; }           // "Create Weighing"
    public string Category { get; set; }       // "Weighing", "Case", "Prosecution", etc.
    public string Description { get; set; }    // "Officer can create new weighing records"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; }
}
```

#### RolePermission

Junction table linking roles to permissions.

```csharp
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public Role Role { get; set; }
    public Permission Permission { get; set; }
}
```

### Database Schema

#### permissions table

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | UUID | PRIMARY KEY | Permission ID |
| code | VARCHAR(50) | UNIQUE, NOT NULL, INDEX | Code: "weighing.create" |
| name | VARCHAR(255) | NOT NULL | Display name |
| category | VARCHAR(50) | NOT NULL, INDEX | Category: weighing, case, prosecution, etc. |
| description | TEXT | | Detailed description |
| is_active | BOOLEAN | DEFAULT TRUE | Active status |
| created_at | TIMESTAMPTZ | DEFAULT NOW() | Creation time |

**Indexes:**
- `idx_permissions_code` (code) UNIQUE
- `idx_permissions_category` (category) WHERE is_active = TRUE
- `idx_permissions_active` (is_active)

#### role_permissions table

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| role_id | UUID | FK → roles(id), PRIMARY KEY | Role ID |
| permission_id | UUID | FK → permissions(id), PRIMARY KEY | Permission ID |
| assigned_at | TIMESTAMPTZ | DEFAULT NOW() | Assignment timestamp |

**Indexes:**
- `idx_role_permissions_permission` (permission_id)

### Permission Categories (77 Total)

#### 1. Weighing Operations (12 permissions)

| Code | Name | Description |
|------|------|-------------|
| weighing.create | Create Weighing | Officer can create new weighing records |
| weighing.read | Read All Weighings | Officer can view all weighing records |
| weighing.read_own | Read Own Weighings | Officer can only view their own weighings |
| weighing.update | Update Weighing | Officer can modify weighing details |
| weighing.approve | Approve Weighing | Officer can approve weighing for compliance |
| weighing.override | Override Tolerance | Officer can override tolerance policies |
| weighing.send_to_yard | Send to Yard | Officer can mark vehicle for yard processing |
| weighing.scale_test | Perform Scale Test | Officer can conduct daily scale calibration |
| weighing.export | Export Weighings | Officer can export weighing records |
| weighing.delete | Delete Weighing | Officer can delete weighing records (draft only) |
| weighing.webhook | Manage Webhooks | Officer can configure weighing webhooks |
| weighing.audit | View Audit Trail | Officer can view weighing audit logs |

#### 2. Case Management (15 permissions)

| Code | Name | Description |
|------|------|-------------|
| case.create | Create Case Register | Officer can create case from weighing or manually |
| case.read | Read All Cases | Officer can view all case registers |
| case.read_own | Read Own Cases | Officer can only view their own cases |
| case.update | Update Case | Officer can modify case details |
| case.assign | Assign Case | Officer can assign/reassign cases to others |
| case.close | Close Case | Officer can close resolved cases |
| case.escalate | Escalate to Manager | Officer can escalate to case manager |
| case.special_release | Grant Special Release | Officer can approve special release |
| case.subfile_manage | Manage Case Subfiles | Officer can upload/modify case subfiles (B-J) |
| case.closure_review | Request Closure Review | Officer can request review before closure |
| case.arrest_warrant | Manage Arrest Warrants | Officer can issue arrest warrants |
| case.court_hearing | Schedule Court Hearing | Officer can schedule/reschedule court hearings |
| case.reweigh_schedule | Schedule Reweigh | Officer can schedule vehicle reweighing |
| case.export | Export Cases | Officer can export case data |
| case.audit | View Audit Trail | Officer can view case audit logs |

#### 3. Prosecution (8 permissions)

| Code | Name | Description |
|------|------|-------------|
| prosecution.create | Create Prosecution Case | Officer can initiate prosecution |
| prosecution.read | Read All Prosecutions | Officer can view all prosecution cases |
| prosecution.read_own | Read Own Prosecutions | Officer can only view their own prosecutions |
| prosecution.update | Update Prosecution | Officer can modify prosecution details |
| prosecution.compute_charges | Compute Charges | Officer can calculate violation charges |
| prosecution.generate_certificate | Generate Certificate | Officer can generate EAC/Traffic Act certificates |
| prosecution.export | Export Prosecutions | Officer can export prosecution records |
| prosecution.audit | View Audit Trail | Officer can view prosecution audit logs |

#### 4. User Management (10 permissions)

| Code | Name | Description |
|------|------|-------------|
| user.create | Create User | Admin can create new user accounts |
| user.read | Read All Users | Admin can view all user records |
| user.read_own | Read Own Profile | User can view their own profile |
| user.update | Update User | Admin can modify user details |
| user.update_own | Update Own Profile | User can modify their own profile |
| user.delete | Delete User | Admin can delete user accounts |
| user.assign_roles | Assign Roles | Admin can assign/revoke user roles |
| user.manage_permissions | Manage Permissions | Admin can grant/deny permissions (via roles) |
| user.manage_shifts | Assign Shifts | Admin can assign shifts to users |
| user.audit | View Audit Trail | Admin can view user audit logs |

#### 5. Station & Configuration (12 permissions)

| Code | Name | Description |
|------|------|-------------|
| station.read | Read Stations | Officer can view station configurations |
| station.read_own | Read Own Station | Officer can only access assigned station |
| station.create | Create Station | Admin can create new stations |
| station.update | Update Station | Admin can modify station settings |
| station.update_own | Update Own Station | Station Manager can modify own station |
| station.delete | Delete Station | Admin can delete stations |
| station.manage_staff | Manage Station Staff | Station Manager can assign users to station |
| station.manage_devices | Manage Devices | Station Manager can configure scales/cameras |
| station.manage_io | Manage I/O Signals | Station Manager can configure gate signals |
| station.configure_defaults | Configure Defaults | Admin can set station defaults |
| station.export | Export Station Data | Officer can export station configurations |
| station.audit | View Audit Trail | Officer can view station audit logs |

#### 6. Configuration Management (8 permissions)

| Code | Name | Description |
|------|------|-------------|
| config.read | Read Configuration | Officer can view system configurations |
| config.manage_axle | Manage Axle Configs | Admin can create/modify axle configurations |
| config.manage_permits | Manage Permits | Admin can issue/revoke permits |
| config.manage_fees | Manage Fee Schedules | Admin can set violation fees |
| config.manage_acts | Manage Legal Acts | Admin can configure legal frameworks |
| config.manage_taxonomy | Manage Taxonomy | Admin can manage violation types, tags, etc. |
| config.manage_references | Manage Reference Data | Admin can manage counties, districts, roads, etc. |
| config.audit | View Audit Trail | Admin can view configuration audit logs |

#### 7. Analytics & Reporting (8 permissions)

| Code | Name | Description |
|------|------|-------------|
| report.read | Read Reports | Officer can access analytics dashboards |
| report.read_own | Read Own Reports | Officer can only see own station's data |
| report.export | Export Reports | Officer can export data (CSV, PDF) |
| report.schedule | Schedule Reports | Officer can schedule automated reports |
| report.custom_query | Run Custom Query | Officer can execute custom analytics queries |
| report.manage_dashboards | Manage Dashboards | Admin can create/modify dashboards |
| report.superset | Access Superset | Officer can access embedded BI dashboards |
| report.audit | View Audit Trail | Officer can view report audit logs |

#### 8. System Administration (6 permissions)

| Code | Name | Description |
|------|------|-------------|
| system.admin | System Admin | Full system access (no restrictions) |
| system.audit_logs | Manage Audit Logs | Admin can view/export all audit logs |
| system.cache_management | Manage Cache | Admin can clear/refresh Redis caches |
| system.integration_management | Manage Integrations | Admin can configure external integrations |
| system.backup_restore | Backup & Restore | Admin can initiate backups/restore operations |
| system.security_policy | Manage Security Policies | Admin can configure password policies, etc. |

---

## Role Design

### Built-in Roles

#### SuperAdmin
- **Permission Count:** 77 (ALL)
- **Description:** Full system access, all operations
- **Typical Users:** System Owner, DevOps
- **Constraints:** No resource-level restrictions

#### Admin
- **Permissions:** 65 (all except system.* categories)
- **Description:** Administrative access, user/role management, configuration
- **Typical Users:** Operations Manager, Compliance Officer
- **Constraints:** Cannot access system-level settings

#### StationManager
- **Permissions:** 45 (station, weighing, case, prosecution, user [limited], report)
- **Description:** Manage station operations, assign staff, review cases
- **Typical Users:** Station Supervisor
- **Constraints:** Can only access own assigned station

#### Prosecutor
- **Permissions:** 30 (prosecution, case, user [limited], report)
- **Description:** Handle prosecution workflow, charge computation
- **Typical Users:** Legal Officer, Prosecutor
- **Constraints:** Case.read_own, cannot assign prosecutions to others

#### ScaleOperator
- **Permissions:** 12 (weighing [create, read_own, scale_test, audit])
- **Description:** Perform weighing, scale calibration
- **Typical Users:** Scale Officer
- **Constraints:** Can only view/create own weighings

#### Inspector
- **Permissions:** 18 (weighing, case [limited], report [read_own])
- **Description:** Inspect vehicles, initiate case register
- **Typical Users:** Field Inspector
- **Constraints:** Can create cases but not prosecute

---

## Implementation Phases

### Phase 1: Permission Model (Weeks 1-2, 40 hours)

**Deliverables:**
- ✅ Permission entity and EF Core model
- ✅ RolePermission junction table
- ✅ Database migration (CreatePermissionsTable)
- ✅ IPermissionService interface and implementation
- ✅ Redis-backed permission cache (1-hour TTL)
- ✅ Seed 77 permissions by category
- ✅ Update Role entity to remove JSON permissions field
- ✅ Unit tests (80%+ coverage)

**Key Files:**
- `Models/User/Permission.cs` - NEW
- `Models/User/RolePermission.cs` - NEW
- `Data/TruLoadDbContext.cs` - ADD configuration for Permission, RolePermission
- `Data/Migrations/*_AddPermissionsTable.cs` - NEW
- `Repositories/User/IPermissionRepository.cs` - NEW
- `Repositories/User/PermissionRepository.cs` - NEW
- `Services/IPermissionService.cs` - NEW
- `Services/PermissionService.cs` - NEW
- `Data/Seeders/PermissionSeeder.cs` - NEW
- `Program.cs` - ADD DI registration for IPermissionService

**Progress:** Not Started

### Phase 2: Authorization Policies (Weeks 2-3, 50 hours)

**Deliverables:**
- ✅ Define 15+ authorization policies (CanCreateWeighing, CanReadOwnOnly, IsStationManager, etc.)
- ✅ Implement 4 authorization handlers:
  - PermissionHandler - Check permission codes
  - RoleHandler - Check role membership
  - ResourceOwnershipHandler - Verify user created/owns resource
  - StationAccessHandler - Station Manager can only access own station
- ✅ Protect all 90+ API endpoints with [Authorize]
- ✅ Add [Authorize(Policy="...")] to sensitive operations
- ✅ Implement resource ownership validation in services
- ✅ Integration tests for all authorization flows

**Key Files:**
- `Authorization/Requirements/*.cs` (PermissionRequirement, RoleRequirement, etc.) - NEW
- `Authorization/Handlers/*.cs` (PermissionHandler, RoleHandler, etc.) - NEW
- `Authorization/Policies/AuthorizationPolicies.cs` - NEW
- `Controllers/**/*.cs` - UPDATE with [Authorize] attributes
- `Services/**/*.cs` - ADD resource ownership checks
- `Tests/Integration/AuthorizationTests.cs` - NEW

**Progress:** Not Started

### Phase 3: Swagger Security Configuration (Week 3, 15 hours)

**Deliverables:**
- ✅ Add Bearer JWT SecurityScheme to Swagger
- ✅ Add OAuth2 configuration (optional, pointing to auth-service)
- ✅ Swagger UI Authorize button functionality
- ✅ Document security requirements on all endpoints
- ✅ Add 401/403 response types to all endpoints

**Key Files:**
- `Program.cs` - UPDATE Swagger configuration
- `Controllers/**/*.cs` - ADD ProducesResponseType(401), ProducesResponseType(403)

**Progress:** Not Started

### Phase 4: User Sync & Claims Enrichment (Week 4, 30 hours) - Optional for MVP

**Deliverables:**
- ✅ ClaimEnrichmentMiddleware - Extract roles from local database
- ✅ UserSyncService - Background service subscribing to NATS events
- ✅ JWKS caching in Redis (24-hour TTL with auto-refresh)
- ✅ Handle token revocation (optional, via introspection)
- ✅ auth_service_sync_logs table for audit trail

**Key Files:**
- `Middleware/ClaimEnrichmentMiddleware.cs` - NEW
- `Services/UserSyncService.cs` - NEW
- `Services/JwksCache.cs` - NEW
- `Data/Models/AuthServiceSyncLog.cs` - NEW
- `Program.cs` - UPDATE middleware registration

**Progress:** Not Started

### Phase 5: Testing & Documentation (Weeks 4-5, 40 hours)

**Deliverables:**
- ✅ Unit tests for PermissionService (80%+ coverage)
- ✅ Unit tests for Authorization Handlers
- ✅ Integration tests for complete auth flows (login → API call → resource check)
- ✅ Security audit (endpoint protection, CORS, secrets)
- ✅ Developer guides:
  - RBAC Developer Guide (adding new permissions/policies)
  - API Authorization Guide (for API consumers)
  - Administrator Guide (managing roles/permissions)

**Key Files:**
- `Tests/Unit/Services/PermissionServiceTests.cs` - NEW
- `Tests/Unit/Authorization/*.cs` - NEW
- `Tests/Integration/AuthorizationTests.cs` - NEW
- `docs/RBAC_DEVELOPER_GUIDE.md` - NEW
- `docs/API_AUTHORIZATION_GUIDE.md` - NEW
- `docs/ADMIN_GUIDE.md` - NEW

**Progress:** Not Started

---

## Implementation Checklist - Phase 1

### Database Schema

- [ ] Create Permission entity model
- [ ] Create RolePermission entity model
- [ ] Configure DbSet<Permission> in TruLoadDbContext
- [ ] Configure DbSet<RolePermission> in TruLoadDbContext
- [ ] Create migration: `Add-Migration AddPermissionsTable`
- [ ] Verify migration SQL for permissions and role_permissions tables
- [ ] Apply migration: `Update-Database`
- [ ] Verify tables created in PostgreSQL

### Services & Repositories

- [ ] Create IPermissionRepository interface
- [ ] Implement PermissionRepository (CRUD + GetByCode, GetByRole)
- [ ] Create IPermissionService interface
- [ ] Implement PermissionService with Redis caching
  - GetUserPermissionsAsync(userId) → List<Permission>
  - UserHasPermissionAsync(userId, permissionCode) → bool
  - GetRolePermissionsAsync(roleId) → List<Permission>
  - InvalidateUserPermissionCacheAsync(userId)
  - InvalidateRolePermissionCacheAsync(roleId)
- [ ] Register services in DI container (Program.cs)
- [ ] Cache configuration (Redis TTL: 1 hour)

### Seed Data

- [ ] Create PermissionSeeder class
- [ ] Seed all 77 permissions by category
- [ ] Seed role-permission mappings:
  - SuperAdmin → all 77 permissions
  - Admin → 65 permissions (exclude system.*)
  - StationManager → 45 permissions
  - Prosecutor → 30 permissions
  - ScaleOperator → 12 permissions
  - Inspector → 18 permissions
- [ ] Make seeder idempotent (updates if permission already exists)
- [ ] Call seeder from DatabaseSeeder.SeedAsync()

### Testing

- [ ] Unit test: PermissionService.GetUserPermissionsAsync()
- [ ] Unit test: PermissionService.UserHasPermissionAsync()
- [ ] Unit test: PermissionService caching behavior
- [ ] Unit test: PermissionRepository CRUD
- [ ] Integration test: Permission seeding
- [ ] Integration test: Redis caching

---

## Authorization Policies (Phase 2)

### Policy Definitions

```csharp
// In AuthorizationPolicies.cs

public static class AuthorizationPolicies
{
    // Permission-based policies
    public const string CanCreateWeighing = "CanCreateWeighing";
    public const string CanApproveWeighing = "CanApproveWeighing";
    public const string CanReadOwnOnly = "CanReadOwnOnly";
    public const string CanManageUsers = "CanManageUsers";
    
    // Role-based policies
    public const string IsAdmin = "IsAdmin";
    public const string IsStationManager = "IsStationManager";
    public const string IsSuperAdmin = "IsSuperAdmin";
    
    // Resource-level policies
    public const string CanEditOwnWeighing = "CanEditOwnWeighing";
    public const string CanAccessStationData = "CanAccessStationData";
}
```

### Authorization Handlers

```csharp
// PermissionHandler
public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionCode { get; set; }
}

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return;
        
        if (await _permissionService.UserHasPermissionAsync(Guid.Parse(userId), requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }
    }
}
```

### Endpoint Protection Example

```csharp
[HttpPost("weighings")]
[Authorize]
[Authorize(Policy = AuthorizationPolicies.CanCreateWeighing)]
public async Task<IActionResult> CreateWeighing([FromBody] CreateWeighingRequest request)
{
    // Implementation
}
```

---

## Configuration Files

### appsettings.json (Production Auth-Service)

```json
{
  "Authentication": {
    "Authority": "https://auth.codevertexitsolutions.com",
    "Audience": "truload-backend",
    "RequireHttpsMetadata": true,
    "JwksUri": "https://auth.codevertexitsolutions.com/api/v1/.well-known/jwks.json",
    "TokenEndpoint": "https://auth.codevertexitsolutions.com/api/v1/token",
    "UserInfoEndpoint": "https://auth.codevertexitsolutions.com/api/v1/userinfo",
    "JwksCacheDuration": "3600",
    "JwksRefreshThreshold": "300"
  },
  "Permissions": {
    "CacheDuration": "3600",
    "CacheKeyPrefix": "truload:permissions"
  }
}
```

### appsettings.Development.json (Local Auth-Service)

```json
{
  "Authentication": {
    "Authority": "http://localhost:4101",
    "RequireHttpsMetadata": false
  }
}
```

---

## Success Metrics

| Metric | Target | Current | Status |
|--------|--------|---------|--------|
| Endpoints with [Authorize] | 100% (90+) | 4% (4) | ❌ |
| Write operations with permission checks | 100% | 0% | ❌ |
| Read_own endpoints with ownership checks | 95% | 0% | ❌ |
| Permission cache hit rate | >90% | N/A | ⏳ |
| Authorization test coverage | >80% | 0% | ❌ |
| Security audit P1/P2 findings | 0 | TBD | ⏳ |
| Role-permission assignment time | <100ms (cached) | N/A | ⏳ |

---

## Risk Assessment & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|-----------|
| Backward compatibility break | High | Medium | Create migration script, provide deprecation period |
| JWT validation failure | High | Low | Implement JWKS caching, short offline window |
| Permission cache inconsistency | Medium | Low | Invalidate on role change, implement TTL |
| Resource ownership bypass | Critical | Low | Enforce checks in all write operations, audit |
| Performance degradation | Medium | Medium | Use Redis caching, optimize queries |

---

## Rollout Strategy

**Phase 1-2: Critical Endpoints First**
1. Week 1: Deploy Permission model, IPermissionService (no breaking changes)
2. Week 2-3: Deploy authorization policies to critical endpoints only:
   - POST /api/v1/weighings (create)
   - POST /api/v1/cases (create)
   - PUT /api/v1/cases/{id} (update)
   - POST /api/v1/prosecutions (create)
3. Week 3-4: Roll out policies to remaining endpoints gradually
4. Week 4-5: Testing, documentation, security audit

**Phase 4-5: Optional for MVP**
- Can defer user sync and claims enrichment to follow-up sprint
- Still validates JWTs but with simpler claims model

---

## References

- `docs/erd.md` - Complete database schema
- `docs/plan.md` - Implementation plan
- `sprints/sprint-01-user-management-security.md` - Detailed sprint tasks
- `.github/copilot-instructions.md` - Platform patterns
- Auth-Service README: `auth-service/auth-api/README.md`
- Auth-Service ERD: `auth-service/auth-api/docs/erd.md`
