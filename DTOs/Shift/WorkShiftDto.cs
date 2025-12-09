namespace TruLoad.Backend.DTOs.Shift;

public class WorkShiftDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TotalHoursPerWeek { get; set; }
    public int GraceMinutes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<WorkShiftScheduleDto> Schedules { get; set; } = new();
}

public class WorkShiftScheduleDto
{
    public Guid Id { get; set; }
    public string Day { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal BreakHours { get; set; }
    public bool IsWorkingDay { get; set; }
}

public class CreateWorkShiftRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TotalHoursPerWeek { get; set; } = 40.00m;
    public int GraceMinutes { get; set; }
    public List<CreateWorkShiftScheduleRequest> Schedules { get; set; } = new();
}

public class CreateWorkShiftScheduleRequest
{
    public string Day { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal BreakHours { get; set; }
    public bool IsWorkingDay { get; set; } = true;
}

public class UpdateWorkShiftRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? TotalHoursPerWeek { get; set; }
    public int? GraceMinutes { get; set; }
    public bool? IsActive { get; set; }
}
