using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Shift;

public class ShiftRotationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? CurrentActiveShiftId { get; set; }
    public string? CurrentActiveShiftName { get; set; }
    public int RunDuration { get; set; }
    public string RunUnit { get; set; } = string.Empty;
    public int BreakDuration { get; set; }
    public string BreakUnit { get; set; } = string.Empty;
    public DateTime? NextChangeDate { get; set; }
    public bool IsActive { get; set; }
    public List<RotationShiftDto> RotationShifts { get; set; } = new();
}

public class RotationShiftDto
{
    public Guid RotationId { get; set; }
    public Guid WorkShiftId { get; set; }
    public string WorkShiftName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
}

public class CreateShiftRotationRequest
{
    [Required]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [Range(1, 365)]
    public int RunDuration { get; set; } = 2;

    [Required]
    [StringLength(20)]
    public string RunUnit { get; set; } = "Months";

    [Range(1, 365)]
    public int BreakDuration { get; set; } = 1;

    [Required]
    [StringLength(20)]
    public string BreakUnit { get; set; } = "Day";

    public List<CreateRotationShiftRequest> RotationShifts { get; set; } = new();
}

public class CreateRotationShiftRequest
{
    [Required]
    public Guid WorkShiftId { get; set; }

    [Range(1, 100)]
    public int SequenceOrder { get; set; }
}

public class UpdateShiftRotationRequest
{
    [StringLength(255)]
    public string? Title { get; set; }

    public Guid? CurrentActiveShiftId { get; set; }

    [Range(1, 365)]
    public int? RunDuration { get; set; }

    [StringLength(20)]
    public string? RunUnit { get; set; }

    [Range(1, 365)]
    public int? BreakDuration { get; set; }

    [StringLength(20)]
    public string? BreakUnit { get; set; }

    public DateTime? NextChangeDate { get; set; }

    public bool? IsActive { get; set; }
}

public class UpdateRotationShiftsRequest
{
    [Required]
    public List<CreateRotationShiftRequest> RotationShifts { get; set; } = new();
}
