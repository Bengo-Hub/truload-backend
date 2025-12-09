using System.Security.Claims;
using System.Text.RegularExpressions;
using TruLoad.Backend.Models;
using truload_backend.Data;

namespace TruLoad.Backend.Middleware;

/// <summary>
/// Audit middleware that logs all API operations to the AuditLog table.
/// Records UserId, action, endpoint, HTTP method, status code, resource type/ID, IP address, and user agent.
/// Integrates with both Serilog (structured logging) and database persistence.
/// </summary>
public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditMiddleware> _logger;

    public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TruLoadDbContext dbContext)
    {
        // Skip health check and Swagger endpoints
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        var startTime = DateTime.UtcNow;
        var requestPath = context.Request.Path.Value ?? string.Empty;
        var requestMethod = context.Request.Method;
        var originalBodyStream = context.Response.Body;

        using (var responseBody = new MemoryStream())
        {
            context.Response.Body = responseBody;

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // Log audit entry for exceptions
                await LogAuditEntryAsync(
                    dbContext,
                    context,
                    requestPath,
                    requestMethod,
                    500,
                    startTime,
                    success: false,
                    denialReason: ex.Message);
                throw;
            }

            // Log audit entry after response
            await LogAuditEntryAsync(
                dbContext,
                context,
                requestPath,
                requestMethod,
                context.Response.StatusCode,
                startTime,
                success: context.Response.StatusCode < 400);

            // Copy response back to original stream
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private async Task LogAuditEntryAsync(
        TruLoadDbContext dbContext,
        HttpContext context,
        string requestPath,
        string requestMethod,
        int statusCode,
        DateTime timestamp,
        bool success = true,
        string? denialReason = null)
    {
        try
        {
            var userId = ExtractUserId(context);
            var resourceType = ExtractResourceType(requestPath);
            var resourceId = ExtractResourceId(requestPath);
            var action = DetermineAction(requestMethod);
            var endpoint = ExtractEndpoint(requestPath);
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var requestId = context.TraceIdentifier;

            // Log to Serilog structured logging
            _logger.LogInformation(
                "AUDIT: User={UserId} Action={Action} ResourceType={ResourceType} ResourceId={ResourceId} " +
                "Endpoint={Endpoint} Method={Method} Status={Status} Success={Success} Timestamp={Timestamp}",
                userId, action, resourceType, resourceId, endpoint, requestMethod, statusCode, success, timestamp);

            // Create audit log entry
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Success = success,
                HttpMethod = requestMethod,
                Endpoint = endpoint,
                StatusCode = statusCode,
                RequestId = requestId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DenialReason = denialReason,
                CreatedAt = timestamp,
                OrganizationId = ExtractOrganizationId(context)
            };

            // Persist to database
            dbContext.AuditLogs.Add(auditLog);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Don't throw; log the error but continue. Audit failures should not break request processing.
            _logger.LogWarning(ex, "Failed to save audit log to database");
        }
    }

    private Guid ExtractUserId(HttpContext context)
    {
        var userIdClaim = context.User.FindFirst("sub") ??
                          context.User.FindFirst(ClaimTypes.NameIdentifier) ??
                          context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            return userId;

        return Guid.Empty;
    }

    private Guid? ExtractOrganizationId(HttpContext context)
    {
        var orgIdClaim = context.User.FindFirst("org_id") ??
                         context.User.FindFirst("tenant_id") ??
                         context.User.FindFirst("organization_id");

        if (orgIdClaim != null && Guid.TryParse(orgIdClaim.Value, out var orgId))
            return orgId;

        return null;
    }

    private string ExtractResourceType(string path)
    {
        // Extract resource type from path segments: /api/v1/{resource}/{id}
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3)
        {
            // segments[0] = api, segments[1] = v1, segments[2] = resource
            var resourceSegment = segments[2];
            // Remove trailing 's' if plural (users -> User)
            if (resourceSegment.EndsWith("s"))
                return resourceSegment[..^1];
            return resourceSegment;
        }

        return "Unknown";
    }

    private Guid? ExtractResourceId(string path)
    {
        // Extract GUID from path segments
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 4)
        {
            // Try to parse the 4th segment as a GUID (after api/v1/resource)
            if (Guid.TryParse(segments[3], out var resourceId))
                return resourceId;
        }

        // Also try to find any GUID in the path as fallback
        var guidPattern = @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";
        var match = Regex.Match(path, guidPattern, RegexOptions.IgnoreCase);
        if (match.Success && Guid.TryParse(match.Value, out var id))
            return id;

        return null;
    }

    private string ExtractEndpoint(string path)
    {
        // Extract meaningful endpoint: /api/v1/resource
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3)
        {
            return $"/{string.Join("/", segments.Take(3))}";
        }

        return path;
    }

    private string DetermineAction(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => "READ",
            "POST" => "CREATE",
            "PUT" => "UPDATE",
            "PATCH" => "UPDATE",
            "DELETE" => "DELETE",
            _ => "UNKNOWN"
        };
    }
}

/// <summary>
/// Extension method to register audit middleware in the request pipeline.
/// </summary>
public static class AuditMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuditMiddleware>();
    }
}
