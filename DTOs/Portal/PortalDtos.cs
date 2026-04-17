using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Portal;

// ── Request DTOs ──

/// <summary>
/// Request to register a transporter portal account by matching existing transporter data.
/// </summary>
public class PortalRegistrationRequest
{
    /// <summary>
    /// Email address to match against existing transporter records.
    /// </summary>
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Phone number to match against existing transporter records (optional).
    /// </summary>
    [MaxLength(50)]
    public string? Phone { get; set; }

    /// <summary>
    /// Transporter code to match against existing transporter records (optional).
    /// </summary>
    [MaxLength(50)]
    public string? TransporterCode { get; set; }
}

// ── Response DTOs ──

/// <summary>
/// Simplified weighing view for the transporter portal.
/// </summary>
public class PortalWeighingDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string VehicleRegNumber { get; set; } = string.Empty;
    public string ControlStatus { get; set; } = string.Empty;

    // Weights
    public int? TareWeightKg { get; set; }
    public int? GrossWeightKg { get; set; }
    public int? NetWeightKg { get; set; }
    public int? AdjustedNetWeightKg { get; set; }

    // Cargo & consignment
    public string? CargoType { get; set; }
    public string? ConsignmentNo { get; set; }
    public string? OrderReference { get; set; }

    // Organization (which weighbridge)
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;

    // Station
    public string? StationName { get; set; }

    // Timestamps
    public DateTime WeighedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Vehicle summary for the transporter portal.
/// </summary>
public class PortalVehicleDto
{
    public Guid Id { get; set; }
    public string RegNo { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? VehicleType { get; set; }

    // Tare info
    public int? DefaultTareWeightKg { get; set; }
    public int? LastTareWeightKg { get; set; }
    public DateTime? LastTareWeighedAt { get; set; }

    // Summary stats
    public int TotalWeighings { get; set; }
    public DateTime? LastWeighedAt { get; set; }
}

/// <summary>
/// Driver summary for the transporter portal.
/// </summary>
public class PortalDriverDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? DrivingLicenseNo { get; set; }
    public string? PhoneNumber { get; set; }
    public string? LicenseStatus { get; set; }
    public DateTime? LicenseExpiryDate { get; set; }
    public int TotalTrips { get; set; }
}

/// <summary>
/// Driver performance metrics for the transporter portal.
/// </summary>
public class PortalDriverPerformanceDto
{
    public Guid DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public int TripCount { get; set; }
    public long TotalNetWeightKg { get; set; }
    public double AvgPayloadKg { get; set; }
    public double AvgTurnaroundMinutes { get; set; }
}

/// <summary>
/// Vehicle weight trend data point for charts.
/// </summary>
public class PortalVehicleWeightTrendDto
{
    public DateTime Date { get; set; }
    public int? TareWeightKg { get; set; }
    public int? GrossWeightKg { get; set; }
    public int? NetWeightKg { get; set; }
    public string? StationName { get; set; }
}

/// <summary>
/// Consignment tracking info for the transporter portal.
/// </summary>
public class PortalConsignmentDto
{
    public Guid WeighingId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string? ConsignmentNo { get; set; }
    public string? OrderReference { get; set; }
    public string VehicleRegNumber { get; set; } = string.Empty;
    public string? CargoType { get; set; }
    public int? ExpectedNetWeightKg { get; set; }
    public int? ActualNetWeightKg { get; set; }
    public int? WeightDiscrepancyKg { get; set; }
    public string? SealNumbers { get; set; }
    public string? SourceLocation { get; set; }
    public string? DestinationLocation { get; set; }
    public string ControlStatus { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public DateTime WeighedAt { get; set; }
}

/// <summary>
/// Current subscription info for the transporter portal.
/// </summary>
public class PortalSubscriptionDto
{
    public string Status { get; set; } = "NONE";
    public string? PlanName { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public PortalFeatureAccess Features { get; set; } = new();
}

/// <summary>
/// Boolean flags for subscription-gated portal features.
/// </summary>
public class PortalFeatureAccess
{
    /// <summary>Access data from multiple weighbridge organizations.</summary>
    public bool MultiSiteAccess { get; set; }

    /// <summary>Export weighing data as CSV/PDF.</summary>
    public bool DataExport { get; set; }

    /// <summary>View driver performance reports.</summary>
    public bool DriverReports { get; set; }

    /// <summary>View vehicle weight trend charts.</summary>
    public bool VehicleTrends { get; set; }

    /// <summary>Access the portal REST API programmatically.</summary>
    public bool ApiAccess { get; set; }

    /// <summary>Access advanced analytics dashboards.</summary>
    public bool Analytics { get; set; }

    /// <summary>Track consignments across weighbridges.</summary>
    public bool ConsignmentTracking { get; set; }
}
