using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Hearing status lifecycle for court proceedings.
/// Tracks the current state of a scheduled hearing: scheduled, held, adjourned, cancelled.
/// </summary>
public class HearingStatus : BaseEntity
{

    /// <summary>
    /// Unique hearing status code (e.g., "SCHEDULED", "HELD", "ADJOURNED", "CANCELLED").
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Hearing status name for display (e.g., "Scheduled", "Held", "Adjourned", "Cancelled").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of this status and allowed transitions.
    /// </summary>
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<CourtHearing> CourtHearings { get; set; } = [];
}
