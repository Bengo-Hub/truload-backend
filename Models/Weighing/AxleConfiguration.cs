namespace TruLoad.Backend.Models;

/// <summary>
/// Unified axle configuration master data - Stores BOTH standard (EAC-defined) and derived (user-created) patterns.
/// 
/// Standard configurations (is_standard=TRUE):
///   - EAC-defined immutable patterns (e.g., "2*", "2A", "3A", "4B", "5C", "6D", "7A", "7B")
///   - Simple codes, created during seeding, cannot be modified
///   - created_by_user_id is NULL
///   - Always available for weighing operations
/// 
/// Derived configurations (is_standard=FALSE):
///   - User-created custom patterns (e.g., "5*S|DD|DD|", "3*S|DW||", "6*SDDWWW")
///   - Complex pipe notation encoding tyre types per position
///   - created_by_user_id tracks creator, guided frontend validation ensures compliance
///   - Users create via frontend with backend compliance checks
///
/// Unified table design simplifies relationships and avoids confusing separate tables.
/// </summary>
public class AxleConfiguration
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Axle code (unique identifier)
    /// Standard: "2*", "3A", "4B", "5C"
    /// Derived: "5*S|DD|DD|", "3*S|DW||" (pipe notation for tyre types per position)
    /// </summary>
    public string AxleCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name (e.g., "Two Axle", "Three Axle A Pattern", "Five Axle Custom")
    /// </summary>
    public string AxleName { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed description of the configuration
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Total number of axles in this configuration
    /// </summary>
    public int AxleNumber { get; set; }
    
    /// <summary>
    /// Gross Vehicle Weight (GVW) permissible in kg
    /// 0 if not specified, otherwise specific GVW limit
    /// </summary>
    public int GvwPermissibleKg { get; set; }
    
    /// <summary>
    /// Is this a standard EAC-defined configuration?
    /// TRUE = Standard (immutable, created_by_user_id = NULL)
    /// FALSE = Derived custom configuration (user-created)
    /// </summary>
    public bool IsStandard { get; set; } = false;
    
    /// <summary>
    /// Legal framework applicability (EAC, TRAFFIC_ACT, or BOTH)
    /// Determines which fee schedule and tolerance rules apply
    /// </summary>
    public string LegalFramework { get; set; } = "BOTH";
    
    /// <summary>
    /// Optional visual diagram URL for reference
    /// </summary>
    public string? VisualDiagramUrl { get; set; }
    
    /// <summary>
    /// Optional notes or special rules for this configuration
    /// </summary>
    public string? Notes { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; } // Soft delete support
    
    /// <summary>
    /// User who created derived configuration (NULL for standard configs)
    /// </summary>
    public Guid? CreatedByUserId { get; set; }
    
    // Navigation properties
    public User? CreatedByUser { get; set; }
    public ICollection<AxleWeightReference> AxleWeightReferences { get; set; } = new List<AxleWeightReference>();
    public ICollection<WeighingAxle> WeighingAxles { get; set; } = new List<WeighingAxle>();
}
