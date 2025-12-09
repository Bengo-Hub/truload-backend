namespace TruLoad.Backend.Models;

/// <summary>
/// WorkShiftSchedule entity - Day-wise schedule configuration for work shifts
/// </summary>
public class WorkShiftSchedule
{
    public Guid Id { get; set; }
    public Guid WorkShiftId { get; set; }
    
    /// <summary>
    /// Day of week: Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
    /// </summary>
    public string Day { get; set; } = string.Empty;
    
    /// <summary>
    /// Alias properties for seeder compatibility
    /// </summary>
    public string? StartTimeStr { get; set; } // String representation (e.g., "08:00", "06:00")
    public string? EndTimeStr { get; set; } // String representation (e.g., "17:00", "14:00")
    
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal BreakHours { get; set; } = 0.0m;
    public bool IsWorkingDay { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public WorkShift WorkShift { get; set; } = null!;
}
