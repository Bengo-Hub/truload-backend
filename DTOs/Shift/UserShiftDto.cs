using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Shift;

public class UserShiftDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public Guid? WorkShiftId { get; set; }
    public string? WorkShiftName { get; set; }
    public Guid? ShiftRotationId { get; set; }
    public string? ShiftRotationTitle { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUserShiftRequest
{
    [Required]
    public Guid UserId { get; set; }

    public Guid? WorkShiftId { get; set; }

    public Guid? ShiftRotationId { get; set; }

    [Required]
    public DateOnly StartsOn { get; set; }

    public DateOnly? EndsOn { get; set; }
}

public class UpdateUserShiftRequest
{
    public Guid? WorkShiftId { get; set; }

    public Guid? ShiftRotationId { get; set; }

    public DateOnly? StartsOn { get; set; }

    public DateOnly? EndsOn { get; set; }
}
