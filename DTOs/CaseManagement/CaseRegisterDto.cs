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
    public string? ObNo { get; set; }
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
    public Guid? DistrictId { get; set; }
    public Guid? SubcountyId { get; set; }
}

/// <summary>
/// Update Case Register Request
/// </summary>
public class UpdateCaseRegisterRequest
{
    public string? ViolationDetails { get; set; }
    public string? DriverNtacNo { get; set; }
    public string? TransporterNtacNo { get; set; }
    public string? ObNo { get; set; }
    public Guid? CourtId { get; set; }
    public Guid? DispositionTypeId { get; set; }
    public Guid? CaseManagerId { get; set; }
    public Guid? ProsecutorId { get; set; }
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
