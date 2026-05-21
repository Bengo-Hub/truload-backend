namespace TruLoad.Backend.Models.Portal;

public class PortalTeamInvitation
{
    public Guid Id { get; set; }
    public Guid TransporterId { get; set; }
    public string InvitedEmail { get; set; } = string.Empty;
    public string Role { get; set; } = "viewer"; // "manager" | "viewer"
    public string Token { get; set; } = string.Empty; // 64-char hex token
    public Guid CreatedByUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Models.Weighing.Transporter? Transporter { get; set; }
}
