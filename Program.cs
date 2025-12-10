using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using FluentValidation;
using FluentValidation.AspNetCore;
using truload_backend.Data;
using TruLoad.Backend.Repositories.UserManagement;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;
using TruLoad.Backend.Repositories.Auth;
using TruLoad.Backend.Repositories.Auth.Interfaces;
using TruLoad.Backend.Repositories.Weighing;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Services.Interfaces;
using TruLoad.Backend.Services.Implementations;
using TruLoad.Backend.Services.Interfaces.Authorization;
using TruLoad.Backend.Services.Implementations.Authorization;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Authorization.Handlers;
using TruLoad.Backend.Authorization.Policies;
using Microsoft.AspNetCore.Authorization;
using TruLoad.Backend.Repositories.Audit;
using TruLoad.Backend.Repositories.Audit.Interfaces;
using TruLoad.Backend.Infrastructure.Security;

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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TruLoad API",
        Version = "v1",
        Description = "Intelligent Weighing and Enforcement Solution API"
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
    options.UseNpgsql(connectionString));

// Redis (StackExchange.Redis)
var redisConnection = builder.Configuration.GetSection("Redis")["ConnectionString"]
    ?? throw new InvalidOperationException("Redis connection string not found.");

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
});

// Authentication & JWT
var authority = builder.Configuration["Authentication:Authority"]
    ?? throw new InvalidOperationException("Authentication Authority not configured.");
var audience = builder.Configuration["Authentication:Audience"]
    ?? throw new InvalidOperationException("Authentication Audience not configured.");
var requireHttpsMetadata = bool.Parse(builder.Configuration["Authentication:RequireHttpsMetadata"] ?? "true");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = requireHttpsMetadata;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Authorization & Permission-Based Policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Permission:system.view_config", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new TruLoad.Backend.Authorization.Requirements.PermissionRequirement("system.view_config")))
    .AddPolicy("Permission:system.manage_roles", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new TruLoad.Backend.Authorization.Requirements.PermissionRequirement("system.manage_roles")))
    .AddPolicy("Permission:user.create", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new TruLoad.Backend.Authorization.Requirements.PermissionRequirement("user.create")))
    .AddPolicy("Permission:user.update", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new TruLoad.Backend.Authorization.Requirements.PermissionRequirement("user.update")))
    .AddPolicy("Permission:user.delete", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new TruLoad.Backend.Authorization.Requirements.PermissionRequirement("user.delete")))
    .AddPolicy("Permission:system.audit_logs", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new TruLoad.Backend.Authorization.Requirements.PermissionRequirement("system.audit_logs")));

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

// Repositories
builder.Services.AddScoped<IUserRepository, TruLoad.Backend.Repositories.UserManagement.Repositories.UserRepository>();
builder.Services.AddScoped<IOrganizationRepository, TruLoad.Backend.Repositories.UserManagement.Repositories.OrganizationRepository>();
builder.Services.AddScoped<IRoleRepository, TruLoad.Backend.Repositories.UserManagement.Repositories.RoleRepository>();
builder.Services.AddScoped<IDepartmentRepository, TruLoad.Backend.Repositories.UserManagement.DepartmentRepository>();
builder.Services.AddScoped<IStationRepository, TruLoad.Backend.Repositories.UserManagement.Repositories.StationRepository>();
builder.Services.AddScoped<IWorkShiftRepository, TruLoad.Backend.Repositories.UserManagement.Repositories.WorkShiftRepository>();

// Infrastructure services
builder.Services.AddSingleton<PasswordHasher>();

// Permission repositories
builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();

// Axle repositories

// Audit repositories
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

builder.Services.AddScoped<IAxleWeightReferenceRepository, AxleWeightReferenceRepository>();
builder.Services.AddScoped<ITyreTypeRepository, TyreTypeRepository>();
builder.Services.AddScoped<IAxleGroupRepository, AxleGroupRepository>();
builder.Services.AddScoped<IAxleFeeScheduleRepository, AxleFeeScheduleRepository>();

// Permission services
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IPermissionVerificationService, PermissionVerificationService>();

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

        // Run idempotent seeder
        await TruLoad.Data.Seeders.DatabaseSeeder.SeedAsync(dbContext, logger);
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to apply database migrations or seeding");
    // Don't fail startup if migrations fail - let health check handle it
}

// Swagger (all environments for now; restrict to dev/staging later)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TruLoad API v1");
    c.RoutePrefix = string.Empty; // Serve Swagger UI at root
});

app.UseSerilogRequestLogging();

// Audit middleware
app.UseAuditMiddleware();

app.UseCors();

app.UseAuthentication();
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
    
    // Run database schema verification on startup (development only)
    // TODO: Re-enable when Tests namespace is available
    //if (app.Environment.IsDevelopment())
    //{
    //    using var scope = app.Services.CreateScope();
    //    var dbContext = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
    //    await TruLoad.Backend.Tests.DatabaseSchemaVerification.VerifySchema(dbContext);
    //}
    
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
