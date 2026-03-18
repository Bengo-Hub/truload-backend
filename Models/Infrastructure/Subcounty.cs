using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models;

namespace TruLoad.Backend.Models.Infrastructure;

/// <summary>
/// Subcounty master data within counties.
/// Administrative subdivision (formerly "district"); one level under County.
/// </summary>
public class Subcounty : BaseEntity
{
    /// <summary>
    /// Parent county ID (foreign key)
    /// </summary>
    public Guid CountyId { get; set; }

    /// <summary>
    /// Unique subcounty code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Subcounty name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public Counties? County { get; set; }
}
