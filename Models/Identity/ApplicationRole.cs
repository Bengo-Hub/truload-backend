using Microsoft.AspNetCore.Identity;

namespace TruLoad.Backend.Models.Identity;

/// <summary>
/// ApplicationRole extends IdentityRole with TruLoad-specific properties
/// Roles are linked to permissions via RolePermission junction table
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    /// <summary>
    /// Role code identifier (e.g., "SYSTEM_ADMIN", "STATION_MANAGER")
    /// Used for programmatic role checking and API consistency
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Role description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Active status
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Record creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Record update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to permissions through RolePermissions junction table
    /// </summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
