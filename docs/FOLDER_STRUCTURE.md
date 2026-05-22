# TruLoad Backend Folder Structure

**Last Updated:** January 6, 2026  
**Purpose:** Document folder organization to prevent duplication and maintain consistency

---

## Recent Updates (January 6, 2026)

### ✅ Completed (This Session)
- **Fixed AxleFeeSchedule schema**: Created AxleFeeScheduleTypeConfiguration.cs with proper table mapping and constraints
- **Created EF Core migration**: AddAxleFeeScheduleTable to create axle_fee_schedules table with updated_at column
- **Registered configuration**: Updated WeighingModuleDbContextConfiguration to apply AxleFeeSchedule configuration
- **Seed data verified**: Confirmed axle-seed-data.json contains comprehensive fee schedules (10 entries covering EAC and Traffic Act)
- **Build & tests passing**: 0 compilation errors, all 80 unit tests pass

### ✅ Previously Completed
- Fixed repository organization: Moved implementations from `Repositories/Weighing/Interfaces/` to `Repositories/Weighing/`
- Removed duplicate StationsController (kept UserManagement version)
- Added 3 new reference data repositories (Infrastructure/)
- Added 3 new reference data controllers (WeighingOperations/)
- Added 3 new reference data seeders (WeighingOperations/)
- Proper separation: Interfaces in `Interfaces/` subfolder, implementations in parent folder

---

## Root Level Structure

```
truload-backend/
├── Authorization/          # Permission policies, handlers, requirements
├── Controllers/            # API controllers organized by module
├── Data/                   # DbContext, entity configurations, seeders
├── DTOs/                   # Data transfer objects organized by module
├── Infrastructure/         # Cross-cutting concerns (caching, file storage)
├── Middleware/            # Custom middleware (audit logging, exception handling)
├── Migrations/            # EF Core database migrations (auto-generated)
├── Models/                # Domain entities organized by module
├── Repositories/          # Data access layer organized by module
├── Services/              # Business logic services
├── Shared/                # Shared utilities and helpers
├── Tests/                 # Unit and integration tests
├── Validators/            # FluentValidation validators
├── wwwroot/               # Static files (logos, images)
├── docs/                  # Documentation and sprint files
├── scripts/               # Utility scripts
└── KubeSecrets/           # Kubernetes secrets templates
```

---

## Data Folder (DbContext & Configurations)

**Location:** `Data/`

```
Data/
├── TruLoadDbContext.cs              # Main EF Core DbContext
├── Configurations/                  # Entity type configurations (Fluent API)
│   ├── AxleConfiguration/
│   │   └── AxleConfigurationTypeConfiguration.cs
│   ├── SystemConfiguration/
│   ├── Traffic/
│   ├── UserManagement/
│   │   ├── RoleTypeConfiguration.cs
│   │   ├── UserTypeConfiguration.cs
│   │   └── ...
│   └── Weighing/
│       ├── AxleFeeScheduleTypeConfiguration.cs  # Fee schedules (EAC, Traffic Act)
│       ├── PermitTypeConfiguration.cs
│       ├── VehicleTypeConfiguration.cs
│       ├── WeighingTransactionTypeConfiguration.cs
│       └── ...
└── Seeders/                         # Data seeders for reference data
    ├── DatabaseSeeder.cs            # Master seeder orchestrator
    ├── PermissionSeeder.cs
    ├── RoleSeeder.cs
    ├── RolePermissionSeeder.cs
    ├── SystemConfiguration/
    │   └── SystemConfigurationSeeder.cs
    ├── UserManagement/
    │   ├── UserManagementSeeder.cs
    │   └── UserSeeder.cs
    └── WeighingOperations/
        ├── WeighingOperationsSeeder.cs  # Axle configs, weight refs, fees
        ├── axle-seed-data.json          # JSON seed file for axles
        ├── CargoTypesSeeder.cs          # 14 cargo types (General, Hazardous, Perishable)
        ├── OriginsDestinationsSeeder.cs # 15 locations (Kenya, Uganda, Tanzania, Rwanda)
        └── RoadsSeeder.cs               # 13 roads (Classes A, B, C, D, E)
```

**RULES:**
- ❌ **DO NOT** create `Migrations/` folder inside `Data/`
- ❌ **DO NOT** create repository classes inside `Data/`
- ✅ Entity configurations go in `Data/Configurations/{Module}/`
- ✅ Seeders go in `Data/Seeders/{Module}/`
- ✅ Only DbContext and configuration classes belong in `Data/`

---

## Migrations Folder (EF Core Migrations)

**Location:** `Migrations/` (root level, NOT in Data/)

```
Migrations/
├── 20251221190703_InitialIdentityMigration.cs
├── 20251227141300_AddPermissionsModel.cs
├── 20251227155649_AddAxleConfigurations.cs
├── 20251231084029_AddWeighingCore.cs
└── TruLoadDbContextModelSnapshot.cs
```

**RULES:**
- ✅ Auto-generated by `dotnet ef migrations add {Name}`
- ❌ **DO NOT** manually create files here
- ✅ Migrations are at root level, NOT in `Data/Migrations/`

---

## Models Folder (Domain Entities)

**Location:** `Models/`

```
Models/
├── IdenCargoTypes.cs       # NEW: Cargo type taxonomy
│   ├── Counties.cs
│   ├── Districts.cs
│   ├── Document.cs
│   ├── OriginsDestinations.cs  # NEW: Origin/destination master data
│   ├── Permission.cs
│   ├── Roads.cs            # NEW: Road master data
│   ├── RolePermission.cs
│   ├── ScaleTests.cs       # NEW: Scale calibration tests
│   └── LocalBlob.cs
├── Shifts/
│   ├── RotationShift.cs
│   ├── ShiftRotation.cs
│   ├── UserShift.cs
│   ├── WorkShift.cs
│   └── WorkShiftSchedule.cs
├── System/
│   ├── ActDefinition.cs
│   ├── AxleConfiguration.cs
│   ├── AxleFeeSchedule.cs
│   ├── AxleGroup.cs
│   ├── AxleWeightReference.cs
│   ├── Department.cs
│   ├── Organization.cs
│   ├── PermitType.cs
│   ├── Station.cs
│   ├── ToleranceSetting.cs
│   └── TyreType.cs
├── Traffic/
│   ├── Driver.cs
│   └── DriverDemeritRecord.cs
└── Weighing/
    ├── Permit.cs
    ├── ProhibitionOrder.cs
    ├── Transporter.cs
    ├── Vehicle.cs
    ├── VehicleOwner.cs
    ├── WeighingAxle.cs
    └── WeighingTransaction.csd.cs
    ├── Transporter.cs
    ├── Vehicle.cs
    ├── VehicleOwner.cs
    ├── WeighingTransaction.cs
    └── ...
```

**RULES:**
- ✅ Entities organized by domain module
- ✅ Navigation properties defined here
- ✅ Data annotations for simple constraints only
- ✅ Complex constraints go in `Data/Configurations/`
nterfaces/
│   │   └── IAuditLogRepository.cs
│   └── AuditLogRepository.cs
├── Auth/
│   ├── Interfaces/
│   │   └── IPermissionRepository.cs
│   └── PermissionRepository.cs
├── Infrastructure/
│   ├── Interfaces/
│   │   ├── ICargoTypesRepository.cs       # NEW: Cargo types data access
│   │   ├── ILocalBlobRepository.cs
│   │   ├── IOriginsDestinationsRepository.cs  # NEW: Origins/destinations data access
│   │   ├── IRoadsRepository.cs            # NEW: Roads data access
│   │   └── IScaleTestRepository.cs        # NEW: Scale test data access
│   ├── CargoTypesRepository.cs
│   ├── LocalBlobRepository.cs
│   ├── OriginsDestinationsRepository.cs
│   ├── RoadsRepository.cs
│   └── ScaleTestRepository.cs
├── UserManagement/
│   ├── Interfaces/
│   │   ├── IRoleRepository.cs
│   │   └── IUserRepository.cs
│   ├── RoleRepository.cs
│   └── UserRepository.cs
└── Weighing/
    ├── Interfaces/
    │   ├── IDriverRepository.cs
    │   ├── IPermitRepository.cs
    │   ├── IProhibitionRepository.cs
    │   ├── IVehicleRepository.cs
    │   └── IWeighingRepository.cs
    ├── DriverRepository.cs
    ├── PermitRepository.cs
    ├── ProhibitionRepository.cs
    ├── VehicleRepository.cs
    └── WeighingRepository.cs
```

**RULES:**Controller.cs
│   └── PermissionsController.cs
├── System/
│   └── SystemController.cs
├── UserManagement/
│   ├── DepartmentsController.cs
│   ├── OrganizationsController.cs
│   ├── RolesController.cs
│   ├── StationsController.cs         # Station management CRUD
│   ├── UsersController.cs
│   └── WorkShiftsController.cs
└── WeighingOperations/
    ├── AxleConfigurationsController.cs
    ├── CargoTypesController.cs       # NEW: Cargo types CRUD
    ├── DriversController.cs
    ├── OriginsDestinationsController.cs  # NEW: Origins/destinations CRUD
    ├── RoadsController.cs            # NEW: Roads CRUD
    ├── ScaleTestsController.cs       # NEW: Scale tests/calibration CRUD
    └── VehiclesController.cs
```

**RULES:**
- ✅ Controllers organized by functional area
- ✅ Base route: `/api/v1/{module}/{resource}`
- ✅ Authorization policies applied via `[Authorize(Policy = "...")]`
- ✅ Return ActionResult<T> for strongly-typed responses
- ❌ **DO NOT** duplicate controllers across folders (StationsController is in UserManagement ONLY)

**Location:** `Controllers/`

```
Controllers/
├── Audit/
│   └── AuditLogController.cs
├── Auth/
│   ├── AuthenticationController.cs
│   └── PermissionsController.cs
├── System/
│   └── SystemController.cs
├── UserManagement/
│   ├── RolesController.cs
│   ├── ShiftsController.cs
│   └── UsersController.cs
└── WeighingOperations/
    ├── AxleConfigurationsController.cs
    ├── DriversController.cs
    ├── VehiclesController.cs
    └── WeighingTransactionsController.cs
```

**RULES:**
- ✅ Controllers organized by functional area
- ✅ Base route: `/api/v1/{module}/{resource}`
- ✅ Authorization policies applied via `[Authorize(Policy = "...")]`
- ✅ Return ActionResult<T> for strongly-typed responses

---

## Services Folder (Business Logic)

**Location:** `Services/`

```
Services/
├── Background/                          # ASP.NET Core IHostedService (long-lived, event-driven)
│   └── SubscriptionCacheInvalidationService.cs  # NATS → Redis cache invalidation
├── Implementations/
│   ├── Auth/
│   │   ├── PermissionService.cs
│   │   └── PermissionVerificationService.cs
│   ├── Caching/
│   │   └── RedisCacheService.cs
│   ├── Documents/
│   │   └── QuestPdfService.cs
│   ├── Infrastructure/
│   │   └── LocalBlobService.cs
│   └── Weighing/
│       ├── ComplianceService.cs
│       └── WeighingService.cs
└── Interfaces/
    ├── Auth/
    │   ├── IPermissionService.cs
    │   └── IPermissionVerificationService.cs
    ├── Caching/
    │   └── IRedisCacheService.cs
    ├── Documents/
    │   └── IQuestPdfService.cs
    ├── Infrastructure/
    │   └── ILocalBlobService.cs
    └── Weighing/
        ├── IComplianceService.cs
        └── IWeighingService.cs
```

**`Services/Background/` rules:**
- Only ASP.NET Core `BackgroundService` / `IHostedService` implementations live here — **not** Hangfire jobs
- Hangfire jobs go in `Services/Implementations/Jobs/` or a `Jobs/` folder at root level
- Background services are registered via `builder.Services.AddHostedService<T>()` in `Program.cs`
- Use `IServiceScopeFactory` to resolve scoped services (e.g., `TruLoadDbContext`) — never inject scoped services directly into a singleton `BackgroundService`

**RULES:**
- ✅ Interfaces in `Services/Interfaces/{Module}/`
- ✅ Implementations in `Services/Implementations/{Module}/`
- ✅ Business logic lives in services, NOT controllers or repositories

---

## DTOs Folder (Data Transfer Objects)

**Location:** `DTOs/` (to be created when needed)

```
DTOs/
├── Auth/
│   ├── LoginRequest.cs
│   ├── TokenResponse.cs
│   └── ...
├── UserManagement/
│   ├── CreateUserRequest.cs
│   ├── UpdateUserRequest.cs
│   ├── UserResponse.cs
│   └── ...
└── Weighing/
    ├── CreateVehicleRequest.cs
    ├── WeighingTransactionRequest.cs
    ├── WeighingTransactionResponse.cs
    └── ...
```

**RULES:**
- ✅ DTOs organized by module
- ✅ Request/Response suffixes for clarity
- ✅ Use DTOs for API contracts, NOT domain entities directly

---

## Key Principles

1. **No Duplication:** Check existing folders before creating new ones
2. **Module Organization:** Group by functional area (Auth, UserManagement, Weighing, etc.)
3. **Separation of Concerns:**
   - Models = Domain entities
   - Data = DbContext + Configurations + Seeders
   - Repositories = Data access
   - Services = Business logic
   - Controllers = API endpoints
   - DTOs = API contracts
4. **Migrations:** Auto-generated at root level, never manually created
5. **Naming Conventions:**
   - Interfaces: `I{Name}.cs`
   - Repositories: `{Entity}Repository.cs`
   - Controllers: `{Resource}Controller.cs`
   - Services: `{Domain}Service.cs`

---

## Adding New Features Checklist

When adding new features (e.g., Station Management):

1. ✅ Create entity in `Models/Weighing/Station.cs`
2. ✅ Create configuration in `Data/Configurations/Weighing/StationTypeConfiguration.cs`
3. ✅ Add DbSet to `Data/TruLoadDbContext.cs`
4. ✅ Create migration: `dotnet ef migrations add AddStation`
5. ✅ Create interface in `Repositories/Weighing/IStationRepository.cs`
6. ✅ Create repository in `Repositories/Weighing/StationRepository.cs`
7. ✅ Create service interface in `Services/Interfaces/Weighing/IStationService.cs`
8. ✅ Create service in `Services/Implementations/Weighing/StationService.cs`
9. ✅ Create DTOs in `DTOs/Weighing/Station*.cs`
10. ✅ Create controller in `Controllers/WeighingOperations/StationsController.cs`
11. ✅ Create seeder in `Data/Seeders/WeighingOperations/StationSeeder.cs`
12. ✅ Register services in `Program.cs`

---

**This document must be updated when new patterns emerge.**
