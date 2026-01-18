using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Weighing;

public class PermitDto
{
    public Guid Id { get; set; }
    public string PermitNo { get; set; } = string.Empty;
    public Guid VehicleId { get; set; }
    public string VehicleRegNo { get; set; } = string.Empty;
    public Guid PermitTypeId { get; set; }
    public string PermitTypeName { get; set; } = string.Empty;
    public int? AxleExtensionKg { get; set; }
    public int? GvwExtensionKg { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string IssuingAuthority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreatePermitRequest
{
    [Required(ErrorMessage = "Permit number is required")]
    [StringLength(100, ErrorMessage = "Permit number cannot exceed 100 characters")]
    public string PermitNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vehicle ID is required")]
    public Guid VehicleId { get; set; }

    [Required(ErrorMessage = "Permit type ID is required")]
    public Guid PermitTypeId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Axle extension must be non-negative")]
    public int? AxleExtensionKg { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "GVW extension must be non-negative")]
    public int? GvwExtensionKg { get; set; }

    [Required(ErrorMessage = "Valid from date is required")]
    public DateTime ValidFrom { get; set; }

    [Required(ErrorMessage = "Valid to date is required")]
    public DateTime ValidTo { get; set; }

    [StringLength(255, ErrorMessage = "Issuing authority cannot exceed 255 characters")]
    public string IssuingAuthority { get; set; } = string.Empty;

    public string Status { get; set; } = "active";
}

public class UpdatePermitRequest
{
    [StringLength(100, ErrorMessage = "Permit number cannot exceed 100 characters")]
    public string? PermitNo { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Axle extension must be non-negative")]
    public int? AxleExtensionKg { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "GVW extension must be non-negative")]
    public int? GvwExtensionKg { get; set; }

    public DateTime? ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    [StringLength(255, ErrorMessage = "Issuing authority cannot exceed 255 characters")]
    public string? IssuingAuthority { get; set; }

    [RegularExpression("^(active|expired|revoked)$", ErrorMessage = "Status must be active, expired, or revoked")]
    public string? Status { get; set; }
}
