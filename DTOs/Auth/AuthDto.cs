namespace TruLoad.Backend.DTOs.Auth;

/// <summary>
/// Request DTO for SSO login.
/// Proxied to sso.codevertexitsolutions.com for credential validation.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// User email address (e.g., admin@codevertexitsolutions.com)
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User password
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Tenant slug from SSO (e.g., "codevertex")
    /// </summary>
    public string TenantSlug { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for successful login.
/// Contains JWT token for use in subsequent API requests.
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// JWT token for authenticated API requests.
    /// Includes user ID, role ID, tenant ID, and other claims.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// When the token expires (Unix timestamp).
    /// </summary>
    public long ExpiresAt { get; set; }

    /// <summary>
    /// User information synced from SSO.
    /// </summary>
    public LoginResponseUser User { get; set; } = new();

    /// <summary>
    /// Error code if authentication failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Error description if authentication failed.
    /// </summary>
    public string? ErrorDescription { get; set; }
}

/// <summary>
/// User information in login response.
/// </summary>
public class LoginResponseUser
{
    /// <summary>
    /// Local database user ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Email from SSO.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's full name from SSO.
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// Tenant ID assigned in local database.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Tenant slug from SSO.
    /// </summary>
    public string TenantSlug { get; set; } = string.Empty;

    /// <summary>
    /// Role ID assigned to user.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Role name assigned to user.
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Whether user is superuser from SSO.
    /// </summary>
    public bool IsSuperUser { get; set; }
}

/// <summary>
/// SSO response from external authentication service.
/// </summary>
public class SsoResponse
{
    /// <summary>
    /// JWT token from SSO service.
    /// Contains user claims: sub (user_id), email, tenant_slug, role, is_superuser.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token for obtaining new access tokens.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Token type (typically "Bearer").
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Seconds until token expires.
    /// </summary>
    public int ExpiresIn { get; set; } = 3600;

    /// <summary>
    /// Error message if authentication failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Detailed error description.
    /// </summary>
    public string? ErrorDescription { get; set; }
}
