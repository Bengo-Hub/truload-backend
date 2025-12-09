# Sprint 1 & Axle System - Production Readiness Checklist

**Status:** In Progress  
**Last Updated:** 2025-12-09  
**Target Completion:** Sprint 1 fully complete, Axle repositories/controllers ready for Sprint 3

---

## ✅ Completed Items

### Database Schema & Models
- ✅ All Sprint 1 entities created (User, Organization, Department, Station, Role, WorkShift, etc.)
- ✅ Axle entities created (TyreType, AxleGroup, AxleConfiguration, AxleWeightReference, AxleFeeSchedule, WeighingAxle)
- ✅ Driver & DriverDemeritRecord entities created (for Sprint 7)
- ✅ TruLoadDbContext configured with Fluent API
- ✅ Migrations applied to PostgreSQL
- ✅ Snake_case column naming convention enforced

### Repositories Implemented
- ✅ UserRepository, OrganizationRepository, DepartmentRepository, RoleRepository, WorkShiftRepository, StationRepository
- ✅ IAxleConfigurationRepository interface created
- ✅ AxleConfigurationRepository implementation complete

### Controllers
- ✅ UsersController, OrganizationsController, DepartmentsController, RolesController, WorkShiftsController, StationsController exist
- ✅ HealthController for health checks

### Documentation
- ✅ Sprint 7 created for demerit points integration (docs/sprints/sprint-07-demerit-points-prosecution.md)
- ✅ axle-erd-map.md comprehensive documentation complete

---

## 🔨 Remaining Work for Production Readiness

### Sprint 1: User Management (Priority 1 - Critical)

#### 1. Complete Auth-Service Integration
**Files to create/update:**
- [ ] `Services/AuthService.cs` - Proxy for auth-service login/refresh endpoints
- [ ] `Services/UserSyncService.cs` - Background sync job for user reconciliation
- [ ] `Program.cs` - Add OIDC authentication configuration
  ```csharp
  builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options => {
          options.Authority = "https://auth-service.bengobox.local";
          options.Audience = "truload";
          options.TokenValidationParameters = new TokenValidationParameters {
              ValidateIssuer = true,
              ValidateAudience = true,
              ValidateLifetime = true
          };
      });
  ```
- [ ] Add Hangfire for background jobs (`Install-Package Hangfire.PostgreSql`)
- [ ] Publish `truload.user.synced.v1` events via outbox after sync

**Acceptance:**
- Login endpoint proxies to auth-service and returns JWT
- User sync service runs every 15 minutes
- JWKS cached with rotation handling
- Orphaned users (deleted from auth-service) blocked

#### 2. Implement Audit Logging Middleware
**Files to create:**
- [ ] `Middleware/AuditLoggingMiddleware.cs`
  - Intercept POST/PUT/DELETE requests
  - Log actor (user ID from JWT), action (HTTP method + route), entity (from route)
  - Capture old/new values (serialize request body and response)
  - Log IP address (`HttpContext.Connection.RemoteIpAddress`)
  - Log user agent (`HttpContext.Request.Headers["User-Agent"]`)
  - Save to AuditLog table asynchronously
- [ ] Register middleware in `Program.cs`: `app.UseMiddleware<AuditLoggingMiddleware>();`

**Acceptance:**
- All mutations logged to `audit_logs` table
- No performance impact (async processing)
- Immutable audit trail

#### 3. Seed Default Roles & Admin User
**Files to update:**
- [ ] `Data/Seeders/UserManagement/UserManagementSeeder.cs` - Add role seeding
  ```csharp
  var roles = new List<Role> {
      new Role { Name = "SuperAdmin", Code = "SUPER_ADMIN", Permissions = JsonSerializer.Serialize(new { all = true }) },
      new Role { Name = "StationManager", Code = "STATION_MGR", Permissions = ... },
      new Role { Name = "Operator", Code = "OPERATOR", Permissions = ... },
      new Role { Name = "Inspector", Code = "INSPECTOR", Permissions = ... }
  };
  ```
- [ ] Add default admin user seed (requires auth_service_user_id from auth-service)

**Acceptance:**
- 4 default roles seeded
- Default admin user created with SuperAdmin role

#### 4. Testing & Documentation
- [ ] Unit tests for all repositories (target 80% coverage)
- [ ] Integration tests for authentication flow
- [ ] Integration tests for CRUD operations with audit logging
- [ ] Complete Swagger documentation with examples
- [ ] Test health check returns 200 OK

**Acceptance:**
- All tests passing
- Swagger UI accessible at `/swagger`
- Health endpoint returns database status

---

### Axle System (Priority 2 - Required for Sprint 3)

#### 5. Complete Master Data Repositories
**Files to create:**
- [ ] `Repositories/Weighing/TyreTypeRepository.cs`
- [ ] `Repositories/Weighing/AxleGroupRepository.cs`
- [ ] `Repositories/Weighing/AxleFeeScheduleRepository.cs`

**Redis Caching Setup:**
- [ ] Add `Microsoft.Extensions.Caching.StackExchangeRedis` package
- [ ] Configure Redis in `Program.cs`:
  ```csharp
  builder.Services.AddStackExchangeRedisCache(options => {
      options.Configuration = builder.Configuration.GetConnectionString("Redis");
      options.InstanceName = "TruLoad:";
  });
  ```
- [ ] Implement caching in repositories (24hr TTL for TyreType/AxleGroup, 1hr for AxleFeeSchedule)
- [ ] Cache invalidation on updates

**Acceptance:**
- All repositories implemented with caching
- Cache hit rate > 90% for read operations
- Cache invalidation works correctly

#### 6. Create Axle Controllers
**Files to create:**
- [ ] `Controllers/Weighing/AxleConfigurationsController.cs`
  - GET `/api/v1/axle-configurations` (list with filters)
  - GET `/api/v1/axle-configurations/{id}` (include weight references)
  - POST `/api/v1/axle-configurations` (create derived, admin only)
  - PUT `/api/v1/axle-configurations/{id}` (update derived, admin only)
  - DELETE `/api/v1/axle-configurations/{id}` (soft delete, admin only)

- [ ] `Controllers/Weighing/TyreTypesController.cs`
  - GET `/api/v1/tyre-types` (public)
  - POST/PUT/DELETE (SuperAdmin only)

- [ ] `Controllers/Weighing/AxleGroupsController.cs`
  - GET `/api/v1/axle-groups` (public)
  - POST/PUT/DELETE (SuperAdmin only)

- [ ] `Controllers/Weighing/AxleFeeSchedulesController.cs`
  - GET `/api/v1/axle-fee-schedules` (public, filter by legal_framework)
  - POST `/api/v1/axle-fee-schedules/calculate-fee` (fee calculation endpoint)
  - POST/PUT/DELETE (SuperAdmin only)

**Authorization:**
- [ ] Add `[Authorize(Roles = "SuperAdmin")]` to admin endpoints
- [ ] Add `[AllowAnonymous]` to public read endpoints (or remove `[Authorize]` if app-wide auth not enabled yet)

**Acceptance:**
- All CRUD endpoints functional
- Authorization enforced
- Validation errors return 400 with clear messages
- Swagger documentation complete with examples

#### 7. Create DTOs & Validation
**Files to create:**
- [ ] `DTOs/Weighing/AxleConfigurationDto.cs` (request/response)
- [ ] `DTOs/Weighing/TyreTypeDto.cs`
- [ ] `DTOs/Weighing/AxleGroupDto.cs`
- [ ] `DTOs/Weighing/AxleFeeScheduleDto.cs`
- [ ] `DTOs/Weighing/FeeCalculationRequest.cs` & `FeeCalculationResponse.cs`
- [ ] `Validators/Weighing/AxleConfigurationValidator.cs` (FluentValidation)
- [ ] `Validators/Weighing/TyreTypeValidator.cs`
- [ ] `Validators/Weighing/AxleGroupValidator.cs`

**Validation Rules:**
- AxleCode: Required, max 20 chars, alphanumeric with hyphens
- AxleNumber: Range 2-8
- GvwPermissible: > 0
- LegalFramework: Enum validation (EAC, TRAFFIC_ACT)

**Acceptance:**
- All DTOs map correctly to/from entities
- FluentValidation rules enforced
- Validation errors return structured error response

#### 8. Register Services in DI Container
**File to update:** `Program.cs`
```csharp
// Axle Repositories
builder.Services.AddScoped<IAxleConfigurationRepository, AxleConfigurationRepository>();
builder.Services.AddScoped<ITyreTypeRepository, TyreTypeRepository>();
builder.Services.AddScoped<IAxleGroupRepository, AxleGroupRepository>();
builder.Services.AddScoped<IAxleFeeScheduleRepository, AxleFeeScheduleRepository>();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
```

**Acceptance:**
- All repositories resolvable from DI container
- No circular dependencies
- Application starts successfully

#### 9. Testing
- [ ] Unit tests for AxleConfigurationRepository validation logic
- [ ] Unit tests for derived config creation/update
- [ ] Integration tests for axle CRUD operations
- [ ] Integration tests for fee calculation endpoint
- [ ] Test caching behavior (cache hit/miss, invalidation)
- [ ] Test authorization (SuperAdmin vs non-admin)

**Acceptance:**
- All tests passing
- Code coverage > 80%
- Derived config validation tested thoroughly

---

## 📋 Sprint 1 Checklist Summary

| Task | Status | Priority |
|------|--------|----------|
| Auth-Service Integration | ❌ Not Started | P1 |
| Audit Logging Middleware | ❌ Not Started | P1 |
| Seed Roles & Admin User | ❌ Not Started | P1 |
| Sprint 1 Testing | ❌ Not Started | P1 |
| Master Data Repositories | ❌ Not Started | P2 |
| Axle Controllers | ❌ Not Started | P2 |
| Axle DTOs & Validation | ❌ Not Started | P2 |
| Axle Service Registration | ❌ Not Started | P2 |
| Axle Testing | ❌ Not Started | P2 |

---

## 🚀 Next Steps (In Order)

1. **Implement Audit Logging Middleware** (2-3 hours)
   - Low risk, high value, no external dependencies

2. **Seed Default Roles** (1 hour)
   - Required for testing authorization

3. **Complete Master Data Repositories** (4-5 hours)
   - TyreType, AxleGroup, AxleFeeSchedule with caching

4. **Create Axle Controllers & DTOs** (6-8 hours)
   - Full CRUD operations with validation

5. **Implement Auth-Service Integration** (8-10 hours)
   - Most complex, requires coordination with auth-service team

6. **Testing & Documentation** (8-10 hours)
   - Unit tests, integration tests, Swagger completion

**Estimated Total:** 29-37 hours remaining work

---

## ⚠️ Blockers & Dependencies

- **Auth-Service Integration:** Requires auth-service instance URL and JWKS endpoint
- **Redis Caching:** Requires Redis instance (can use local Docker for dev)
- **Admin User Seed:** Needs auth_service_user_id from centralized auth-service

---

## 📝 Notes

- Demerit points system deferred to Sprint 7 (database schema ready, implementation scheduled)
- Axle system ready for Sprint 3 weighing setup once repositories/controllers complete
- Sprint 1 user management mostly complete - focus on auth integration and audit logging
- All new code should follow existing patterns (repository pattern, DTOs, FluentValidation)

---

## 🎯 Definition of Done

**Sprint 1 Complete When:**
- ✅ All entities, repositories, controllers functional
- ✅ Auth-service integration working (login/refresh/sync)
- ✅ Audit logging captures all mutations
- ✅ Default roles & admin user seeded
- ✅ All unit & integration tests passing (>80% coverage)
- ✅ Swagger documentation complete
- ✅ Health check endpoint operational

**Axle System Complete When:**
- ✅ All repositories implemented with caching
- ✅ All controllers with CRUD operations
- ✅ DTOs & validation rules enforced
- ✅ Fee calculation endpoint functional
- ✅ Authorization enforced (admin vs public)
- ✅ All tests passing
- ✅ Ready for Sprint 3 integration
