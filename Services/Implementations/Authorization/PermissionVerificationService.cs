using System.Security.Claims;
using TruLoad.Backend.Services.Interfaces;
using TruLoad.Backend.Services.Interfaces.Authorization;

namespace TruLoad.Backend.Services.Implementations.Authorization;

/// <summary>
/// Implementation of permission verification service.
/// Verifies user permissions by extracting JWT claims and checking against PermissionService.
/// Caches permission lookups per request to avoid duplicate database queries.
/// </summary>
public class PermissionVerificationService : IPermissionVerificationService
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionVerificationService> _logger;

    // Per-request cache to avoid multiple lookups of same data
    private const string UserPermissionsCacheKey = "user_permissions";

    public PermissionVerificationService(IPermissionService permissionService, ILogger<PermissionVerificationService> logger)
    {
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> UserHasPermissionAsync(HttpContext httpContext, string permissionCode, CancellationToken cancellationToken = default)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));

        if (string.IsNullOrWhiteSpace(permissionCode))
            throw new ArgumentException("Permission code cannot be null or whitespace.", nameof(permissionCode));

        var userPermissions = await GetUserPermissionsAsync(httpContext, cancellationToken);
        var hasPermission = userPermissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);

        var userId = GetUserIdFromClaims(httpContext);
        _logger.LogInformation("Permission check for user {UserId}: {PermissionCode} = {Result}",
            userId ?? "unknown", permissionCode, hasPermission);

        return hasPermission;
    }

    public async Task<bool> UserHasAnyPermissionAsync(HttpContext httpContext, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));

        if (permissionCodes == null)
            throw new ArgumentNullException(nameof(permissionCodes));

        var codes = permissionCodes.ToList();
        if (!codes.Any())
            throw new ArgumentException("At least one permission code is required.", nameof(permissionCodes));

        var userPermissions = await GetUserPermissionsAsync(httpContext, cancellationToken);
        var hasAny = codes.Any(code => userPermissions.Contains(code, StringComparer.OrdinalIgnoreCase));

        var userId = GetUserIdFromClaims(httpContext);
        _logger.LogInformation("Permission check for user {UserId}: ANY of {Codes} = {Result}",
            userId ?? "unknown", string.Join(", ", codes), hasAny);

        return hasAny;
    }

    public async Task<bool> UserHasAllPermissionsAsync(HttpContext httpContext, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));

        if (permissionCodes == null)
            throw new ArgumentNullException(nameof(permissionCodes));

        var codes = permissionCodes.ToList();
        if (!codes.Any())
            throw new ArgumentException("At least one permission code is required.", nameof(permissionCodes));

        var userPermissions = await GetUserPermissionsAsync(httpContext, cancellationToken);
        var hasAll = codes.All(code => userPermissions.Contains(code, StringComparer.OrdinalIgnoreCase));

        var userId = GetUserIdFromClaims(httpContext);
        _logger.LogInformation("Permission check for user {UserId}: ALL of {Codes} = {Result}",
            userId ?? "unknown", string.Join(", ", codes), hasAll);

        return hasAll;
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));

        // Check per-request cache first
        if (httpContext.Items.TryGetValue(UserPermissionsCacheKey, out var cachedPermissions))
        {
            return (IEnumerable<string>)cachedPermissions!;
        }

        // Extract permissions directly from JWT token claims (added during login)
        var principal = httpContext.User;
        if (principal == null || !principal.Identity!.IsAuthenticated)
        {
            _logger.LogWarning("User is not authenticated");
            httpContext.Items[UserPermissionsCacheKey] = new List<string>();
            return Enumerable.Empty<string>();
        }

        try
        {
            // Get all "permission" claims from JWT token
            var permissionClaims = principal.FindAll("permission").Select(c => c.Value).ToList();
            
            if (!permissionClaims.Any())
            {
                _logger.LogWarning("No permission claims found in JWT for user {UserId}",
                    GetUserIdFromClaims(httpContext) ?? "unknown");
            }

            // Cache in request items for subsequent checks
            httpContext.Items[UserPermissionsCacheKey] = permissionClaims;

            return permissionClaims;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting permissions from JWT claims");
            httpContext.Items[UserPermissionsCacheKey] = new List<string>();
            return Enumerable.Empty<string>();
        }
    }

    public string? GetUserIdFromClaims(HttpContext httpContext)
    {
        if (httpContext == null)
            return null;

        var principal = httpContext.User;
        // Try standard claim types first
        return principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? principal?.FindFirst("sub")?.Value;
    }
}
