using Microsoft.AspNetCore.Authorization;
using TruLoad.Backend.Authorization.Requirements;

namespace TruLoad.Backend.Authorization.Policies;

/// <summary>
/// Factory for creating authorization policies for permission-based authorization.
/// Handles dynamic policy creation for single, any, and all permission requirements.
/// </summary>
public static class PermissionPolicyFactory
{
    /// <summary>
    /// Create a single permission policy.
    /// Policy name format: Permission:{code}
    /// </summary>
    public static (string PolicyName, AuthorizationPolicy Policy) CreateSinglePermissionPolicy(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
            throw new ArgumentException("Permission code cannot be null or whitespace.", nameof(permissionCode));

        var policyName = $"Permission:{permissionCode}";
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissionCode))
            .Build();

        return (policyName, policy);
    }

    /// <summary>
    /// Create an "any permission" policy (OR logic).
    /// Policy name format: Permission:Any:{code1}|{code2}|...
    /// </summary>
    public static (string PolicyName, AuthorizationPolicy Policy) CreateAnyPermissionPolicy(params string[] permissionCodes)
    {
        if (permissionCodes == null || !permissionCodes.Any())
            throw new ArgumentException("At least one permission code is required.", nameof(permissionCodes));

        var codesString = string.Join("|", permissionCodes);
        var policyName = $"Permission:Any:{codesString}";
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissionCodes, PermissionRequirementType.Any))
            .Build();

        return (policyName, policy);
    }

    /// <summary>
    /// Create an "all permissions" policy (AND logic).
    /// Policy name format: Permission:All:{code1}|{code2}|...
    /// </summary>
    public static (string PolicyName, AuthorizationPolicy Policy) CreateAllPermissionsPolicy(params string[] permissionCodes)
    {
        if (permissionCodes == null || !permissionCodes.Any())
            throw new ArgumentException("At least one permission code is required.", nameof(permissionCodes));

        var codesString = string.Join("|", permissionCodes);
        var policyName = $"Permission:All:{codesString}";
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissionCodes, PermissionRequirementType.All))
            .Build();

        return (policyName, policy);
    }

    /// <summary>
    /// Create a combined policy with multiple authorization requirements.
    /// Useful for scenarios requiring both role-based and permission-based authorization.
    /// </summary>
    public static (string PolicyName, AuthorizationPolicy Policy) CreateCombinedPolicy(
        string policyName,
        IEnumerable<IAuthorizationRequirement> requirements)
    {
        if (string.IsNullOrWhiteSpace(policyName))
            throw new ArgumentException("Policy name cannot be null or whitespace.", nameof(policyName));

        var requirementList = requirements?.ToList();
        if (requirementList == null || !requirementList.Any())
            throw new ArgumentException("At least one requirement is required.", nameof(requirements));

        var builder = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser();

        foreach (var requirement in requirementList)
        {
            builder.AddRequirements(requirement);
        }

        var policy = builder.Build();
        return (policyName, policy);
    }
}
