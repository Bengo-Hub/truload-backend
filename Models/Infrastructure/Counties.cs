using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models;

/// <summary>
/// County master data for geographic organization
/// </summary>
public class Counties : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Navigation properties (Subcounties are in Infrastructure and reference CountyId)
}