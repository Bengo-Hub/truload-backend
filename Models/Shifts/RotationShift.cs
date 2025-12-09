namespace TruLoad.Backend.Models;

/// <summary>
/// RotationShift junction table - Shifts in a rotation
/// </summary>
public class RotationShift
{
    public Guid RotationId { get; set; }
    public Guid WorkShiftId { get; set; }
    public int SequenceOrder { get; set; }

    // Navigation properties
    public ShiftRotation ShiftRotation { get; set; } = null!;
    public WorkShift WorkShift { get; set; } = null!;
}
