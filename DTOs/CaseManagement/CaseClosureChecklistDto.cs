namespace TruLoad.Backend.DTOs.CaseManagement;

/// <summary>
/// Case Closure Checklist Data Transfer Object
/// </summary>
public class CaseClosureChecklistDto
{
    public Guid Id { get; set; }
    public Guid CaseRegisterId { get; set; }
    public string? CaseNo { get; set; }
    public Guid? ClosureTypeId { get; set; }
    public string? ClosureTypeName { get; set; }
    public Guid? LegalSectionId { get; set; }
    public string? LegalSectionTitle { get; set; }
    public bool SubfileAComplete { get; set; }
    public bool SubfileBComplete { get; set; }
    public bool SubfileCComplete { get; set; }
    public bool SubfileDComplete { get; set; }
    public bool SubfileEComplete { get; set; }
    public bool SubfileFComplete { get; set; }
    public bool SubfileGComplete { get; set; }
    public bool SubfileHComplete { get; set; }
    public bool SubfileIComplete { get; set; }
    public bool SubfileJComplete { get; set; }
    public bool AllSubfilesVerified { get; set; }
    public Guid? ReviewStatusId { get; set; }
    public string? ReviewStatusName { get; set; }
    public DateTime? ReviewRequestedAt { get; set; }
    public Guid? ReviewRequestedById { get; set; }
    public string? ReviewRequestedByName { get; set; }
    public string? ReviewNotes { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Update Closure Checklist Request
/// </summary>
public class UpdateChecklistRequest
{
    public Guid? ClosureTypeId { get; set; }
    public Guid? LegalSectionId { get; set; }
    public bool? SubfileAComplete { get; set; }
    public bool? SubfileBComplete { get; set; }
    public bool? SubfileCComplete { get; set; }
    public bool? SubfileDComplete { get; set; }
    public bool? SubfileEComplete { get; set; }
    public bool? SubfileFComplete { get; set; }
    public bool? SubfileGComplete { get; set; }
    public bool? SubfileHComplete { get; set; }
    public bool? SubfileIComplete { get; set; }
    public bool? SubfileJComplete { get; set; }
}

/// <summary>
/// Request Review Request
/// </summary>
public class RequestReviewRequest
{
    public string? ReviewNotes { get; set; }
}

/// <summary>
/// Approve/Reject Review Request
/// </summary>
public class ReviewDecisionRequest
{
    public string? ReviewNotes { get; set; }
}
