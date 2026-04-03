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

    /// <summary>
    /// Date the warrant was issued by the court
    /// </summary>
    public DateTime IssuedDate { get; set; }

    /// <summary>
    /// Date the warrant was physically executed (null if not yet executed)
    /// </summary>
    public DateTime? ExecutionDate { get; set; }

    /// <summary>
    /// URL to uploaded warrant document
    /// </summary>
    public string? WarrantFileUrl { get; set; }

    /// <summary>
    /// Linked case party (defendant) ID
    /// </summary>
    public Guid? CasePartyId { get; set; }

    /// <summary>
    /// Display name of the linked defendant
    /// </summary>
    public string? CasePartyName { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Create/Track Arrest Warrant Request
/// </summary>
public class CreateArrestWarrantRequest
{
    public Guid CaseRegisterId { get; set; }
    public string AccusedName { get; set; } = string.Empty;
    public string? AccusedIdNo { get; set; }
    public string? OffenceDescription { get; set; }
    public string? IssuedBy { get; set; }

    /// <summary>
    /// Date the warrant was issued by the court (required)
    /// </summary>
    public DateTime IssuedDate { get; set; }

    /// <summary>
    /// Date the warrant was executed (optional - null means not yet executed)
    /// </summary>
    public DateTime? ExecutionDate { get; set; }

    /// <summary>
    /// URL to uploaded warrant document file
    /// </summary>
    public string? WarrantFileUrl { get; set; }

    /// <summary>
    /// FK to case party (defendant) this warrant targets
    /// </summary>
    public Guid? CasePartyId { get; set; }
}

/// <summary>
/// Execute Warrant Request
/// </summary>
public class ExecuteWarrantRequest
{
    public string ExecutionDetails { get; set; } = string.Empty;

    /// <summary>
    /// Date the warrant was physically executed
    /// </summary>
    public DateTime? ExecutionDate { get; set; }
}

/// <summary>
/// Lift Warrant Request (formerly Drop)
/// </summary>
public class LiftWarrantRequest
{
    public string LiftedReason { get; set; } = string.Empty;
}

/// <summary>
/// Drop Warrant Request (kept for backward compatibility)
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
