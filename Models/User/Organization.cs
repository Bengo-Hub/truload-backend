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
    public string OrgType { get; set; } = "Government";

    /// <summary>
    /// Tenant type for module activation: CommercialWeighing (limited modules) or AxleLoadEnforcement (all modules).
    /// Null = treat as AxleLoadEnforcement for backward compatibility.
    /// </summary>
    public string? TenantType { get; set; }

    /// <summary>
    /// JSON array of enabled module keys (e.g. ["weighing","cases","prosecution"]).
    /// When null or empty, all modules are enabled for AxleLoadEnforcement; for CommercialWeighing a default set is used.
    /// </summary>
    public string? EnabledModulesJson { get; set; }
    
    /// <summary>
    /// Indicates if this is the default organization for the system
    /// </summary>
    public bool IsDefault { get; set; } = false;
    
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }

    /// <summary>Organisation logo overlaying the login page image (right panel).</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Tenant platform logo on the login form (left panel, e.g. KURA Weigh).</summary>
    public string? PlatformLogoUrl { get; set; }

    /// <summary>Login page background image (right panel).</summary>
    public string? LoginPageImageUrl { get; set; }

    /// <summary>Primary brand colour (hex, e.g. #0a9f3d).</summary>
    public string? PrimaryColor { get; set; }

    /// <summary>Secondary brand colour (hex).</summary>
    public string? SecondaryColor { get; set; }

    // ── Commercial Weighing fields ──────────────────────────────────────────────

    /// <summary>
    /// Flat fee charged to a transporter per commercial weighing session (KES).
    /// Only applies when TenantType = "CommercialWeighing". Default 500 KES.
    /// </summary>
    public decimal CommercialWeighingFeeKes { get; set; } = 500m;

    /// <summary>
    /// Payment gateway used for invoices: "ecitizen_pesaflow" (default, enforcement) | "treasury" (commercial).
    /// </summary>
    public string PaymentGateway { get; set; } = "ecitizen_pesaflow";

    /// <summary>
    /// SSO tenant slug matching the auth-api tenant slug for this commercial organisation.
    /// Used to JIT-provision users via SSO token exchange. Null for enforcement tenants.
    /// </summary>
    public string? SsoTenantSlug { get; set; }

    // ── Audit ───────────────────────────────────────────────────────────────────

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Department> Departments { get; set; } = new List<Department>();
}
