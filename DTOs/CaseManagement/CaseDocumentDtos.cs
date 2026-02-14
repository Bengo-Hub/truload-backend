namespace TruLoad.Backend.DTOs.CaseManagement;

/// <summary>
/// Represents a single document associated with a case from any source
/// (weighing, prosecution, financial, court, etc.)
/// </summary>
public record CaseDocumentDto
{
    public Guid Id { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? ReferenceNo { get; init; }
    public string DownloadUrl { get; init; } = string.Empty;
    public string? Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
}

/// <summary>
/// Summary of document counts per category
/// </summary>
public record CaseDocumentSummaryDto
{
    public int TotalDocuments { get; init; }
    public int WeightTickets { get; init; }
    public int ChargeSheets { get; init; }
    public int Invoices { get; init; }
    public int Receipts { get; init; }
    public int CourtMinutes { get; init; }
    public int SpecialReleaseCertificates { get; init; }
    public int Subfiles { get; init; }
}
