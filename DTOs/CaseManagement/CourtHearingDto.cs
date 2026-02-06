using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.CaseManagement;

/// <summary>
/// Court Hearing Data Transfer Object
/// </summary>
public class CourtHearingDto
{
    public Guid Id { get; set; }
    public Guid CaseRegisterId { get; set; }
    public string CaseNo { get; set; } = string.Empty;
    public Guid? CourtId { get; set; }
    public string? CourtName { get; set; }
    public string? CourtLocation { get; set; }
    public DateTime HearingDate { get; set; }
    public TimeSpan? HearingTime { get; set; }
    public Guid? HearingTypeId { get; set; }
    public string? HearingTypeName { get; set; }
    public Guid? HearingStatusId { get; set; }
    public string? HearingStatusName { get; set; }
    public Guid? HearingOutcomeId { get; set; }
    public string? HearingOutcomeName { get; set; }
    public string? MinuteNotes { get; set; }
    public DateTime? NextHearingDate { get; set; }
    public string? AdjournmentReason { get; set; }
    public string? PresidingOfficer { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to schedule a new court hearing
/// </summary>
public class CreateCourtHearingRequest
{
    /// <summary>
    /// Court where hearing will be held (optional)
    /// </summary>
    public Guid? CourtId { get; set; }

    /// <summary>
    /// Scheduled hearing date (required)
    /// </summary>
    public DateTime HearingDate { get; set; }

    /// <summary>
    /// Scheduled hearing time (optional)
    /// </summary>
    public TimeSpan? HearingTime { get; set; }

    /// <summary>
    /// Type of hearing (mention, plea, trial, judgment, etc.)
    /// </summary>
    public Guid? HearingTypeId { get; set; }

    /// <summary>
    /// Presiding magistrate/judge name
    /// </summary>
    public string? PresidingOfficer { get; set; }

    /// <summary>
    /// Initial notes or agenda
    /// </summary>
    public string? MinuteNotes { get; set; }
}

/// <summary>
/// Request to update an existing court hearing
/// </summary>
public class UpdateCourtHearingRequest
{
    public Guid? CourtId { get; set; }
    public DateTime? HearingDate { get; set; }
    public TimeSpan? HearingTime { get; set; }
    public Guid? HearingTypeId { get; set; }
    public string? PresidingOfficer { get; set; }
    public string? MinuteNotes { get; set; }
}

/// <summary>
/// Request to adjourn a hearing
/// </summary>
public class AdjournHearingRequest
{
    /// <summary>
    /// Reason for adjournment (required)
    /// </summary>
    public string AdjournmentReason { get; set; } = string.Empty;

    /// <summary>
    /// Next hearing date (required)
    /// </summary>
    public DateTime NextHearingDate { get; set; }

    /// <summary>
    /// Additional minute notes
    /// </summary>
    public string? MinuteNotes { get; set; }
}

/// <summary>
/// Request to complete a hearing with outcome
/// </summary>
public class CompleteHearingRequest
{
    /// <summary>
    /// Hearing outcome (convicted, acquitted, dismissed, etc.)
    /// </summary>
    public Guid HearingOutcomeId { get; set; }

    /// <summary>
    /// Minute notes documenting the hearing
    /// </summary>
    public string? MinuteNotes { get; set; }

    /// <summary>
    /// If adjournment, next hearing date
    /// </summary>
    public DateTime? NextHearingDate { get; set; }

    /// <summary>
    /// Fine amount if convicted
    /// </summary>
    public decimal? FineAmount { get; set; }

    /// <summary>
    /// Sentence details if convicted
    /// </summary>
    public string? SentenceDetails { get; set; }
}

/// <summary>
/// Search criteria for court hearings
/// </summary>
public class CourtHearingSearchCriteria : PagedRequest
{
    public Guid? CaseRegisterId { get; set; }
    public Guid? CourtId { get; set; }
    public Guid? HearingTypeId { get; set; }
    public Guid? HearingStatusId { get; set; }
    public DateTime? HearingDateFrom { get; set; }
    public DateTime? HearingDateTo { get; set; }
}
