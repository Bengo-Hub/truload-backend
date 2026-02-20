using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TruLoad.Backend.Models.Identity;

/// <summary>
/// Server-side refresh token entity for secure token rotation.
/// Tokens are stored hashed. On refresh, old token is revoked and new one issued.
/// </summary>
[Table("RefreshTokens")]
public class RefreshToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// SHA-256 hash of the token value. Never store raw tokens.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// If this token was replaced by rotation, points to the replacement.
    /// </summary>
    public Guid? ReplacedByTokenId { get; set; }

    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    [NotMapped]
    public bool IsRevoked => RevokedAt.HasValue;

    [NotMapped]
    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}
