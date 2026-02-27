namespace TruLoad.Backend.Models;

/// <summary>
/// Organization entity - Companies and government agencies
/// </summary>
public class Organization
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Organization type: Government or Private
    /// </summary>
    public string OrgType { get; set; } = "Private";
    
    /// <summary>
    /// Indicates if this is the default organization for the system
    /// </summary>
    public bool IsDefault { get; set; } = false;
    
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Department> Departments { get; set; } = new List<Department>();
}
