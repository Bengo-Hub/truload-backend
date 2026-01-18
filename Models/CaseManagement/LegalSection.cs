using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Unified legal section reference for both Criminal Procedure Code (CPC) and Penal Code (PC) sections.
/// Enables flexible, database-driven management of applicable law sections for case classification.
/// Replaces separate CPCSection and PCSection tables.
/// </summary>
public class LegalSection : BaseEntity
{
    /// <summary>
    /// Legal framework classification
    /// Values: CPC (Criminal Procedure Code), PC (Penal Code), TRAFFIC_ACT (Kenya Traffic Act), OTHER
    /// </summary>
    public required string LegalFramework { get; set; }

    /// <summary>
    /// Section number (e.g., "354", "361", "370" for CPC; "304A", "279" for PC)
    /// UNIQUE within same LegalFramework for compliance
    /// </summary>
    public required string SectionNo { get; set; }

    /// <summary>
    /// Official section title (e.g., "Mode of taking statements of witnesses", "Causing death by negligence")
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Full legal description and scope of the section
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Active status for this legal section
    /// Allows historical sections to be deactivated without deletion
    /// </summary>
    public new bool IsActive { get; set; } = true;

    // Navigation properties (removed duplicate CPC/PC collections - now unified)
}
