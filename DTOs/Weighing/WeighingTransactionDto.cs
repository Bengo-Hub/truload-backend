using System.ComponentModel.DataAnnotations;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.DTOs.Yard;

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

    // Weighing location (road, town, county, coordinates)
    public Guid? RoadId { get; set; }
    public string? RoadName { get; set; }
    public string? RoadCode { get; set; }
    public string? LocationTown { get; set; }
    public string? LocationCounty { get; set; }
    public decimal? LocationLat { get; set; }
    public decimal? LocationLng { get; set; }

    // Weighing mode
    public string? WeighingType { get; set; }
    public string? Bound { get; set; }

    // Status & Compliance
    public string ControlStatus { get; set; } = string.Empty;
    public decimal TotalFeeUsd { get; set; }
    public bool IsCompliant { get; set; }
    public bool IsSentToYard { get; set; }
    public string ViolationReason { get; set; } = string.Empty;

    // Capture tracking
    public string CaptureStatus { get; set; } = string.Empty;
    public string CaptureSource { get; set; } = string.Empty;

    // Timing
    public DateTime WeighedAt { get; set; }
    public int? TimeTakenSeconds { get; set; } // Processing time

    // Sync & Reweigh
    public bool IsSync { get; set; }
    public int ReweighCycleNo { get; set; } = 0;
    public Guid? OriginalWeighingId { get; set; }

    // Permit
    public bool HasPermit { get; set; }
    public string? PermitNumber { get; set; }

    // Scale Test (daily calibration verification)
    public Guid? ScaleTestId { get; set; }
    public string? ScaleTestResult { get; set; }
    public DateTime? ScaleTestCarriedAt { get; set; }

    // Images & Media
    public string? VehicleThumbnailUrl { get; set; }
    public List<string> VehicleImageUrls { get; set; } = new();

    // Axles
    public List<WeighingAxleDto> WeighingAxles { get; set; } = new();

    // Tag Alerts (populated on decision page after weighing)
    public KeNHATagAlertDto? KeNHATagAlert { get; set; }
    public List<VehicleTagDto> OpenTags { get; set; } = new();

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
/// Alert DTO when a vehicle has an existing KeNHA tag/prohibition.
/// Populated by background KeNHA API check during weighing initiation.
/// Only populated when KeNHA integration is configured and active.
/// </summary>
public class KeNHATagAlertDto
{
    public bool HasTag { get; set; }
    public string? TagStatus { get; set; }
    public string? TagCategory { get; set; }
    public string? Reason { get; set; }
    public string? Station { get; set; }
    public DateTime? TagDate { get; set; }
    public string? TagUid { get; set; }
    public string AlertLevel { get; set; } = "info"; // info, warning, critical
    public string? Message { get; set; }
}

/// <summary>
/// Request DTO for creating a new weighing transaction.
/// Provide either VehicleId or VehicleRegNo. When VehicleRegNo is provided,
/// the backend will look up the vehicle by registration number and create it if not found.
/// </summary>
public class CreateWeighingRequest
{
    [StringLength(50)]
    public string? TicketNumber { get; set; }

    [Required]
    public Guid StationId { get; set; }

    /// <summary>
    /// Vehicle ID (optional if VehicleRegNo is provided).
    /// </summary>
    public Guid? VehicleId { get; set; }

    /// <summary>
    /// Vehicle registration number. When provided, the backend will look up
    /// the vehicle by reg number and auto-create it if not found.
    /// </summary>
    [StringLength(50)]
    public string? VehicleRegNo { get; set; }

    public Guid? DriverId { get; set; }
    public Guid? TransporterId { get; set; }

    /// <summary>
    /// Scale test ID for this weighing session. Required per regulations.
    /// Backend validates a passing scale test exists for the station/bound today.
    /// </summary>
    public Guid? ScaleTestId { get; set; }

    /// <summary>
    /// Direction/bound for bidirectional stations (A or B).
    /// </summary>
    [StringLength(10)]
    public string? Bound { get; set; }

    /// <summary>
    /// Weighing type/mode: mobile, multideck, wim, static
    /// </summary>
    [StringLength(20)]
    public string? WeighingType { get; set; }

    /// <summary>
    /// Applicable Act (e.g. Traffic Act, EAC). When null, backend uses default act from settings.
    /// </summary>
    public Guid? ActId { get; set; }

    public Guid? OriginId { get; set; }
    public Guid? DestinationId { get; set; }
    public Guid? CargoId { get; set; }

    /// <summary>
    /// Road where weighing took place; Town/City, County and coordinates from geolocation or manual.
    /// </summary>
    public Guid? RoadId { get; set; }
    [StringLength(100)]
    public string? LocationTown { get; set; }
    [StringLength(100)]
    public string? LocationCounty { get; set; }
    public decimal? LocationLat { get; set; }
    public decimal? LocationLng { get; set; }
}

/// <summary>
/// Request DTO for updating a weighing transaction.
/// Include all vehicle/transport metadata so weight ticket and compliance use complete data.
/// </summary>
public class UpdateWeighingRequest
{
    [StringLength(50)]
    public string? VehicleRegNumber { get; set; }

    public Guid? DriverId { get; set; }
    public Guid? TransporterId { get; set; }

    /// <summary>Applicable Act (e.g. Traffic Act, EAC). When null, backend uses default act.</summary>
    public Guid? ActId { get; set; }

    public Guid? OriginId { get; set; }
    public Guid? DestinationId { get; set; }
    public Guid? CargoId { get; set; }

    public Guid? RoadId { get; set; }
    [StringLength(100)]
    public string? LocationTown { get; set; }
    [StringLength(100)]
    public string? LocationCounty { get; set; }
    public decimal? LocationLat { get; set; }
    public decimal? LocationLng { get; set; }
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
    
    public int OverloadKg { get; set; }

    public bool IsCompliant { get; set; }
    public string ControlStatus { get; set; } = string.Empty;
    public string ViolationReason { get; set; } = string.Empty;
    public bool IsSentToYard { get; set; }
    public string CaptureStatus { get; set; } = string.Empty;

    public Guid? VehicleId { get; set; }
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
/// ReweighTicketNumber is optional; when empty, the backend generates it from document sequence.
/// </summary>
public class InitiateReweighRequest
{
    [Required]
    public Guid OriginalWeighingId { get; set; }

    /// <summary>Optional. When null or empty, server generates from document sequence.</summary>
    [StringLength(50)]
    public string? ReweighTicketNumber { get; set; }

    /// <summary>
    /// Relief truck registration number (if offloading excess weight to another truck)
    /// </summary>
    [StringLength(20)]
    public string? ReliefTruckRegNumber { get; set; }

    /// <summary>
    /// Relief truck empty weight in kg (before loading the offloaded cargo)
    /// </summary>
    public int? ReliefTruckEmptyWeightKg { get; set; }
}

/// <summary>
/// Request DTO for searching weighing transactions with filters and pagination.
/// Enhanced with ticket page filter options.
/// </summary>
public class SearchWeighingRequest : PagedRequest
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

    /// <summary>
    /// Filter by weighing type: mobile, multideck, wim, static
    /// </summary>
    [StringLength(20)]
    public string? WeighingType { get; set; }

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

    // Sorting
    [StringLength(50)]
    public string SortBy { get; set; } = "WeighedAt";

    [StringLength(10)]
    public string SortOrder { get; set; } = "desc";
}

/// <summary>
/// Request DTO for autoweigh capture from TruConnect middleware.
/// Enables automated weight capture without manual transaction initiation.
/// </summary>
public class AutoweighCaptureRequest
{
    [Required]
    public Guid StationId { get; set; }

    /// <summary>
    /// Direction/bound for bidirectional stations (A or B).
    /// </summary>
    [StringLength(10)]
    public string? Bound { get; set; }

    /// <summary>
    /// Vehicle registration number from ANPR or manual entry.
    /// If VehicleId is not provided, vehicle will be looked up by registration.
    /// </summary>
    [Required]
    [StringLength(50)]
    public string VehicleRegNumber { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Pre-identified vehicle ID. If provided, registration lookup is skipped.
    /// </summary>
    public Guid? VehicleId { get; set; }

    /// <summary>
    /// Captured axle weights from middleware.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one axle weight must be provided")]
    public List<WeighingAxleCaptureDto> Axles { get; set; } = new();

    /// <summary>
    /// Weighing mode used for capture: multideck, static, wim, mobile.
    /// </summary>
    [StringLength(20)]
    public string WeighingMode { get; set; } = "static";

    /// <summary>
    /// When the weights were captured at the middleware.
    /// </summary>
    public DateTime? CapturedAt { get; set; }

    /// <summary>
    /// Source system identifier (e.g., "TruConnect", "ANPR", "Manual").
    /// </summary>
    [StringLength(50)]
    public string? Source { get; set; }

    /// <summary>
    /// Source device identifier for audit trail.
    /// </summary>
    public Guid? SourceDeviceId { get; set; }

    /// <summary>
    /// Optional client-generated local ID for offline idempotency.
    /// </summary>
    [StringLength(100)]
    public string? ClientLocalId { get; set; }

    /// <summary>
    /// Capture source: auto (middleware auto-detection), manual (operator), frontend (TruLoad app).
    /// Default is "auto" for autoweigh endpoint.
    /// </summary>
    [StringLength(20)]
    public string CaptureSource { get; set; } = "auto";

    /// <summary>
    /// Whether this is a final capture (true) or preliminary auto-weigh data (false).
    /// When false, CaptureStatus will be "auto". When true, CaptureStatus will be "captured".
    /// </summary>
    public bool IsFinalCapture { get; set; } = false;

    /// <summary>
    /// Optional: Existing weighing transaction ID from the frontend.
    /// When provided, the backend updates this transaction instead of creating a new one.
    /// This links the middleware autoweigh to the frontend-initiated transaction.
    /// </summary>
    public Guid? WeighingTransactionId { get; set; }
}

/// <summary>
/// Response DTO for autoweigh capture result.
/// </summary>
public class AutoweighResultDto
{
    public Guid WeighingId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string VehicleRegNumber { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public bool VehicleFound { get; set; }

    public int GvwMeasuredKg { get; set; }
    public int GvwPermissibleKg { get; set; }
    public int GvwOverloadKg { get; set; }

    public bool IsCompliant { get; set; }
    public string ControlStatus { get; set; } = string.Empty;
    public string ViolationReason { get; set; } = string.Empty;
    public string CaptureStatus { get; set; } = string.Empty;
    public string CaptureSource { get; set; } = string.Empty;

    public decimal TotalFeeUsd { get; set; }
    public bool HasPermit { get; set; }

    public List<AxleComplianceDto> AxleCompliance { get; set; } = new();

    public DateTime CapturedAt { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? Source { get; set; }
}
