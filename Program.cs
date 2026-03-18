using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.PostgreSql;
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
using TruLoad.Backend.Services.Interfaces.Reporting;
using TruLoad.Backend.Services.Implementations.Reporting;
using TruLoad.Backend.Services.BackgroundJobs;
using TruLoad.Backend.Services.Implementations.Reporting.Modules;
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
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new TruLoad.Backend.Json.NullableDateTimeJsonConverter());
    });
builder.Services.AddMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TruLoad API",
        Version = "v1",
        Description = "Intelligent Weighing and Enforcement Solution API\n\n[Hangfire Dashboard](/hangfire)"
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

    // Separate security requirements = OR logic (Bearer OR ApiKey)
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
        }
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
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

// Media upload (branding logos/images). Production: set Media__StoragePath to tuload-backend-media mount (e.g. /mnt/tuload-backend-media).
builder.Services.Configure<MediaUploadOptions>(options =>
{
    builder.Configuration.GetSection(MediaUploadOptions.SectionName).Bind(options);
    var envPath = Environment.GetEnvironmentVariable("MEDIA_STORAGE_PATH");
    if (!string.IsNullOrWhiteSpace(envPath)) options.StoragePath = envPath.Trim();
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
    // Disable built-in password validators — DynamicPasswordValidator reads policy from DB
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 1;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

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

// Dynamic password policy from DB settings (replaces static Identity password options)
builder.Services.AddScoped<IPasswordValidator<ApplicationUser>, DynamicPasswordValidator>();

// Cookie auth for Hangfire dashboard (browser-based login)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/hangfire/login";
    options.Cookie.Name = "TruLoad.Auth";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
});

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

    // SignalR sends JWT as query parameter for WebSocket connections
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Authorization & Permission-Based Policies (defined in AuthorizationServiceExtensions.cs)
builder.Services.AddAuthorizationBuilder()
    .AddPermissionPolicies();

builder.Services.AddScoped<IAuthorizationHandler, PermissionRequirementHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

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
builder.Services.AddSingleton<RateLimitSettings>();
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

// Document numbering service (Sprint 22)
builder.Services.AddScoped<IDocumentNumberService, DocumentNumberService>();

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
builder.Services.AddScoped<ICaseDocumentService, CaseDocumentService>();
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
builder.Services.AddScoped<ICurrencyService, CurrencyService>();
builder.Services.AddSingleton<ExchangeRateSyncJob>();

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
builder.Services.AddScoped<IActConfigurationService, ActConfigurationService>();
builder.Services.AddScoped<IBackupService, BackupService>();

// Materialized view refresh + partition lifecycle management
builder.Services.AddScoped<IMaterializedViewService, MaterializedViewService>();
builder.Services.AddScoped<TruLoad.Backend.Services.BackgroundJobs.MaterializedViewRefreshJob>();

// Analytics services (Superset integration)
builder.Services.Configure<SupersetOptions>(builder.Configuration.GetSection(SupersetOptions.SectionName));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.AddHttpClient<ISupersetService, SupersetService>();

// SignalR for real-time analytics
builder.Services.AddSignalR();

// Reporting services (modular per-module report generation)
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IModuleReportGenerator, WeighingReportGenerator>();
builder.Services.AddScoped<IModuleReportGenerator, ProsecutionReportGenerator>();
builder.Services.AddScoped<IModuleReportGenerator, CaseReportGenerator>();
builder.Services.AddScoped<IModuleReportGenerator, FinancialReportGenerator>();
builder.Services.AddScoped<IModuleReportGenerator, YardReportGenerator>();
builder.Services.AddScoped<IModuleReportGenerator, SecurityReportGenerator>();

// ===== Hangfire Background Jobs =====
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(c =>
        c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")))
    .WithJobExpirationTimeout(TimeSpan.FromHours(48)));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production" ? 10 : 5;
    options.Queues = new[] { "critical", "default", "payments" };
    options.ServerName = $"TruLoad-{Environment.MachineName}";
});

// Register background job services
builder.Services.AddScoped<TruLoad.Backend.Services.BackgroundJobs.PesaflowInvoiceSyncJob>();
builder.Services.AddScoped<TruLoad.Backend.Services.Implementations.Shared.NotificationBackgroundJob>();

// Hangfire job retention: auto-delete succeeded/failed jobs after 48 hours
GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 3 });

// ===== App Configuration =====
var app = builder.Build();

// Apply pending migrations and seed database automatically on startup
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        Log.Information("Checking pending migrations...");

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
        var seedingVersion = 13; // Increment this when you need to re-seed
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

        // Seed IntegrationConfig for eCitizen/Pesaflow (all environments)
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var integrationLogger = scope.ServiceProvider.GetRequiredService<ILogger<TruLoad.Backend.Data.Seeders.SystemConfiguration.IntegrationConfigSeeder>>();
        var integrationSeeder = new TruLoad.Backend.Data.Seeders.SystemConfiguration.IntegrationConfigSeeder(
            dbContext, encryptionService, builder.Configuration, integrationLogger);
        await integrationSeeder.SeedAsync();
        Log.Information("✓ Integration config seeding completed");

        // Initialize materialized views on startup
        try
        {
            var mvService = scope.ServiceProvider.GetRequiredService<IMaterializedViewService>();
            await mvService.RefreshAllAsync();
            Log.Information("✓ Materialized views refreshed on startup");
        }
        catch (Exception mvEx)
        {
            Log.Warning(mvEx, "Failed to refresh materialized views on startup — will retry via Hangfire");
        }
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to apply database migrations or seeding");
    // Don't fail startup if migrations fail - let health check handle it
}

// Load rate limit settings from DB (uses defaults if DB unavailable)
try
{
    await RateLimitingConfiguration.LoadRateLimitSettingsFromDbAsync(app.Services);
    Log.Information("✓ Rate limit settings loaded from database");
}
catch (Exception ex)
{
    Log.Warning(ex, "Failed to load rate limit settings from DB, using defaults");
}

// Response Compression - MUST be first for all downstream responses
app.UseTruLoadResponseCompression();

// Ensure media directory exists and is writable (avoid permission errors on first upload).
var mediaOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<MediaUploadOptions>>().Value;
var mediaPath = mediaOptions.StoragePath;
if (!Path.IsPathRooted(mediaPath)) mediaPath = Path.Combine(app.Environment.ContentRootPath, mediaPath);
try
{
    if (!Directory.Exists(mediaPath)) Directory.CreateDirectory(mediaPath);
    var testFile = Path.Combine(mediaPath, ".write-test");
    global::System.IO.File.WriteAllText(testFile, "");
    global::System.IO.File.Delete(testFile);
    Log.Information("Media directory ready: {MediaPath}", mediaPath);
}
catch (Exception ex)
{
    Log.Warning(ex, "Media directory not writable at {MediaPath}. Uploads will fail until permissions are fixed (see docs/MEDIA_STORAGE.md).", mediaPath);
}
if (Directory.Exists(mediaPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(mediaPath),
        RequestPath = "/media"
    });
}

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

// CORS must be before exception handler so error responses include CORS headers
app.UseCors();

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception occurred: {ExceptionDetails}", exception?.ToString());

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var errorResponse = new
        {
            error = new
            {
                code = "INTERNAL_SERVER_ERROR",
                message = "An unexpected error occurred",
                details = app.Environment.IsDevelopment() ? exception?.ToString() : null,
                traceId = context.TraceIdentifier,
                timestamp = DateTime.UtcNow
            }
        };

        await context.Response.WriteAsJsonAsync(errorResponse);
    });
});



// Rate Limiting - after CORS, before authentication
app.UseTruLoadRateLimiting();

app.UseAuthentication();

// Tenant context middleware - resolves org/station from headers/claims/default
// Must be after authentication (needs user claims) and before authorization
app.UseTenantContext();

app.UseAuthorization();

// Audit middleware - must be after Authentication to pick up user claims
app.UseAuditMiddleware();

// Hangfire cookie auth middleware - intercepts /hangfire requests and authenticates
// via Identity cookie (since default scheme is JWT Bearer, cookies aren't checked automatically)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hangfire") &&
        !context.Request.Path.StartsWithSegments("/hangfire/login"))
    {
        var authResult = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!authResult.Succeeded)
        {
            var returnUrl = context.Request.Path + context.Request.QueryString;
            context.Response.Redirect($"/hangfire/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return; // Short-circuit before Hangfire can set 401
        }
        context.User = authResult.Principal!;
    }
    await next();
});

app.MapControllers();

// SignalR hubs
app.MapHub<TruLoad.Backend.Hubs.AnalyticsHub>("/hubs/analytics");

// Hangfire Dashboard (admin access only)
app.MapHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
{
    Authorization = new[] { new TruLoad.Backend.Infrastructure.Authorization.HangfireAuthorizationFilter() },
    DashboardTitle = "TruLoad Background Jobs"
});

// Schedule recurring jobs
Hangfire.RecurringJob.AddOrUpdate<TruLoad.Backend.Services.BackgroundJobs.PesaflowInvoiceSyncJob>(
    "pesaflow-invoice-sync",
    job => job.ExecuteAsync(default),
    "*/15 * * * *", // Every 15 minutes
    new Hangfire.RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc,
        QueueName = "payments"
    });

Hangfire.RecurringJob.AddOrUpdate<ExchangeRateSyncJob>(
    "exchange-rate-sync",
    job => job.ExecuteAsync(),
    "0 0 * * *", // Daily at midnight UTC
    new Hangfire.RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc,
        QueueName = "default"
    });

Hangfire.RecurringJob.AddOrUpdate<TruLoad.Backend.Services.BackgroundJobs.BackupScheduleJob>(
    "automated-database-backup",
    job => job.ExecuteAsync(),
    "0 2 * * *", // Daily at 2 AM UTC (configurable via settings)
    new Hangfire.RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc,
        QueueName = "default"
    });

Hangfire.RecurringJob.AddOrUpdate<TruLoad.Backend.Services.BackgroundJobs.MaterializedViewRefreshJob>(
    "mv-refresh",
    job => job.ExecuteAsync(default),
    "*/30 * * * *", // Every 30 minutes
    new Hangfire.RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc,
        QueueName = "default"
    });


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
