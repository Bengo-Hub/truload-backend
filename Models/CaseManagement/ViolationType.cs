using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Violation type taxonomy for case categorization and severity assessment.
/// Enables flexible, configurable violation classification across the system.
/// </summary>
public class ViolationType : BaseEntity
{

    /// <summary>
    /// Unique violation code (e.g., "OVL001", "SPL002"). Used for reference and reporting.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Violation name (e.g., "Overloading", "Speeding", "Axle Weight Violation").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Detailed description of the violation and what constitutes it.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Severity level: low, medium, high, critical.
    /// Guides initial disposition and prosecution intensity.
    /// </summary>
    public required string Severity { get; set; } // low, medium, high, critical

    // Navigation properties
    public ICollection<CaseRegister> CaseRegisters { get; set; } = [];
}
