using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Models.Notifications;

/// <summary>
/// Stores PWA push notification subscriptions for a user.
/// Enables targeting specific devices even if user is offline.
/// </summary>
[Table("push_subscriptions")]
public class PushSubscription : TenantAwareEntity
{
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// The push service endpoint URL.
    /// </summary>
    [Required]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// P-256DH public key (base64).
    /// </summary>
    [Required]
    public string P256dh { get; set; } = string.Empty;

    /// <summary>
    /// Authentication secret (base64).
    /// </summary>
    [Required]
    public string Auth { get; set; } = string.Empty;

    /// <summary>
    /// Device/Browser name or info (for user to manage devices).
    /// </summary>
    [MaxLength(255)]
    public string? DeviceName { get; set; }

    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser? User { get; set; }
}
