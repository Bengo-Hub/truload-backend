using System.ComponentModel.DataAnnotations;
using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.Yard;

/// <summary>
/// Response DTO for a yard entry.
/// </summary>
public class YardEntryDto
{
    public Guid Id { get; set; }
    public Guid WeighingId { get; set; }
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EnteredAt { get; set; }
    public DateTime? ReleasedAt { get; set; }

    // Weighing transaction details (flattened)
    public string? TicketNumber { get; set; }
    public string? VehicleRegNumber { get; set; }
    public string? DriverName { get; set; }
    public string? TransporterName { get; set; }
    public int? GvwMeasuredKg { get; set; }
    public int? GvwPermissibleKg { get; set; }
    public int? OverloadKg { get; set; }
    public decimal? TotalFeeUsd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Whether the linked case (if any) is closed.
    /// Yard release is only allowed when the case is closed (per FRD).
    /// </summary>
    public bool IsCaseClosed { get; set; }
}

/// <summary>
/// Request DTO for creating a yard entry.
/// </summary>
public class CreateYardEntryRequest
{
    [Required]
    public Guid WeighingId { get; set; }

    [Required]
    public Guid StationId { get; set; }

    [Required]
    [StringLength(50)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for releasing a vehicle from yard.
/// </summary>
public class ReleaseYardEntryRequest
{
    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Request DTO for searching yard entries.
/// </summary>
public class SearchYardEntriesRequest : PagedRequest
{
    public Guid? StationId { get; set; }
    public string? Status { get; set; }
    public string? Reason { get; set; }
    public string? VehicleRegNo { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public string SortBy { get; set; } = "EnteredAt";
    public string SortOrder { get; set; } = "desc";
}
