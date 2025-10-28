using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using truload_backend.Data;

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
var jwtSecret = builder.Configuration["JWT:Secret"]
    ?? throw new InvalidOperationException("JWT Secret not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

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

// TODO: Add MediatR, FluentValidation, AutoMapper, MassTransit (RabbitMQ), etc.

// ===== App Configuration =====
var app = builder.Build();

// Swagger (all environments for now; restrict to dev/staging later)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TruLoad API v1");
    c.RoutePrefix = string.Empty; // Serve Swagger UI at root
});

app.UseSerilogRequestLogging();

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
