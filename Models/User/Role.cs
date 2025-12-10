namespace TruLoad.Backend.Models;

/// <summary>
/// Role entity - Application-specific roles and permissions
/// Permissions are managed via the RolePermissions junction table in a many-to-many relationship.
/// </summary>
public class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Role code identifier (e.g., "SYSTEM_ADMIN", "STATION_MANAGER")
    /// Used for programmatic role checking and API consistency
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    
    /// <summary>
    /// Many-to-many relationship to permissions through RolePermissions junction table.
    /// Use this to access all permissions assigned to this role.
    /// </summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
