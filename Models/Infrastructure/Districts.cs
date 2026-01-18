using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models;

/// <summary>
/// District/Subcounty master data within counties
/// </summary>
public class Districts : BaseEntity
{
    public Guid CountyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public Counties? County { get; set; }
}