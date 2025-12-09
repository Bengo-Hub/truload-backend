namespace TruLoad.Backend.Models;

/// <summary>
/// Department entity - Departments within organizations
/// </summary>
public class Department
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public ICollection<User> Users { get; set; } = new List<User>();
}
