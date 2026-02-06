using System.ComponentModel.DataAnnotations;
using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.Yard;

/// <summary>
/// Response DTO for a vehicle tag.
/// </summary>
public class VehicleTagDto
{
    public Guid Id { get; set; }
    public string RegNo { get; set; } = string.Empty;
    public string TagType { get; set; } = string.Empty;
    public Guid TagCategoryId { get; set; }
    public string TagCategoryCode { get; set; } = string.Empty;
    public string TagCategoryName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string StationCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TagPhotoPath { get; set; }
    public TimeSpan? EffectiveTimePeriod { get; set; }
    public Guid CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }
    public string? ClosedReason { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool Exported { get; set; }
    /// <summary>
    /// Linked case register ID (for violation tracking)
    /// Required for manual tags, optional for automatic tags
    /// </summary>
    public Guid? CaseRegisterId { get; set; }
    public string? CaseNo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request DTO for creating a vehicle tag.
/// </summary>
public class CreateVehicleTagRequest
{
    [Required]
    [StringLength(50)]
    public string RegNo { get; set; } = string.Empty;

    [StringLength(20)]
    public string TagType { get; set; } = "manual";

    [Required]
    public Guid TagCategoryId { get; set; }

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string StationCode { get; set; } = string.Empty;

    public string? TagPhotoPath { get; set; }

    /// <summary>
    /// Duration in days (e.g., 30 for 30 days)
    /// </summary>
    public int? EffectiveDays { get; set; }

    /// <summary>
    /// Whether to create a case register entry for this tag.
    /// Defaults to true for manual tags (violation tracking).
    /// For automatic tags, this can be set to false.
    /// </summary>
    public bool CreateCase { get; set; } = true;
}

/// <summary>
/// Request DTO for closing a vehicle tag.
/// </summary>
public class CloseVehicleTagRequest
{
    [Required]
    [StringLength(500)]
    public string ClosedReason { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for searching vehicle tags.
/// </summary>
public class SearchVehicleTagsRequest : PagedRequest
{
    public string? RegNo { get; set; }
    public string? Status { get; set; }
    public string? TagType { get; set; }
    public Guid? TagCategoryId { get; set; }
    public string? StationCode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public string SortBy { get; set; } = "OpenedAt";
    public string SortOrder { get; set; } = "desc";
}

/// <summary>
/// Response DTO for tag category.
/// </summary>
public class TagCategoryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
