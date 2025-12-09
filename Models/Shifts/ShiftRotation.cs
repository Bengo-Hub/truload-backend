namespace TruLoad.Backend.Models;

/// <summary>
/// ShiftRotation entity - Rotation patterns for rotating shifts
/// </summary>
public class ShiftRotation
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? CurrentActiveShiftId { get; set; }
    public int RunDuration { get; set; } = 2;
    
    /// <summary>
    /// Run unit: Days, Weeks, Months
    /// </summary>
    public string RunUnit { get; set; } = "Months";
    
    public int BreakDuration { get; set; } = 1;
    
    /// <summary>
    /// Break unit: Day, Week, Month
    /// </summary>
    public string BreakUnit { get; set; } = "Day";
    
    public DateTime? NextChangeDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public WorkShift? CurrentActiveShift { get; set; }
    public ICollection<RotationShift> RotationShifts { get; set; } = new List<RotationShift>();
    public ICollection<UserShift> UserShifts { get; set; } = new List<UserShift>();
}
