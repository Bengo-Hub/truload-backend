using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Hearing outcome taxonomy for court judgments and decisions.
/// Defines possible outcomes of court hearings: adjourned, ruling, convicted, acquitted, etc.
/// Based on Kenyan criminal procedure outcomes.
/// </summary>
public class HearingOutcome : BaseEntity
{

    /// <summary>
    /// Unique outcome code (e.g., "ADJOURNED", "CONVICTED", "ACQUITTED", "RULING", "DISCHARGED").
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Outcome name for display (e.g., "Adjourned", "Convicted", "Acquitted").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of this outcome and its implications for case closure.
    /// </summary>
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<CourtHearing> CourtHearings { get; set; } = [];
}
