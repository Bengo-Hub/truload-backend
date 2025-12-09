using Microsoft.AspNetCore.Authorization;

namespace TruLoad.Backend.Authorization.Attributes;

/// <summary>
/// Authorization attribute for requiring a single permission.
/// Usage: [HasPermission("user.create")] on a controller action.
/// The user must have the specified permission to access the endpoint.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
            throw new ArgumentException("Permission code cannot be null or whitespace.", nameof(permissionCode));

        // Use policy named after the permission code for single permissions
        Policy = $"Permission:{permissionCode}";
    }
}

/// <summary>
/// Authorization attribute for requiring at least one of multiple permissions.
/// Usage: [HasAnyPermission("user.create", "user.update")] on a controller action.
/// The user must have at least one of the specified permissions to access the endpoint.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class HasAnyPermissionAttribute : AuthorizeAttribute
{
    public HasAnyPermissionAttribute(params string[] permissionCodes)
    {
        if (permissionCodes == null || !permissionCodes.Any())
            throw new ArgumentException("At least one permission code is required.", nameof(permissionCodes));

        // Use policy with Any prefix for OR logic
        var codesString = string.Join("|", permissionCodes);
        Policy = $"Permission:Any:{codesString}";
    }
}

/// <summary>
/// Authorization attribute for requiring all of multiple permissions.
/// Usage: [HasAllPermissions("user.create", "user.approve")] on a controller action.
/// The user must have all specified permissions to access the endpoint.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class HasAllPermissionsAttribute : AuthorizeAttribute
{
    public HasAllPermissionsAttribute(params string[] permissionCodes)
    {
        if (permissionCodes == null || !permissionCodes.Any())
            throw new ArgumentException("At least one permission code is required.", nameof(permissionCodes));

        // Use policy with All prefix for AND logic
        var codesString = string.Join("|", permissionCodes);
        Policy = $"Permission:All:{codesString}";
    }
}
