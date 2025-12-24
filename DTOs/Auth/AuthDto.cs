using System.Text.Json.Serialization;

namespace TruLoad.Backend.DTOs.Auth;

/// <summary>
/// Response DTO for successful login.
/// Contains JWT token for use in subsequent API requests.
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// JWT token for authenticated API requests.
    /// Includes user ID, roles, permissions, and organizational claims.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// When the token expires (Unix timestamp).
    /// </summary>
    public long ExpiresAt { get; set; }

    /// <summary>
    /// User information from Identity.
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
    /// User ID from Identity database.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's full name.
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// Organization ID assigned to user.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Station ID assigned to user.
    /// </summary>
    public Guid? StationId { get; set; }

    /// <summary>
    /// Department ID assigned to user.
    /// </summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>
    /// Roles assigned to the user.
    /// </summary>
    public List<RoleDto> Roles { get; set; } = new();

    /// <summary>
    /// Permissions aggregated from assigned roles.
    /// </summary>
    public List<PermissionDto> Permissions { get; set; } = new();
}

/// <summary>
/// Lightweight role DTO returned in login response.
/// </summary>
public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Lightweight permission DTO returned in login response.
/// </summary>
public class PermissionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
