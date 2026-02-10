namespace TruLoad.Backend.DTOs.CaseManagement;

/// <summary>
/// Case Assignment Log Data Transfer Object
/// </summary>
public class CaseAssignmentLogDto
{
    public Guid Id { get; set; }
    public Guid CaseRegisterId { get; set; }
    public string? CaseNo { get; set; }
    public Guid? PreviousOfficerId { get; set; }
    public string? PreviousOfficerName { get; set; }
    public Guid NewOfficerId { get; set; }
    public string? NewOfficerName { get; set; }
    public Guid AssignedById { get; set; }
    public string? AssignedByName { get; set; }
    public string AssignmentType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public bool IsCurrent { get; set; }
    public string? OfficerRank { get; set; }
}

/// <summary>
/// Log New Assignment Request
/// </summary>
public class LogAssignmentRequest
{
    public Guid NewOfficerId { get; set; }
    public string AssignmentType { get; set; } = "initial";
    public string Reason { get; set; } = string.Empty;
    public string? OfficerRank { get; set; }
}
