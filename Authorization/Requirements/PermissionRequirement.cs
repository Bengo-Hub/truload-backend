using Microsoft.AspNetCore.Authorization;

namespace TruLoad.Backend.Authorization.Requirements;

/// <summary>
/// Authorization requirement for permission-based access control.
/// Represents a requirement to have one or more specific permissions.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The permission codes required for authorization.
    /// Multiple codes can be specified for OR/AND logic checks.
    /// </summary>
    public IEnumerable<string> PermissionCodes { get; }

    /// <summary>
    /// The authorization requirement type: All (AND logic) or Any (OR logic).
    /// </summary>
    public PermissionRequirementType RequirementType { get; }

    /// <summary>
    /// Initializes a new instance of the PermissionRequirement class.
    /// </summary>
    /// <param name="permissionCodes">The permission codes to check (single or multiple).</param>
    /// <param name="requirementType">Whether all permissions are required (AND) or any one (OR). Defaults to All for single permission.</param>
    public PermissionRequirement(IEnumerable<string> permissionCodes, PermissionRequirementType requirementType = PermissionRequirementType.All)
    {
        if (!permissionCodes.Any())
            throw new ArgumentException("At least one permission code is required.", nameof(permissionCodes));

        PermissionCodes = permissionCodes;
        RequirementType = requirementType;
    }

    /// <summary>
    /// Initializes a new instance of the PermissionRequirement class with a single permission.
    /// </summary>
    /// <param name="permissionCode">The single permission code to check.</param>
    public PermissionRequirement(string permissionCode)
        : this(new[] { permissionCode }, PermissionRequirementType.All)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
            throw new ArgumentException("Permission code cannot be null or whitespace.", nameof(permissionCode));
    }
}

/// <summary>
/// Specifies whether all permissions or any permission is required for authorization.
/// </summary>
public enum PermissionRequirementType
{
    /// <summary>
    /// User must have all specified permissions (AND logic).
    /// </summary>
    All = 0,

    /// <summary>
    /// User must have at least one of the specified permissions (OR logic).
    /// </summary>
    Any = 1
}
