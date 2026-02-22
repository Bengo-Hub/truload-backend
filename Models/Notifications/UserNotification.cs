using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Models.Notifications;

/// <summary>
/// Stores in-app user notifications for the notification inbox.
/// </summary>
public class UserNotification : TenantAwareEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Type of notification: success, warning, info
    /// Maps to frontend Notification type
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Type { get; set; } = "info";

    public bool IsRead { get; set; } = false;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional URL to link to when the notification is clicked
    /// </summary>
    [MaxLength(500)]
    public string? LinkUrl { get; set; }

    /// <summary>
    /// Optional metadata related to the notification (e.g., entity IDs)
    /// </summary>
    public string? MetadataJson { get; set; }
}
