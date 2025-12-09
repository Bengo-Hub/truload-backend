namespace TruLoad.Backend.Models;

/// <summary>
/// User entity - Application-level user management with auth-service synchronization
/// Local user records maintain app-specific data (shifts, stations, roles)
/// while identity is synced from centralized auth-service
/// </summary>
public class User
{
    /// <summary>
    /// Local user ID (primary key)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to centralized auth-service user
    /// This is the source of truth for identity
    /// </summary>
    public Guid AuthServiceUserId { get; set; }

    /// <summary>
    /// User email (synced from auth-service)
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Contact phone number
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Full name of the user
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// User status: active, inactive, locked
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// Assigned weighbridge station (optional)
    /// </summary>
    public Guid? StationId { get; set; }

    /// <summary>
    /// Organization/company affiliation
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Department within organization
    /// </summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>
    /// Last login timestamp
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Sync status with auth-service: synced, pending, failed
    /// </summary>
    public string SyncStatus { get; set; } = "synced";

    /// <summary>
    /// Last sync timestamp with auth-service
    /// </summary>
    public DateTime? SyncAt { get; set; }
    
    /// <summary>
    /// Alias for SyncAt for consistency
    /// </summary>
    public DateTime? LastSyncAt { get => SyncAt; set => SyncAt = value; }

    /// <summary>
    /// Record creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Record update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete timestamp
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public Organization? Organization { get; set; }
    public Department? Department { get; set; }
    public Station? Station { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserShift> UserShifts { get; set; } = new List<UserShift>();
}
