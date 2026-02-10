using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.CaseManagement;

/// <summary>
/// Arrest Warrant Data Transfer Object
/// </summary>
public class ArrestWarrantDto
{
    public Guid Id { get; set; }
    public Guid CaseRegisterId { get; set; }
    public string? CaseNo { get; set; }
    public string WarrantNo { get; set; } = string.Empty;
    public string? IssuedBy { get; set; }
    public string AccusedName { get; set; } = string.Empty;
    public string? AccusedIdNo { get; set; }
    public string? OffenceDescription { get; set; }
    public Guid WarrantStatusId { get; set; }
    public string? WarrantStatusName { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime? DroppedAt { get; set; }
    public string? ExecutionDetails { get; set; }
    public string? DroppedReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Create Arrest Warrant Request
/// </summary>
public class CreateArrestWarrantRequest
{
    public Guid CaseRegisterId { get; set; }
    public string AccusedName { get; set; } = string.Empty;
    public string? AccusedIdNo { get; set; }
    public string? OffenceDescription { get; set; }
    public string? IssuedBy { get; set; }
}

/// <summary>
/// Execute Warrant Request
/// </summary>
public class ExecuteWarrantRequest
{
    public string ExecutionDetails { get; set; } = string.Empty;
}

/// <summary>
/// Drop Warrant Request
/// </summary>
public class DropWarrantRequest
{
    public string DroppedReason { get; set; } = string.Empty;
}

/// <summary>
/// Search criteria for arrest warrants
/// </summary>
public class ArrestWarrantSearchCriteria : PagedRequest
{
    public Guid? CaseRegisterId { get; set; }
    public Guid? WarrantStatusId { get; set; }
    public string? AccusedName { get; set; }
    public DateTime? IssuedFrom { get; set; }
    public DateTime? IssuedTo { get; set; }
}
