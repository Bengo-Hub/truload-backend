namespace TruLoad.Backend.DTOs;

/// <summary>
/// Data Transfer Object for Permission entity.
/// Used for API responses to expose permission data to clients.
/// </summary>
public class PermissionDto
{
    /// <summary>
    /// Unique identifier for the permission.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique code for the permission (e.g., "weighing.create", "case.read_own").
    /// Used for programmatic permission checks.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the permission.
    /// Example: "Create Weighing", "Read Own Cases"
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Category the permission belongs to.
    /// Example: "Weighing", "Case", "Prosecution", "User", "Station", "Configuration", "Analytics", "System"
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of what this permission allows.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether the permission is currently active and can be assigned.
    /// Inactive permissions cannot be granted to new users/roles.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Timestamp when the permission was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
