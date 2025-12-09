namespace TruLoad.Backend.Models;

/// <summary>
/// Permit type master data (2A, 3A, 3B, Overload, Special)
/// Defines weight extensions and validity rules for different permit types
/// </summary>
public class PermitType
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Permit type code (e.g., "2A", "3A", "3B", "OVERLOAD", "SPECIAL")
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Permit type name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of permit type and applicability
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Axle weight extension in kg (e.g., +3000 for 2A permit)
    /// </summary>
    public int AxleExtensionKg { get; set; }
    
    /// <summary>
    /// GVW extension in kg (e.g., +1000, +2000)
    /// </summary>
    public int GvwExtensionKg { get; set; }
    
    /// <summary>
    /// Typical validity period in days
    /// </summary>
    public int? ValidityDays { get; set; }
    
    /// <summary>
    /// Whether this permit type is currently in use
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
