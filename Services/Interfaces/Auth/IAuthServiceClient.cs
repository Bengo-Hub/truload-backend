using TruLoad.Backend.DTOs.Auth;

namespace TruLoad.Backend.Services.Interfaces.Auth;

/// <summary>
/// Client interface for direct auth-service API communication.
/// Used for user/tenant creation and management operations.
/// </summary>
public interface IAuthServiceClient
{
    /// <summary>
    /// Create a tenant in auth-service.
    /// </summary>
    Task<CreateTenantResponse> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tenant by slug from auth-service.
    /// </summary>
    Task<TenantDto?> GetTenantBySlugAsync(string tenantSlug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a user in auth-service.
    /// Used when user exists locally but not in SSO.
    /// </summary>
    Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user by email and tenant from auth-service.
    /// </summary>
    Task<UserDto?> GetUserByEmailAsync(string email, string tenantSlug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if user exists in auth-service.
    /// </summary>
    Task<bool> UserExistsAsync(string email, string tenantSlug, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to create tenant in auth-service
/// </summary>
public class CreateTenantRequest
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Response from auth-service tenant creation
/// </summary>
public class CreateTenantResponse
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// Request to create user in auth-service
/// </summary>
public class CreateUserRequest
{
    public Guid Id { get; set; }  // Use same UUID from local DB
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;  // Pre-hashed with Argon2
    public string FullName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
    public string? Role { get; set; }
    public bool IsSuperUser { get; set; }
}

/// <summary>
/// Response from auth-service user creation
/// </summary>
public class CreateUserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
}

/// <summary>
/// Tenant DTO from auth-service
/// </summary>
public class TenantDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// User DTO from auth-service
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
    public string? Role { get; set; }
    public bool IsSuperUser { get; set; }
}
