using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Views;

/* 
 * REGULAR VIEWS
 */

public class ActiveVehicleTag : TenantAwareEntity
{
    public string RegNo { get; set; } = null!;
    public string TagType { get; set; } = null!;
    public Guid TagCategoryId { get; set; }
    public string TagCategoryName { get; set; } = null!;
    public string? TagCategoryDescription { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = null!;
    public string? TagPhotoPath { get; set; }
    public TimeSpan? EffectiveTimePeriod { get; set; }
    public Guid CreatedById { get; set; }
    public string CreatedByUsername { get; set; } = null!;
    public string? CreatedByFullName { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
    public double DaysOpen { get; set; }
}

public class YardStatusSummary : TenantAwareEntity
{
    public Guid YardEntryId { get; set; }
    public Guid WeighingId { get; set; }
    public string TicketNumber { get; set; } = null!;
    public string VehicleRegNumber { get; set; } = null!;
    public Guid? VehicleId { get; set; }
    public string StationName { get; set; } = null!;
    public string StationCode { get; set; } = null!;
    public string? EntryReason { get; set; }
    public string Status { get; set; } = null!;
    public DateTime EnteredAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public double DurationHours { get; set; }
    public Guid? CaseRegisterId { get; set; }
    public string? CaseNo { get; set; }
    public string? ViolationDetails { get; set; }
    public Guid? SpecialReleaseId { get; set; }
    public string? ReleaseType { get; set; }
    public string? ReleaseMemoNo { get; set; }
    public string? TransporterName { get; set; }
    public string? TransporterPhone { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
}

public class ActiveCase : TenantAwareEntity
{
    public Guid CaseId { get; set; }
    public string CaseNo { get; set; } = null!;
    public Guid? WeighingId { get; set; }
    public string? TicketNumber { get; set; }
    public string? VehicleRegNumber { get; set; }
    public Guid? VehicleId { get; set; }
    public string? RegNo { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public string? DriverIdNo { get; set; }
    public Guid ViolationTypeId { get; set; }
    public string ViolationType { get; set; } = null!;
    public string ViolationSeverity { get; set; } = null!;
    public string? ViolationDetails { get; set; }
    public Guid ActId { get; set; }
    public string ActName { get; set; } = null!;
    public string? DriverNtacNo { get; set; }
    public string? TransporterNtacNo { get; set; }
    public string? ObNo { get; set; }
    public Guid? CourtId { get; set; }
    public string? CourtName { get; set; }
    public Guid? DispositionTypeId { get; set; }
    public string? DispositionType { get; set; }
    public Guid CaseStatusId { get; set; }
    public string CaseStatus { get; set; } = null!;
    public bool EscalatedToCaseManager { get; set; }
    public Guid? CaseManagerId { get; set; }
    public string? CaseManagerName { get; set; }
    public Guid? ProsecutorId { get; set; }
    public Guid? ComplainantOfficerId { get; set; }
    public Guid? InvestigatingOfficerId { get; set; }
    public Guid CreatedById { get; set; }
    public double DaysOpen { get; set; }
}

public class PendingCourtHearing : TenantAwareEntity
{
    public Guid HearingId { get; set; }
    public Guid CaseRegisterId { get; set; }
    public string CaseNo { get; set; } = null!;
    public Guid CourtId { get; set; }
    public string CourtName { get; set; } = null!;
    public string? CourtLocation { get; set; }
    public DateTime HearingDate { get; set; }
    public TimeSpan HearingTime { get; set; }
    public Guid? HearingTypeId { get; set; }
    public string? HearingType { get; set; }
    public Guid? HearingStatusId { get; set; }
    public string? HearingStatus { get; set; }
    public Guid? HearingOutcomeId { get; set; }
    public string? HearingOutcome { get; set; }
    public string? MinuteNotes { get; set; }
    public DateTime? NextHearingDate { get; set; }
    public string? AdjournmentReason { get; set; }
    public string? PresidingOfficer { get; set; }
    public int DaysUntilHearing { get; set; }
    public Guid? VehicleId { get; set; }
    public string? VehicleRegNo { get; set; }
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public string? ViolationDetails { get; set; }
}

public class ActiveArrestWarrant : TenantAwareEntity
{
    public Guid WarrantId { get; set; }
    public string WarrantNo { get; set; } = null!;
    public Guid CaseRegisterId { get; set; }
    public string CaseNo { get; set; } = null!;
    public string IssuedAgainst { get; set; } = null!;
    public string? AccusedIdNo { get; set; }
    public string? Reason { get; set; }
    public string? IssuedByCourtOfficer { get; set; }
    public DateTime IssuedAt { get; set; }
    public Guid? WarrantStatusId { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime? DroppedAt { get; set; }
    public string? ExecutionDetails { get; set; }
    public double DaysSinceIssued { get; set; }
    public Guid? VehicleId { get; set; }
    public string? VehicleRegNo { get; set; }
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? ViolationDetails { get; set; }
}

public class RecentCompliantWeighing : TenantAwareEntity
{
    public Guid WeighingId { get; set; }
    public string TicketNumber { get; set; } = null!;
    public string VehicleRegNumber { get; set; } = null!;
    public Guid? VehicleId { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public Guid? TransporterId { get; set; }
    public string? TransporterName { get; set; }
    public string StationName { get; set; } = null!;
    public string StationCode { get; set; } = null!;
    public string WeighingType { get; set; } = null!;
    public decimal GvwMeasuredKg { get; set; }
    public decimal GvwPermissibleKg { get; set; }
    public decimal OverloadKg { get; set; }
    public string ControlStatus { get; set; } = null!;
    public bool IsCompliant { get; set; }
    public bool ToleranceApplied { get; set; }
    public DateTime WeighedAt { get; set; }
    public Guid? OriginId { get; set; }
    public string? OriginName { get; set; }
    public Guid? DestinationId { get; set; }
    public string? DestinationName { get; set; }
    public Guid? CargoId { get; set; }
    public string? CargoType { get; set; }
}

public class PendingSpecialRelease : TenantAwareEntity
{
    public Guid SpecialReleaseId { get; set; }
    public string? ReleaseMemoNo { get; set; }
    public Guid CaseRegisterId { get; set; }
    public string CaseNo { get; set; } = null!;
    public string ReleaseType { get; set; } = null!;
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = null!;
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public Guid? ApproverRoleId { get; set; }
    public string Status { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public double DaysPending { get; set; }
    public Guid? VehicleId { get; set; }
    public string? VehicleRegNo { get; set; }
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public string? ViolationDetails { get; set; }
    public Guid? WeighingId { get; set; }
    public string? TicketNumber { get; set; }
}

public class ActivePermit : TenantAwareEntity
{
    public Guid PermitId { get; set; }
    public string PermitNo { get; set; } = null!;
    public Guid VehicleId { get; set; }
    public string RegNo { get; set; } = null!;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public Guid PermitTypeId { get; set; }
    public string PermitType { get; set; } = null!;
    public string? PermitTypeDescription { get; set; }
    public decimal GvwExtensionKg { get; set; }
    public decimal AxleExtensionKg { get; set; }
    public string? IssuingAuthority { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public double DaysUntilExpiry { get; set; }
    public bool IsExpiringSoon { get; set; }
}

/* 
 * MATERIALIZED VIEWS
 */

public class MvDailyWeighingStats : ITenantAware
{
    public Guid OrganizationId { get; set; }
    public Guid? StationId { get; set; }
    public string StationName { get; set; } = null!;
    public DateTime WeighingDate { get; set; }
    public long TotalWeighings { get; set; }
    public long CompliantCount { get; set; }
    public long NonCompliantCount { get; set; }
    public long SentToYardCount { get; set; }
    public decimal? AvgGvwMeasured { get; set; }
    public decimal? AvgOverload { get; set; }
    public decimal? MaxOverload { get; set; }
    public decimal? TotalFeesCollected { get; set; }
    public long UniqueVehicles { get; set; }
    public long UniqueTransporters { get; set; }
}

public class MvChargeSummary : ITenantAware
{
    public Guid OrganizationId { get; set; }
    public Guid? StationId { get; set; }
    public Guid ProsecutionCaseId { get; set; }
    public Guid CaseRegisterId { get; set; }
    public string CaseNo { get; set; } = null!;
    public Guid? VehicleId { get; set; }
    public Guid? WeighingId { get; set; }
    public Guid ActId { get; set; }
    public string ActName { get; set; } = null!;
    public decimal GvwOverloadKg { get; set; }
    public decimal GvwFeeUsd { get; set; }
    public decimal MaxAxleOverloadKg { get; set; }
    public decimal MaxAxleFeeUsd { get; set; }
    public string BestChargeBasis { get; set; } = null!;
    public decimal PenaltyMultiplier { get; set; }
    public decimal TotalFeeUsd { get; set; }
    public decimal TotalFeeKes { get; set; }
    public decimal ForexRate { get; set; }
    public string Status { get; set; } = null!;
    public string? CertificateNo { get; set; }
    public string ChargeReason { get; set; } = null!;
    public decimal FeeDifferenceUsd { get; set; }
}

public class MvAxleGroupViolation : ITenantAware
{
    public Guid OrganizationId { get; set; }
    public Guid? StationId { get; set; }
    public string AxleGrouping { get; set; } = null!;
    public string TyreType { get; set; } = null!;
    public long TotalWeighings { get; set; }
    public long Violations { get; set; }
    public decimal ViolationRatePct { get; set; }
    public decimal AvgMeasuredWeight { get; set; }
    public decimal AvgPermissibleWeight { get; set; }
    public decimal? AvgOverload { get; set; }
    public decimal MaxOverload { get; set; }
    public decimal TotalFeesGenerated { get; set; }
    public long StationsWithViolations { get; set; }
    public string[]? ViolatingStations { get; set; }
}

public class MvDriverDemeritRanking : ITenantAware
{
    public Guid OrganizationId { get; set; }
    public Guid? StationId { get; set; }
    public Guid DriverId { get; set; }
    public string IdNoOrPassport { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public long TotalCases { get; set; }
    public long ClosedCases { get; set; }
    public long OpenCases { get; set; }
    public decimal? TotalOverloadKg { get; set; }
    public decimal? TotalFeesCharged { get; set; }
    public DateTime? LastViolationDate { get; set; }
    public decimal? MaxSingleOverloadKg { get; set; }
    public bool IsRepeatOffender { get; set; }
    public long ActiveWarrants { get; set; }
}

public class MvVehicleViolationHistory : ITenantAware
{
    public Guid OrganizationId { get; set; }
    public Guid? StationId { get; set; }
    public Guid VehicleId { get; set; }
    public string RegNo { get; set; } = null!;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? VehicleType { get; set; }
    public string? OwnerName { get; set; }
    public string? TransporterName { get; set; }
    public long TotalWeighings { get; set; }
    public long Violations { get; set; }
    public decimal ViolationRatePct { get; set; }
    public decimal? TotalOverloadKg { get; set; }
    public decimal? TotalFeesCharged { get; set; }
    public DateTime LastWeighingDate { get; set; }
    public decimal MaxOverloadKg { get; set; }
    public bool IsCurrentlyTagged { get; set; }
    public bool IsInYard { get; set; }
}

public class MvStationPerformanceScorecard : ITenantAware
{
    public Guid OrganizationId { get; set; }
    public Guid? StationId { get; set; }
    public string StationCode { get; set; } = null!;
    public string StationName { get; set; } = null!;
    public string StationType { get; set; } = null!;
    public string? RoadName { get; set; }
    public string? CountyName { get; set; }
    public long TotalWeighings { get; set; }

    [Column("weighings_last_30_days")]
    public long WeighingsLast30Days { get; set; }

    [Column("weighings_last_7_days")]
    public long WeighingsLast7Days { get; set; }

    public decimal ComplianceRatePct { get; set; }
    public decimal? TotalRevenueUsd { get; set; }

    [Column("revenue_last_30_days")]
    public decimal? RevenueLast30Days { get; set; }

    public long UniqueVehicles { get; set; }
    public long UniqueTransporters { get; set; }
    public long TotalYardEntries { get; set; }
    public long ActiveYardEntries { get; set; }
    public long TotalCasesGenerated { get; set; }
    public DateTime? LastScaleTestDate { get; set; }
    public long PassedScaleTests { get; set; }
    public long FailedScaleTests { get; set; }
}
