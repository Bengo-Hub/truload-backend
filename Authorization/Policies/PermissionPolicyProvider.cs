using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace TruLoad.Backend.Authorization.Policies;

/// <summary>
/// Dynamic authorization policy provider that handles Permission:Any: and Permission:All: patterns.
/// Falls back to the default provider for standard policies (including Permission:xxx.yyy).
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallbackProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallbackProvider.GetFallbackPolicyAsync();

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("Permission:Any:", StringComparison.Ordinal))
        {
            var codes = policyName["Permission:Any:".Length..].Split('|');
            var (_, policy) = PermissionPolicyFactory.CreateAnyPermissionPolicy(codes);
            return policy;
        }

        if (policyName.StartsWith("Permission:All:", StringComparison.Ordinal))
        {
            var codes = policyName["Permission:All:".Length..].Split('|');
            var (_, policy) = PermissionPolicyFactory.CreateAllPermissionsPolicy(codes);
            return policy;
        }

        return await _fallbackProvider.GetPolicyAsync(policyName);
    }
}
