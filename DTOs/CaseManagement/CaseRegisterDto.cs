using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.CaseManagement;

/// <summary>
/// Case Register Data Transfer Object
/// </summary>
public class CaseRegisterDto
{
    public Guid Id { get; set; }
    public string CaseNo { get; set; } = string.Empty;
    public Guid? WeighingId { get; set; }
    public string? WeighingTicketNo { get; set; }
    public Guid? YardEntryId { get; set; }
    public Guid? ProhibitionOrderId { get; set; }
    public string? ProhibitionNo { get; set; }
    public Guid VehicleId { get; set; }
    public string VehicleRegNumber { get; set; } = string.Empty;
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public string? DriverLicenseNo { get; set; }
    public Guid ViolationTypeId { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string? ViolationDetails { get; set; }
    public Guid? ActId { get; set; }
    public string? ActName { get; set; }
    public string? DriverNtacNo { get; set; }
    public string? TransporterNtacNo { get; set; }
    /// <summary>Transporter name (from linked weighing when available).</summary>
    public string? TransporterName { get; set; }
    public string? ObNo { get; set; }
    /// <summary>URL to uploaded OB extract document.</summary>
    public string? ObExtractFileUrl { get; set; }
    /// <summary>Court case file number (e.g., MCTR/E889/2025).</summary>
    public string? CourtCaseNo { get; set; }
    /// <summary>Police case file number (e.g., TCR 851/2025).</summary>
    public string? PoliceCaseFileNo { get; set; }
    public Guid? CourtId { get; set; }
    public string? CourtName { get; set; }
    public Guid? DispositionTypeId { get; set; }
    public string? DispositionType { get; set; }
    public Guid CaseStatusId { get; set; }
    public string CaseStatus { get; set; } = string.Empty;
    public bool EscalatedToCaseManager { get; set; }
    public Guid? CaseManagerId { get; set; }
    public string? CaseManagerName { get; set; }
    public Guid? ProsecutorId { get; set; }
    public string? ProsecutorName { get; set; }
    public Guid? ComplainantOfficerId { get; set; }
    public string? ComplainantOfficerName { get; set; }
    /// <summary>Station where the vehicle is detained (for court/prosecution cases).</summary>
    public Guid? DetentionStationId { get; set; }
    public string? DetentionStationName { get; set; }
    public Guid? InvestigatingOfficerId { get; set; }
    public string? InvestigatingOfficerName { get; set; }
    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }
    public string? ClosingReason { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Create Case Register Request
/// </summary>
public class CreateCaseRegisterRequest
{
    public Guid? WeighingId { get; set; }
    public Guid? YardEntryId { get; set; }
    public Guid? ProhibitionOrderId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid ViolationTypeId { get; set; }
    public string? ViolationDetails { get; set; }
    public Guid? ActId { get; set; }
    public Guid? RoadId { get; set; }
    public Guid? CountyId { get; set; }
    public Guid? SubcountyId { get; set; }
}

/// <summary>
/// Update Case Register Request
/// </summary>
public class UpdateCaseRegisterRequest
{
    public string? ViolationDetails { get; set; }
    /// <summary>Driver NTAC for this case (mandatory only when escalating to case manager; optional for prosecution).</summary>
    public string? DriverNtacNo { get; set; }
    /// <summary>Transporter/owner NTAC for this case (mandatory only when escalating to case manager; optional for prosecution).</summary>
    public string? TransporterNtacNo { get; set; }
    public string? ObNo { get; set; }
    /// <summary>URL to uploaded OB extract document.</summary>
    public string? ObExtractFileUrl { get; set; }
    /// <summary>Court case file number (e.g., MCTR/E889/2025).</summary>
    public string? CourtCaseNo { get; set; }
    /// <summary>Police case file number (e.g., TCR 851/2025).</summary>
    public string? PoliceCaseFileNo { get; set; }
    public Guid? CourtId { get; set; }
    public Guid? DispositionTypeId { get; set; }
    public Guid? CaseManagerId { get; set; }
    public Guid? ProsecutorId { get; set; }
    /// <summary>Complainant officer (for court/prosecution cases).</summary>
    public Guid? ComplainantOfficerId { get; set; }
    /// <summary>Station where the vehicle is detained.</summary>
    public Guid? DetentionStationId { get; set; }
    public Guid? InvestigatingOfficerId { get; set; }
}

/// <summary>
/// Close Case Request
/// </summary>
public class CloseCaseRequest
{
    public Guid DispositionTypeId { get; set; }
    public string ClosingReason { get; set; } = string.Empty;
}

/// <summary>
/// Case Search Criteria
/// </summary>
public class CaseSearchCriteria : PagedRequest
{
    public string? CaseNo { get; set; }
    public string? VehicleRegNumber { get; set; }
    public Guid? StationId { get; set; }
    public Guid? ViolationTypeId { get; set; }
    public Guid? CaseStatusId { get; set; }
    public Guid? DispositionTypeId { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public bool? EscalatedToCaseManager { get; set; }
    public Guid? CaseManagerId { get; set; }
}

/// <summary>
/// Case statistics response DTO matching frontend CaseStatistics type
/// </summary>
public class CaseStatisticsDto
{
    public int TotalCases { get; set; }
    public int OpenCases { get; set; }
    public int EscalatedCases { get; set; }
    public int ClosedCases { get; set; }
}
