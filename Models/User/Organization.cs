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
    public string? Website { get; set; }

    /// <summary>Full street/building address line (e.g. "Barabara Plaza-JKIA, Off Airport South Road").</summary>
    public string? StreetAddress { get; set; }

    /// <summary>PO Box identifier including postal code (e.g. "41727-00100").</summary>
    public string? PoBox { get; set; }

    /// <summary>City (e.g. "Nairobi").</summary>
    public string? City { get; set; }

    /// <summary>Country (e.g. "Kenya").</summary>
    public string? Country { get; set; }

    /// <summary>Legacy combined address field — kept for backward compatibility.</summary>
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
    /// Business model for commercial weighing:
    /// "ThirdPartyWeighbridge" — public/private weighbridge charges transporters per transaction (default)
    /// "FacilityOwnedScale"    — factory/quarry/farm owns their scale, weighs their own fleet, no per-transaction fee
    /// Both models still pay TruLoad subscription fees.
    /// </summary>
    public string WeighingBusinessModel { get; set; } = "ThirdPartyWeighbridge";

    /// <summary>
    /// Flat fee charged to a transporter per commercial weighing session (KES).
    /// Only applies when WeighingBusinessModel = "ThirdPartyWeighbridge". Default 500 KES.
    /// </summary>
    public decimal CommercialWeighingFeeKes { get; set; } = 500m;

    /// <summary>
    /// Org-level default tare expiry in days for commercial weighing.
    /// Vehicles without an explicit TareExpiryDays inherit this value.
    /// Null = no expiry enforced (tares never expire). Default 90 days.
    /// </summary>
    public int? DefaultTareExpiryDays { get; set; } = 90;

    /// <summary>
    /// Optional grace period in days past tare expiry before the system hard-blocks
    /// the use of a stored tare. 0 = no grace period (block immediately on expiry).
    /// For example, if DefaultTareExpiryDays = 90 and TareGracePeriodDays = 5,
    /// tares are blocked only after 95 days since last measurement.
    /// </summary>
    public int TareGracePeriodDays { get; set; } = 0;

    /// <summary>
    /// Payment gateway used for invoices: "ecitizen_pesaflow" (default, enforcement) | "treasury" (commercial).
    /// </summary>
    public string PaymentGateway { get; set; } = "ecitizen_pesaflow";

    /// <summary>
    /// SSO tenant slug matching the auth-api tenant slug for this commercial organisation.
    /// Used to JIT-provision users via SSO token exchange. Null for enforcement tenants.
    /// </summary>
    public string? SsoTenantSlug { get; set; }

    /// <summary>
    /// Billing model override. "service_charge" = tenant pays per-transaction; bypass subscription gating.
    /// Null or empty = standard subscription model (default).
    /// Mirrors the billing_mode tenant metadata in auth-api.
    /// </summary>
    public string? BillingMode { get; set; }

    /// <summary>
    /// Demo org flag. When true, bypass all subscription enforcement.
    /// Used for sales/training organisations. Mirrors auth-api is_demo tenant metadata.
    /// </summary>
    public bool IsDemo { get; set; } = false;

    // ── Operational Allowance ────────────────────────────────────────────────────

    /// <summary>
    /// Org-specific operational tolerance in kg. Overrides the global WeighingOperationalToleranceKg setting.
    /// Used by enforcement orgs to set a custom auto-release threshold.
    /// Null = use global default (typically 200 kg from application settings).
    /// </summary>
    public int? OperationalAllowanceKg { get; set; }

    // ── Payment Settings ────────────────────────────────────────────────────────

    /// <summary>Bank name for manual payment instructions on invoices.</summary>
    public string? PaymentBankName { get; set; }
    /// <summary>Bank branch for invoice payment instructions.</summary>
    public string? PaymentBankBranch { get; set; }
    /// <summary>Bank account number for invoice payment instructions.</summary>
    public string? PaymentBankAccountNumber { get; set; }
    /// <summary>M-Pesa Paybill business number for invoice payment instructions.</summary>
    public string? PaymentMpesaPaybillNumber { get; set; }
    /// <summary>M-Pesa Till number (alternative to Paybill) for invoice payment instructions.</summary>
    public string? PaymentMpesaTillNumber { get; set; }

    // ── Audit ───────────────────────────────────────────────────────────────────

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Department> Departments { get; set; } = new List<Department>();
}
