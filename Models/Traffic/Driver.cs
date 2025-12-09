namespace TruLoad.Backend.Models;

/// <summary>
/// Driver information with NTSA integration and demerit points tracking.
/// Supports Kenya Traffic Act enforcement with license suspension management.
/// </summary>
public class Driver
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// NTSA (National Transport and Safety Authority) driver ID
    /// Unique identifier from national database
    /// </summary>
    public string NtsaId { get; set; } = string.Empty;
    
    /// <summary>
    /// National ID or Passport number
    /// Primary identification document
    /// </summary>
    public string IdNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Driving license number
    /// Unique per driver, issued by NTSA
    /// </summary>
    public string DrivingLicenseNo { get; set; } = string.Empty;
    
    /// <summary>
    /// Driver's full names (first and middle names)
    /// </summary>
    public string FullNames { get; set; } = string.Empty;
    
    /// <summary>
    /// Driver's surname (last name)
    /// </summary>
    public string Surname { get; set; } = string.Empty;
    
    /// <summary>
    /// Gender (Male, Female, Other)
    /// </summary>
    public string? Gender { get; set; }
    
    /// <summary>
    /// Nationality (e.g., Kenyan, Tanzanian, Ugandan)
    /// </summary>
    public string? Nationality { get; set; }
    
    /// <summary>
    /// Date of birth
    /// Used for age verification and probationary driver classification
    /// </summary>
    public DateTime? DateOfBirth { get; set; }
    
    /// <summary>
    /// Residential address
    /// </summary>
    public string? Address { get; set; }
    
    /// <summary>
    /// Contact phone number
    /// </summary>
    public string? PhoneNumber { get; set; }
    
    /// <summary>
    /// Email address
    /// </summary>
    public string? Email { get; set; }
    
    /// <summary>
    /// License class (e.g., "BCE" for commercial vehicles)
    /// Determines vehicle categories driver is authorized for
    /// </summary>
    public string? LicenseClass { get; set; }
    
    /// <summary>
    /// License issue date
    /// </summary>
    public DateTime? LicenseIssueDate { get; set; }
    
    /// <summary>
    /// License expiry date
    /// System should block weighing if expired
    /// </summary>
    public DateTime? LicenseExpiryDate { get; set; }
    
    /// <summary>
    /// Current license status
    /// - active: Valid and operational
    /// - suspended: Temporarily revoked due to violations
    /// - revoked: Permanently cancelled
    /// - expired: Past expiry date
    /// </summary>
    public string LicenseStatus { get; set; } = "active";
    
    /// <summary>
    /// Is this a professional/commercial driver?
    /// Professional drivers may have +2 point allowance in some jurisdictions
    /// </summary>
    public bool IsProfessionalDriver { get; set; } = false;
    
    /// <summary>
    /// Current total demerit points (cached calculation)
    /// Calculated from non-expired driver_demerit_records
    /// Threshold: 12 points = suspension (8 for probationary)
    /// </summary>
    public int CurrentDemeritPoints { get; set; } = 0;
    
    /// <summary>
    /// Suspension start date (if currently suspended)
    /// </summary>
    public DateTime? SuspensionStartDate { get; set; }
    
    /// <summary>
    /// Suspension end date (if currently suspended)
    /// License restored after this date
    /// </summary>
    public DateTime? SuspensionEndDate { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<DriverDemeritRecord> DemeritRecords { get; set; } = new List<DriverDemeritRecord>();
    // Future: public ICollection<Weighing> Weighings { get; set; }
    // Future: public ICollection<CaseRegister> CaseRegisters { get; set; }
}
