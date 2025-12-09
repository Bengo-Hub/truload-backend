namespace TruLoad.Backend.DTOs;

/// <summary>
/// Summary statistics for audit logs, including entry counts, user activity, and action/resource distributions.
/// </summary>
public class AuditLogSummaryDto
{
    /// <summary>
    /// Total number of audit log entries in the specified date range.
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// Number of successful audit entries (HTTP 2xx status codes).
    /// </summary>
    public int SuccessfulEntries { get; set; }

    /// <summary>
    /// Number of failed audit entries (HTTP 4xx/5xx status codes).
    /// </summary>
    public int FailedEntries { get; set; }

    /// <summary>
    /// Count of unique users who performed actions.
    /// </summary>
    public int UniqueUsers { get; set; }

    /// <summary>
    /// Breakdown of audit entries by action type (Create, Read, Update, Delete).
    /// </summary>
    public Dictionary<string, int> ActionCounts { get; set; } = new();

    /// <summary>
    /// Breakdown of audit entries by resource type (Users, Roles, Weighings, etc.).
    /// </summary>
    public Dictionary<string, int> ResourceTypeCounts { get; set; } = new();

    /// <summary>
    /// Success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate => TotalEntries > 0 ? (double)SuccessfulEntries / TotalEntries * 100 : 0;
}
