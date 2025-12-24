using Microsoft.AspNetCore.Identity;
using TruLoad.Backend.Models;

namespace TruLoad.Backend.Models.Identity;

/// <summary>
/// ApplicationUser extends IdentityUser with TruLoad-specific properties
/// Includes organization, station, department assignments
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Full name of the user
    /// </summary>
    public string FullName { get; set; } = string.Empty;

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
    public ICollection<UserShift> UserShifts { get; set; } = new List<UserShift>();
}
