namespace TruLoad.Backend.DTOs.CaseManagement;

/// <summary>
/// Special Release Data Transfer Object
/// </summary>
public class SpecialReleaseDto
{
    public Guid Id { get; set; }
    public string CertificateNo { get; set; } = string.Empty;
    public Guid CaseRegisterId { get; set; }
    public string CaseNo { get; set; } = string.Empty;
    public Guid ReleaseTypeId { get; set; }
    public string ReleaseType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool RequiresRedistribution { get; set; }
    public bool RequiresReweigh { get; set; }
    public Guid? LoadCorrectionMemoId { get; set; }
    public string? LoadCorrectionMemoNo { get; set; }
    public Guid? ComplianceCertificateId { get; set; }
    public string? ComplianceCertificateNo { get; set; }
    public Guid? AuthorizedById { get; set; }
    public string? AuthorizedByName { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public bool IsApproved { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool IsRejected { get; set; }
    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Create Special Release Request
/// </summary>
public class CreateSpecialReleaseRequest
{
    public Guid CaseRegisterId { get; set; }
    public Guid ReleaseTypeId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool RequiresRedistribution { get; set; }
    public bool RequiresReweigh { get; set; }
}

/// <summary>
/// Approve Special Release Request
/// </summary>
public class ApproveSpecialReleaseRequest
{
    public string? ApprovalNotes { get; set; }
}

/// <summary>
/// Reject Special Release Request
/// </summary>
public class RejectSpecialReleaseRequest
{
    public string RejectionReason { get; set; } = string.Empty;
}
