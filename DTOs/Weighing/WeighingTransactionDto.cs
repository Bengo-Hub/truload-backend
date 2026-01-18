using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// Response DTO for a weighing transaction.
/// Enhanced with ticket page display fields.
/// </summary>
public class WeighingTransactionDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;

    // Vehicle Information
    public Guid VehicleId { get; set; }
    public string VehicleRegNumber { get; set; } = string.Empty;
    public string? VehicleMake { get; set; }
    public string? VehicleModel { get; set; }
    public string? VehicleType { get; set; }
    public string? AxleConfiguration { get; set; }
    public bool IsMultiDeck { get; set; }
    public string? DeckType { get; set; } // A, B, Multi-Deck (A), Multi-Deck (B)

    // Driver & Transporter
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public Guid? TransporterId { get; set; }
    public string? TransporterName { get; set; }

    // Station & User
    public Guid StationId { get; set; }
    public string? StationName { get; set; }
    public string? StationCode { get; set; }
    public Guid WeighedByUserId { get; set; }
    public string? WeighedByUserName { get; set; }

    // Weight Measurements
    public int GvwMeasuredKg { get; set; }
    public int GvwPermissibleKg { get; set; }
    public int OverloadKg { get; set; }
    public int ExcessKg { get; set; } // For display purposes

    // Deck Weights (for multi-deck vehicles)
    public int? DeckAWeightKg { get; set; }
    public int? DeckBWeightKg { get; set; }
    public int? DeckCWeightKg { get; set; }
    public int? DeckDWeightKg { get; set; }

    // ANPR Information
    public string? AnprRegistration { get; set; }
    public int? AnprCheckCount { get; set; }
    public bool AnprMatch { get; set; }

    // Origin & Destination
    public string? SourceLocation { get; set; }
    public string? DestinationLocation { get; set; }
    public string? CargoType { get; set; }
    public string? CargoDescription { get; set; }

    // Status & Compliance
    public string ControlStatus { get; set; } = string.Empty;
    public decimal TotalFeeUsd { get; set; }
    public bool IsCompliant { get; set; }
    public bool IsSentToYard { get; set; }
    public string ViolationReason { get; set; } = string.Empty;

    // Timing
    public DateTime WeighedAt { get; set; }
    public int? TimeTakenSeconds { get; set; } // Processing time

    // Sync & Reweigh
    public bool IsSync { get; set; }
    public int ReweighCycleNo { get; set; } = 1;
    public Guid? OriginalWeighingId { get; set; }

    // Permit
    public bool HasPermit { get; set; }
    public string? PermitNumber { get; set; }

    // Images & Media
    public string? VehicleThumbnailUrl { get; set; }
    public List<string> VehicleImageUrls { get; set; } = new();

    // Axles
    public List<WeighingAxleDto> WeighingAxles { get; set; } = new();

    // Display Helpers
    public string StatusBadgeColor { get; set; } = "gray"; // green, red, yellow, gray
    public string ComplianceIcon { get; set; } = ""; // tagged, warned, legal, overload
}

/// <summary>
/// DTO for weighing axle within a transaction.
/// </summary>
public class WeighingAxleDto
{
    public Guid Id { get; set; }
    public int AxleNumber { get; set; }
    public int MeasuredWeightKg { get; set; }
    public int PermissibleWeightKg { get; set; }
    public int OverloadKg { get; set; }
    public Guid AxleConfigurationId { get; set; }
    public Guid? AxleWeightReferenceId { get; set; }
    public DateTime CapturedAt { get; set; }
}

/// <summary>
/// Request DTO for creating a new weighing transaction.
/// </summary>
public class CreateWeighingRequest
{
    [Required]
    [StringLength(50)]
    public string TicketNumber { get; set; } = string.Empty;
    
    [Required]
    public Guid StationId { get; set; }
    
    [Required]
    public Guid VehicleId { get; set; }
    
    public Guid? DriverId { get; set; }
    public Guid? TransporterId { get; set; }
}

/// <summary>
/// Request DTO for updating a weighing transaction.
/// </summary>
public class UpdateWeighingRequest
{
    [StringLength(50)]
    public string? VehicleRegNumber { get; set; }
    
    public Guid? DriverId { get; set; }
    public Guid? TransporterId { get; set; }
}

/// <summary>
/// Request DTO for capturing axle weights (unified endpoint for all modes).
/// Frontend modes (Static, WIM, Mobile) send identical payload structure.
/// </summary>
public class WeighingAxleCaptureDto
{
    [Required]
    [Range(1, 10)]
    public int AxleNumber { get; set; }
    
    [Required]
    [Range(0, int.MaxValue)]
    public int MeasuredWeightKg { get; set; }
    
    public Guid? AxleConfigurationId { get; set; }
}

/// <summary>
/// Request DTO for capturing multiple axle weights in a single operation.
/// </summary>
public class CaptureWeightsRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one axle weight must be captured")]
    public List<WeighingAxleCaptureDto> Axles { get; set; } = new();
}

/// <summary>
/// Response DTO with compliance evaluation results.
/// </summary>
public class WeighingResultDto
{
    public Guid WeighingId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string VehicleRegNumber { get; set; } = string.Empty;
    
    public int GvwMeasuredKg { get; set; }
    public int GvwPermissibleKg { get; set; }
    public int GvwOverloadKg { get; set; }
    
    public bool IsCompliant { get; set; }
    public string ControlStatus { get; set; } = string.Empty;
    public string ViolationReason { get; set; } = string.Empty;
    
    public decimal TotalFeeUsd { get; set; }
    public bool HasPermit { get; set; }
    public int ReweighCycleNo { get; set; }
    
    public List<AxleComplianceDto> AxleCompliance { get; set; } = new();
    
    public DateTime WeighedAt { get; set; }
}

/// <summary>
/// Axle-specific compliance information.
/// </summary>
public class AxleComplianceDto
{
    public int AxleNumber { get; set; }
    public int MeasuredWeightKg { get; set; }
    public int PermissibleWeightKg { get; set; }
    public int OverloadKg { get; set; }
    public bool IsCompliant { get; set; }
}

/// <summary>
/// Request DTO for initiating a reweigh cycle.
/// </summary>
public class InitiateReweighRequest
{
    [Required]
    public Guid OriginalWeighingId { get; set; }

    [Required]
    [StringLength(50)]
    public string ReweighTicketNumber { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for searching weighing transactions with filters and pagination.
/// Enhanced with ticket page filter options.
/// </summary>
public class SearchWeighingRequest
{
    // Date & Time Filters
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public TimeSpan? FromTime { get; set; }
    public TimeSpan? ToTime { get; set; }

    // Station & User Filters
    public Guid? StationId { get; set; }
    public string? StationCode { get; set; }
    public Guid? OperatorId { get; set; }

    // Vehicle Filters
    [StringLength(50)]
    public string? VehicleRegNo { get; set; }
    public string? AxleConfiguration { get; set; }
    public Guid? TransporterId { get; set; }

    // Status Filters
    [StringLength(50)]
    public string? ControlStatus { get; set; } // All, Pending, Passed, Failed, etc.
    public string? State { get; set; } // Active, Recent Tickets filter
    public bool? IsCompliant { get; set; }

    // Search Fields
    public string? SearchTicketNo { get; set; }
    public string? SearchVehicleReg { get; set; }

    // Cargo & Destination
    public string? CargoType { get; set; }
    public string? SourceLocation { get; set; }
    public string? DestinationLocation { get; set; }

    // Advanced Filters
    public bool? HasPermit { get; set; }
    public bool? IsSentToYard { get; set; }
    public int? MinOverloadKg { get; set; }
    public int? MaxOverloadKg { get; set; }

    // View Mode
    public string? ViewMode { get; set; } = "list"; // list, images, line

    // Pagination
    [Range(0, int.MaxValue)]
    public int Skip { get; set; } = 0;

    [Range(1, 100)]
    public int Take { get; set; } = 50;

    // Sorting
    [StringLength(50)]
    public string SortBy { get; set; } = "WeighedAt";

    [StringLength(10)]
    public string SortOrder { get; set; } = "desc";
}

/// <summary>
/// Response DTO for paginated weighing transaction search results.
/// </summary>
public class WeighingSearchResultDto
{
    public List<WeighingTransactionDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}
