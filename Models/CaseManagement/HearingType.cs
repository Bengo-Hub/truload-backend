using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Hearing type taxonomy for court proceedings.
/// Defines categories of court interactions: initial mention, substantive hearing, judgment, etc.
/// Based on Kenyan criminal procedure law.
/// </summary>
public class HearingType : BaseEntity
{

    /// <summary>
    /// Unique hearing type code (e.g., "MENTION", "HEARING", "JUDGMENT", "RULING", "BAIL").
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Hearing type name for display (e.g., "Mention", "Full Hearing", "Judgment Day").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of when and why this hearing type is used.
    /// </summary>
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<CourtHearing> CourtHearings { get; set; } = [];
}
