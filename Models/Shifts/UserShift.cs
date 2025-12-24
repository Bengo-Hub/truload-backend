using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Models;

/// <summary>
/// UserShift entity - User assignments to specific shifts or rotations
/// </summary>
public class UserShift
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? WorkShiftId { get; set; }
    public Guid? ShiftRotationId { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
    public WorkShift? WorkShift { get; set; }
    public ShiftRotation? ShiftRotation { get; set; }
}
