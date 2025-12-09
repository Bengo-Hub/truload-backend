namespace TruLoad.Backend.Models;

/// <summary>
/// WorkShift entity - Work shift definitions (e.g., "Morning Shift", "Night Shift")
/// </summary>
public class WorkShift
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Shift code identifier for programmatic use (e.g., "MORNING", "EVENING", "NIGHT")
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Alias for Name - for compatibility with seeders and legacy code
    /// </summary>
    public string? ShiftName { get; set; }
    
    /// <summary>
    /// Alias for Code - for compatibility with seeders and legacy code
    /// </summary>
    public string? ShiftCode { get; set; }
    
    public string? Description { get; set; }
    public decimal TotalHoursPerWeek { get; set; } = 40.00m;
    public int GraceMinutes { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public ICollection<WorkShiftSchedule> WorkShiftSchedules { get; set; } = new List<WorkShiftSchedule>();
    public ICollection<UserShift> UserShifts { get; set; } = new List<UserShift>();
    public ICollection<RotationShift> RotationShifts { get; set; } = new List<RotationShift>();
}
