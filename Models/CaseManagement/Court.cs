using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Court master data for case hearings and prosecution.
/// Referenced by case_registers and court_hearings.
/// </summary>
public class Court : BaseEntity
{
    /// <summary>
    /// Unique court code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Court name (e.g., "Nairobi Law Courts")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Court location/address
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Court type: magistrate, high_court, appeal_court, supreme_court
    /// </summary>
    public string CourtType { get; set; } = "magistrate";

    /// <summary>
    /// County where the court is located (for filtering courts by region).
    /// </summary>
    public Guid? CountyId { get; set; }

    /// <summary>
    /// District/Subcounty where the court is located (for filtering by subcounty).
    /// </summary>
    public Guid? DistrictId { get; set; }

    // Navigation properties
    public ICollection<CaseRegister> CaseRegisters { get; set; } = new List<CaseRegister>();
    public ICollection<CourtHearing> CourtHearings { get; set; } = new List<CourtHearing>();
}
