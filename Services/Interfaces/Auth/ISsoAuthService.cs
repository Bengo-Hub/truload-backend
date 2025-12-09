using System.Security.Claims;
using TruLoad.Backend.DTOs.Auth;

namespace TruLoad.Backend.Services.Interfaces.Auth;

/// <summary>
/// Service interface for SSO authentication and JWT token management.
/// Handles authentication requests to centralized auth-service and local token generation.
/// </summary>
public interface ISsoAuthService
{
    /// <summary>
    /// Proxy login credentials to auth-service and sync user into local database.
    /// </summary>
    /// <param name="request">Login request with email, password, tenant_slug</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LoginResponse with token, user details, or error information</returns>
    Task<LoginResponse> ProxyLoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parse JWT token claims without signature validation (development mode).
    /// </summary>
    /// <param name="jwtToken">JWT token string</param>
    /// <returns>ClaimsPrincipal representing the token claims, or null if parsing fails</returns>
    ClaimsPrincipal? ParseSsoToken(string jwtToken);
}
