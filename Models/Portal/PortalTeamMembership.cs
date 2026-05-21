namespace TruLoad.Backend.Models.Portal;

public class PortalTeamMembership
{
    public Guid Id { get; set; }
    public Guid TransporterId { get; set; }
    public Guid UserId { get; set; }       // SSO user ID of the team member
    public string UserEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = "viewer"; // "admin" | "manager" | "viewer"
    public Guid InvitedByUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Models.Weighing.Transporter? Transporter { get; set; }
}
