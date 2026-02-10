using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using FluentValidation;
using FluentValidation.AspNetCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Repositories.UserManagement;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;
using TruLoad.Backend.Repositories.Auth;
using TruLoad.Backend.Repositories.Auth.Interfaces;
using TruLoad.Backend.Repositories.Weighing;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.Services.Interfaces;
using TruLoad.Backend.Services.Implementations;
using TruLoad.Backend.Services.Interfaces.Authorization;
using TruLoad.Backend.Services.Implementations.Authorization;
using TruLoad.Backend.Services.Interfaces.Auth;
using TruLoad.Backend.Services.Implementations.Auth;
using TruLoad.Backend.Data.Configurations.SystemConfiguration;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using TruLoad.Backend.Services.Implementations.Infrastructure;
using TruLoad.Backend.Data.Repositories.Infrastructure;
using TruLoad.Backend.Repositories.Infrastructure;
using TruLoad.Backend.Services.Interfaces.Weighing;
using TruLoad.Backend.Services.Implementations.Weighing;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Authorization.Handlers;
using TruLoad.Backend.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using TruLoad.Backend.Repositories.Audit;
using TruLoad.Backend.Repositories.Audit.Interfaces;
using TruLoad.Backend.Infrastructure.Security;
using TruLoad.Backend.Repositories.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;
using TruLoad.Backend.Services.Implementations.CaseManagement;
using TruLoad.Backend.Services.Interfaces.Shared;
using TruLoad.Backend.Services.Implementations.Shared;
using TruLoad.Backend.Services.Interfaces.Prosecution;
using TruLoad.Backend.Services.Implementations.Prosecution;
using TruLoad.Backend.Services.Interfaces.Financial;
using TruLoad.Backend.Services.Implementations.Financial;
using TruLoad.Backend.Services.Interfaces.Yard;
using TruLoad.Backend.Services.Implementations.Yard;
using TruLoad.Backend.Services.Interfaces.System;
using TruLoad.Backend.Services.Implementations.System;
using TruLoad.Backend.Services.Interfaces.Analytics;
using TruLoad.Backend.Services.Implementations.Analytics;
using TruLoad.Backend.Services.Interfaces.Integration;
using TruLoad.Backend.Services.Implementations.Integration;
using TruLoad.Backend.DTOs.Analytics;
using TruLoad.Backend.Configuration;

// Set QuestPDF License
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// ===== Logging =====
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// ===== Services =====
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TruLoad API",
        Version = "v1",
        Description = "Intelligent Weighing and Enforcement Solution API"
    });

    // Avoid schema ID collisions when DTOs share names across namespaces
    options.CustomSchemaIds(type => type.FullName?.Replace('.', '_'));

    // Add server URLs for dev and production environments
    options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer
    {
        Url = "http://localhost:4000",
        Description = "Development Server (Local)"
    });
    
    options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer
    {
        Url = "https://kuraweighapitest.masterspace.co.ke",
        Description = "Production Server (Testing)"
    });
    
    options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer
    {
        Url = "https://kuraweighapi.kura.go.ke",
        Description = "Production Server (KuraWeigh)"
    });

    // JWT Bearer
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // API Key
    options.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "API Key needed to access the endpoints. Example: 'X-API-KEY: {key}'",
        Name = "X-API-KEY",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        },
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            new string[] {}
        }
    });
});

// Database (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<TruLoadDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.UseVector()));

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false; // Set to true when email service is integrated
})
.AddEntityFrameworkStores<TruLoadDbContext>()
.AddDefaultTokenProviders();

// Redis (StackExchange.Redis)
var redisConnection = builder.Configuration.GetSection("Redis")["ConnectionString"]
    ?? throw new InvalidOperationException("Redis connection string not found.");

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
});

// HTTP Client Factory
builder.Services.AddHttpClient();

// Authentication & JWT (Local Token Issuance)
var jwtSecret = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT secret key not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };
});

// Authorization & Permission-Based Policies (defined in AuthorizationServiceExtensions.cs)
builder.Services.AddAuthorizationBuilder()
    .AddPermissionPolicies();

builder.Services.AddScoped<IAuthorizationHandler, PermissionRequirementHandler>();

// CORS
var allowedOrigins = builder.Configuration["CORS:AllowedOrigins"]?.Split(',')
    ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres")
    .AddRedis(redisConnection, name: "redis");

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

// Rate Limiting (Performance optimization - prevents API abuse)
builder.Services.AddTruLoadRateLimiting();

// Multi-tenant context (Organization/Station resolution from headers/claims/default)
builder.Services.AddTenantContext();

// Response Compression (Performance optimization - reduces bandwidth)
builder.Services.AddTruLoadResponseCompression();

// Repositories (User/Role now handled by Identity UserManager/RoleManager)
builder.Services.AddScoped<IOrganizationRepository, TruLoad.Backend.Repositories.UserManagement.Repositories.OrganizationRepository>();
builder.Services.AddScoped<IDepartmentRepository, TruLoad.Backend.Repositories.UserManagement.DepartmentRepository>();
builder.Services.AddScoped<IStationRepository, TruLoad.Backend.Repositories.UserManagement.Repositories.StationRepository>();
builder.Services.AddScoped<IWorkShiftRepository, TruLoad.Backend.Repositories.UserManagement.Repositories.WorkShiftRepository>();
builder.Services.AddScoped<IUserShiftRepository, TruLoad.Backend.Repositories.UserManagement.Repositories.UserShiftRepository>();
builder.Services.AddScoped<IShiftRotationRepository, TruLoad.Backend.Repositories.UserManagement.Repositories.ShiftRotationRepository>();
builder.Services.AddScoped<IRotationShiftRepository, TruLoad.Backend.Repositories.UserManagement.Repositories.RotationShiftRepository>();

// Infrastructure services
builder.Services.AddSingleton<PasswordHasher>();

// Permission repositories
builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();

// Axle repositories
builder.Services.AddScoped<IAxleConfigurationRepository, AxleConfigurationRepository>();

// Audit repositories
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

builder.Services.AddScoped<IAxleWeightReferenceRepository, AxleWeightReferenceRepository>();
builder.Services.AddScoped<ITyreTypeRepository, TyreTypeRepository>();
builder.Services.AddScoped<IAxleGroupRepository, AxleGroupRepository>();
builder.Services.AddScoped<IAxleFeeScheduleRepository, AxleFeeScheduleRepository>();
builder.Services.AddScoped<IToleranceRepository, ToleranceRepository>();
builder.Services.AddScoped<IWeighingRepository, WeighingRepository>();
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<ITransporterRepository, TransporterRepository>();
builder.Services.AddScoped<IPermitRepository, PermitRepository>();
builder.Services.AddScoped<IProhibitionRepository, ProhibitionRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IScaleTestRepository, ScaleTestRepository>();
builder.Services.AddScoped<ICargoTypesRepository, CargoTypesRepository>();
builder.Services.AddScoped<IOriginsDestinationsRepository, OriginsDestinationsRepository>();
builder.Services.AddScoped<IRoadsRepository, RoadsRepository>();
builder.Services.AddScoped<IVehicleMakesRepository, VehicleMakesRepository>();

// Infrastructure services (File Storage with SHA-256 checksums)
builder.Services.AddScoped<IPdfService, QuestPdfService>();
builder.Services.AddScoped<IBlobStorageService, LocalBlobStorageService>();

// Notification Service Configuration
builder.Services.Configure<NotificationServiceOptions>(
    builder.Configuration.GetSection(NotificationServiceOptions.SectionName));

// Shared Notification Service (HTTP client integration with Go notifications-service)
builder.Services.AddHttpClient<INotificationService, NotificationService>();

// Permission services
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IPermissionVerificationService, PermissionVerificationService>();
builder.Services.AddScoped<IOwnershipCheckService, OwnershipCheckService>();

// Status lookup service (centralized cached lookups for status/type entities)
builder.Services.AddScoped<IStatusLookupService, StatusLookupService>();

// Auth services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IWeighingService, WeighingService>();
builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();

// Sprint 11: Axle Group Aggregation & Fee/Demerit Services
builder.Services.AddScoped<IAxleTypeFeeRepository, AxleTypeFeeRepository>();
builder.Services.AddScoped<IDemeritPointsRepository, DemeritPointsRepository>();
builder.Services.AddScoped<IAxleGroupAggregationService, AxleGroupAggregationService>();

// Case Management repositories
builder.Services.AddScoped<ICaseRegisterRepository, CaseRegisterRepository>();
builder.Services.AddScoped<ISpecialReleaseRepository, SpecialReleaseRepository>();

// Case Management services
builder.Services.AddScoped<ICaseRegisterService, CaseRegisterService>();
builder.Services.AddScoped<ISpecialReleaseService, SpecialReleaseService>();
builder.Services.AddScoped<ICourtHearingService, CourtHearingService>();
builder.Services.AddScoped<ICourtService, CourtService>();
builder.Services.AddScoped<ICaseSubfileService, CaseSubfileService>();
builder.Services.AddScoped<ICasePartyService, CasePartyService>();
builder.Services.AddScoped<IArrestWarrantService, ArrestWarrantService>();
builder.Services.AddScoped<ICaseClosureChecklistService, CaseClosureChecklistService>();
builder.Services.AddScoped<ICaseAssignmentLogService, CaseAssignmentLogService>();
builder.Services.AddScoped<ILoadCorrectionMemoService, LoadCorrectionMemoService>();
builder.Services.AddScoped<IComplianceCertificateService, ComplianceCertificateService>();

// Prosecution services
builder.Services.AddScoped<IProsecutionService, ProsecutionService>();

// Financial services
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();

// Integration & Payment services (Sprint 15: eCitizen/Pesaflow)
builder.Services.AddSingleton<IEncryptionService, AesGcmEncryptionService>();
builder.Services.AddScoped<IIntegrationConfigService, IntegrationConfigService>();
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
    StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddHttpClient<IECitizenService, ECitizenService>(c =>
    c.Timeout = TimeSpan.FromSeconds(30));

// KeNHA & NTSA integration services
builder.Services.AddHttpClient<IKeNHAService, KeNHAService>(c =>
    c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient<INTSAService, NTSAService>(c =>
    c.Timeout = TimeSpan.FromSeconds(30));

// Yard services
builder.Services.AddScoped<IYardService, YardService>();
builder.Services.AddScoped<IVehicleTagService, VehicleTagService>();

// System Settings services
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IBackupService, BackupService>();

// Analytics services (Superset integration)
builder.Services.Configure<SupersetOptions>(builder.Configuration.GetSection(SupersetOptions.SectionName));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.AddHttpClient<ISupersetService, SupersetService>();

// TODO: Add MediatR, AutoMapper, MassTransit (RabbitMQ), etc.

// ===== App Configuration =====
var app = builder.Build();

// Apply pending migrations and seed database automatically on startup
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // Ensure pgvector extension is enabled (idempotent)
        Log.Information("Checking pgvector extension...");
        dbContext.EnsurePgVectorExtension();
        Log.Information("✓ pgvector extension verified");

        var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();

        if (pendingMigrations.Any())
        {
            Log.Information("Applying {Count} pending migrations...", pendingMigrations.Count);
            dbContext.Database.Migrate();
            Log.Information("✓ Migrations applied successfully");
        }
        else
        {
            Log.Information("✓ Database is up to date (no pending migrations)");
        }

        // Check if initial seeding has already been completed
        var seedingVersion = 5; // Increment this when you need to re-seed (v5: Added MIDDLEWARE_SERVICE role + middleware@truconnect.local user)
        var seedingName = "InitialSeed";

        var existingSeed = await dbContext.DatabaseSeedingHistory
            .AsNoTracking()
            .Where(s => s.SeedingName == seedingName && s.Version == seedingVersion && s.IsCompleted)
            .FirstOrDefaultAsync();

        if (existingSeed != null)
        {
            Log.Information("✓ Database seeding already completed (Version {Version} on {Date})",
                existingSeed.Version, existingSeed.CreatedAt);
        }
        else
        {
            // Run idempotent seeder - get required Identity managers from DI
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var startTime = DateTime.UtcNow;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            Log.Information("Starting database seeding (Version {Version})...", seedingVersion);
            await TruLoad.Data.Seeders.DatabaseSeeder.SeedAsync(dbContext, roleManager, userManager, logger);

            sw.Stop();

            // Record successful seeding
            var seedingRecord = new TruLoad.Backend.Models.Infrastructure.DatabaseSeedingHistory
            {
                SeedingName = seedingName,
                Version = seedingVersion,
                IsCompleted = true,
                DurationMs = sw.ElapsedMilliseconds,
                Notes = $"Initial database seeding completed",
                CreatedAt = startTime,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.DatabaseSeedingHistory.Add(seedingRecord);
            await dbContext.SaveChangesAsync();

            Log.Information("✓ Database seeding completed in {Duration}ms", sw.ElapsedMilliseconds);
        }

        // Sprint 15: Seed IntegrationConfig for eCitizen/Pesaflow (Development only)
        if (app.Environment.IsDevelopment())
        {
            var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
            var integrationLogger = scope.ServiceProvider.GetRequiredService<ILogger<TruLoad.Backend.Data.Seeders.SystemConfiguration.IntegrationConfigSeeder>>();
            var integrationSeeder = new TruLoad.Backend.Data.Seeders.SystemConfiguration.IntegrationConfigSeeder(
                dbContext, encryptionService, builder.Configuration, integrationLogger);
            await integrationSeeder.SeedAsync();
            Log.Information("✓ Integration config seeding completed");
        }
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to apply database migrations or seeding");
    // Don't fail startup if migrations fail - let health check handle it
}

// Response Compression - MUST be first for all downstream responses
app.UseTruLoadResponseCompression();

// Swagger (all environments for now; restrict to dev/staging later)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TruLoad API v1");
    c.RoutePrefix = "v1/docs"; // Serve Swagger UI at /v1/docs
});

// Support legacy Swagger URLS (/swagger and /swagger/index.html)
app.MapGet("/swagger", () => Results.Redirect("/v1/docs", false));
app.MapGet("/swagger/index.html", () => Results.Redirect("/v1/docs", false));

app.UseSerilogRequestLogging();

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception occurred");

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var errorResponse = new
        {
            error = new
            {
                code = "INTERNAL_SERVER_ERROR",
                message = "An unexpected error occurred",
                details = app.Environment.IsDevelopment() ? exception?.Message : null,
                traceId = context.TraceIdentifier,
                timestamp = DateTime.UtcNow
            }
        };

        await context.Response.WriteAsJsonAsync(errorResponse);
    });
});

// Audit middleware
app.UseAuditMiddleware();

app.UseCors();

// Rate Limiting - after CORS, before authentication
app.UseTruLoadRateLimiting();

app.UseAuthentication();

// Tenant context middleware - resolves org/station from headers/claims/default
// Must be after authentication (needs user claims) and before authorization
app.UseTenantContext();

app.UseAuthorization();

app.MapControllers();

// Health endpoint
app.MapHealthChecks("/health");

// Minimal test endpoint
app.MapGet("/api/v1/ping", () => Results.Ok(new { message = "TruLoad API is running", timestamp = DateTime.UtcNow }))
    .WithTags("System");

try
{
    Log.Information("Starting TruLoad Backend API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
