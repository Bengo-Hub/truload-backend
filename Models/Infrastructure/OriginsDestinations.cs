using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models;

/// <summary>
/// Origin and destination master data for cargo routes
/// </summary>
public class OriginsDestinations : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LocationType { get; set; } = "city"; // city, town, port, border, warehouse
    public string Country { get; set; } = "Kenya";

    /// <summary>
    /// Organization that owns this location.
    /// Null = shared/global (visible to every tenant, same as today). Non-null = tenant-specific
    /// (only visible to that organization, in addition to the shared/global rows).
    /// Mirrors the same NULL=shared convention already used by Driver.OrganizationId.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Navigation property to the owning Organization (null for shared/global rows).
    /// </summary>
    public virtual Organization? Organization { get; set; }
}