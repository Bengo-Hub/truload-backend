using Microsoft.AspNetCore.Authorization;
using TruLoad.Backend.Authorization.Requirements;
using TruLoad.Backend.Services.Interfaces.Authorization;

namespace TruLoad.Backend.Authorization.Handlers;

/// <summary>
/// Authorization handler for PermissionRequirement.
/// Verifies user permissions using PermissionVerificationService.
/// Handles both single and multiple permission checks with AND/OR logic.
/// </summary>
public class PermissionRequirementHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionVerificationService _permissionVerificationService;
    private readonly ILogger<PermissionRequirementHandler> _logger;

    public PermissionRequirementHandler(
        IPermissionVerificationService permissionVerificationService,
        ILogger<PermissionRequirementHandler> logger)
    {
        _permissionVerificationService = permissionVerificationService 
            ?? throw new ArgumentNullException(nameof(permissionVerificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (requirement == null)
            throw new ArgumentNullException(nameof(requirement));

        // HttpContext may not be available in all scenarios (e.g., unit tests)
        var httpContext = context.Resource as HttpContext;
        if (httpContext == null)
        {
            // For non-HTTP contexts, we cannot verify permissions
            _logger.LogWarning("No HttpContext available for permission verification");
            context.Fail();
            return;
        }

        // Check if user is authenticated
        if (context.User == null || !(context.User.Identity?.IsAuthenticated ?? false))
        {
            _logger.LogWarning("Unauthenticated user attempting to access protected resource");
            context.Fail();
            return;
        }

        try
        {
            bool hasPermission = requirement.RequirementType switch
            {
                PermissionRequirementType.All => 
                    await _permissionVerificationService.UserHasAllPermissionsAsync(
                        httpContext, requirement.PermissionCodes),
                
                PermissionRequirementType.Any => 
                    await _permissionVerificationService.UserHasAnyPermissionAsync(
                        httpContext, requirement.PermissionCodes),
                
                _ => throw new InvalidOperationException($"Unknown requirement type: {requirement.RequirementType}")
            };

            if (hasPermission)
            {
                context.Succeed(requirement);
                _logger.LogInformation("User {UserId} authorized for permissions: {Codes}",
                    _permissionVerificationService.GetUserIdFromClaims(httpContext) ?? "unknown",
                    string.Join(", ", requirement.PermissionCodes));
            }
            else
            {
                context.Fail();
                _logger.LogWarning("User {UserId} denied access. Required permissions: {Codes}, Type: {RequirementType}",
                    _permissionVerificationService.GetUserIdFromClaims(httpContext) ?? "unknown",
                    string.Join(", ", requirement.PermissionCodes),
                    requirement.RequirementType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during permission verification for user {UserId}",
                _permissionVerificationService.GetUserIdFromClaims(httpContext) ?? "unknown");
            context.Fail();
        }
    }
}
