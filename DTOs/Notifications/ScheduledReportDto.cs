using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Notifications;

public class ScheduledReportDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string Format { get; set; } = "PDF";
    public string CronSchedule { get; set; } = string.Empty;
    public string? ScheduleDescription { get; set; }
    public List<string> Recipients { get; set; } = new();
    public string? ParametersJson { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public string? LastRunStatus { get; set; }
    public string? LastRunError { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateScheduledReportRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Module { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ReportType { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Format { get; set; } = "PDF";

    [Required, MaxLength(50)]
    public string CronSchedule { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ScheduleDescription { get; set; }

    [Required, MinLength(1)]
    public List<string> Recipients { get; set; } = new();

    public string? ParametersJson { get; set; }
}

public class UpdateScheduledReportRequest : CreateScheduledReportRequest
{
    public bool IsActive { get; set; } = true;
}

/// <summary>Metadata about available report types for UI dropdowns.</summary>
public class ReportTypeMetaDto
{
    public string Module { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string[] SupportedFormats { get; set; } = ["PDF", "CSV", "XLSX"];
}
