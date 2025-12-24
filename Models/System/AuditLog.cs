using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Models;

/// <summary>
/// Audit log entry for tracking all system changes and authorization events.
/// Used for compliance, debugging, and security auditing.
/// </summary>
[Table("audit_logs")]
public class AuditLog
{
    /// <summary>
    /// Unique audit log identifier.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// User who performed the action (from JWT auth_service_user_id).
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of action: CREATE, UPDATE, DELETE, READ, LOGIN, LOGOUT, PERMISSION_DENIED
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Resource type affected: User, Role, Station, Permission, etc.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Resource ID affected (null for non-entity actions like login/logout).
    /// </summary>
    public Guid? ResourceId { get; set; }

    /// <summary>
    /// Resource display name for easier identification.
    /// </summary>
    [MaxLength(255)]
    public string? ResourceName { get; set; }

    /// <summary>
    /// Success status: true for allowed actions, false for permission denied/errors.
    /// </summary>
    [Required]
    public bool Success { get; set; } = true;

    /// <summary>
    /// HTTP method used: GET, POST, PUT, DELETE, etc.
    /// </summary>
    [MaxLength(10)]
    public string? HttpMethod { get; set; }

    /// <summary>
    /// API endpoint called: /api/v1/users, /api/v1/roles, etc.
    /// </summary>
    [MaxLength(500)]
    public string? Endpoint { get; set; }

    /// <summary>
    /// HTTP status code returned.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Request ID for tracing across logs.
    /// </summary>
    [MaxLength(100)]
    public string? RequestId { get; set; }

    /// <summary>
    /// IP address of requester.
    /// </summary>
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent from request.
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Reason for denial (if Success = false).
    /// </summary>
    [MaxLength(500)]
    public string? DenialReason { get; set; }

    /// <summary>
    /// Required permission code for the action (if applicable).
    /// </summary>
    [MaxLength(100)]
    public string? RequiredPermission { get; set; }

    /// <summary>
    /// Organization/Tenant ID for multi-tenancy filtering.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Old values before update (JSON serialized).
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// New values after update (JSON serialized).
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// When the action was performed.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the ApplicationUser who performed the action.
    /// </summary>
    [ForeignKey("UserId")]
    public virtual ApplicationUser? User { get; set; }
}

