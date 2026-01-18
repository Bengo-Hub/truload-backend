using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Arrest warrant status lifecycle for warrant tracking.
/// Tracks warrant progression: issued, active, executed, dropped.
/// Critical for arrest and law enforcement operations.
/// </summary>
public class WarrantStatus : BaseEntity
{

    /// <summary>
    /// Unique warrant status code (e.g., "ISSUED", "ACTIVE", "EXECUTED", "DROPPED").
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Warrant status name for display (e.g., "Issued", "Active", "Executed", "Dropped").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of this status and allowed transitions.
    /// </summary>
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<ArrestWarrant> ArrestWarrants { get; set; } = [];
}
