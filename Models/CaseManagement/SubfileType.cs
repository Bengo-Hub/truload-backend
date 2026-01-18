using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Subfile type taxonomy for case document categorization.
/// Defines the 10 subfile types (A-J) used in Kenyan case management:
/// A = Initial Case Details (weight logs, vehicle info, violation details)
/// B = Document Evidence (weight tickets, photos, ANPR footage, permit documents)
/// C = Expert Reports (engineering/forensic reports)
/// D = Witness Statements (inspector/driver/witnesses)
/// E = Accused Statements & Reweigh/Compliance documents
/// F = Investigation Diary (investigation steps, timelines)
/// G = Charge Sheets, Bonds, NTAC, Arrest Warrants
/// H = Accused Records (prior offences, identification documents)
/// I = Covering Report (prosecutorial summary memo)
/// J = Minute Sheets & Correspondences (court minutes, adjournments, correspondence)
/// </summary>
public class SubfileType : BaseEntity
{

    /// <summary>
    /// Subfile code: single letter A through J.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Subfile name (e.g., "Initial Case Details", "Document Evidence").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Detailed description of what documents belong in this subfile and when it's required.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Example document types included in this subfile.
    /// </summary>
    public string? ExampleDocuments { get; set; }

    /// <summary>
    /// Whether this subfile type is mandatory for all cases.
    /// </summary>
    public bool IsMandatory { get; set; } = false;

    // Navigation properties
    public ICollection<CaseSubfile> CaseSubfiles { get; set; } = [];
}
