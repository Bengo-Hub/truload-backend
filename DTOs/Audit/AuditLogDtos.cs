using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.Audit;

/// <summary>
/// DTO for audit log entries returned from API.
/// </summary>
public record AuditLogDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string? UserName { get; init; }
    public string? UserFullName { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public Guid? ResourceId { get; init; }
    public string? ResourceName { get; init; }
    public bool Success { get; init; }
    public string? HttpMethod { get; init; }
    public string? Endpoint { get; init; }
    public int? StatusCode { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? DenialReason { get; init; }
    public string? RequiredPermission { get; init; }
    public Guid? OrganizationId { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Request parameters for querying audit logs.
/// </summary>
public class AuditLogQueryParams : PagedRequest
{
    public Guid? UserId { get; set; }
    public string? ResourceType { get; set; }
    public Guid? ResourceId { get; set; }
    public string? Action { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool? SuccessOnly { get; set; }
    public string? OrderBy { get; set; } = "CreatedAt";
}
