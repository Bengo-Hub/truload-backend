namespace TruLoad.Backend.DTOs.User;

public class OrganizationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? OrgType { get; set; }
    public string? TenantType { get; set; }
    public List<string>? EnabledModules { get; set; }

    /// <summary>
    /// Commercial vertical/sub-use-case classification (e.g. "waste_management", "quarry" - see
    /// Constants.CommercialVerticals), read from MetadataJson's "vertical" key. Null for
    /// unclassified orgs (every org before this feature existed, and any commercial org that
    /// hasn't picked one since) and for non-commercial (AxleLoadEnforcement) orgs.
    /// </summary>
    public string? Vertical { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }
    /// <summary>Per-tenant TruLoad app base URL (e.g. https://kuraweigh.kura.go.ke). Used for email deep links.</summary>
    public string? AppUrl { get; set; }
    public string? StreetAddress { get; set; }
    public string? PoBox { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Address { get; set; }
    public string? LogoUrl { get; set; }
    public string? PlatformLogoUrl { get; set; }
    public string? LoginPageImageUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Commercial weighing settings — only populated for CommercialWeighing tenants
    public decimal? CommercialWeighingFeeKes { get; set; }
    public int? DefaultTareExpiryDays { get; set; }
    public int TareGracePeriodDays { get; set; }
    public string? PaymentGateway { get; set; }
    public string? WeighingBusinessModel { get; set; }
}

public class CreateOrganizationRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? OrgType { get; set; }

    /// <summary>
    /// "CommercialWeighing" or "AxleLoadEnforcement" - null defaults to enforcement (back-compat).
    /// The create form has sent this field since it was added, but it had nowhere to land until now
    /// (this DTO had no matching property) - every org created via the UI silently stayed
    /// enforcement-typed regardless of what the platform owner picked, only fixable afterward via
    /// the separate module-access screen's PATCH .../modules endpoint.
    /// </summary>
    public string? TenantType { get; set; }

    /// <summary>
    /// Optional commercial vertical/sub-use-case (e.g. "waste_management", "quarry" - see
    /// Constants.CommercialVerticals). Applied server-side at creation: an unrecognised value is
    /// silently ignored rather than rejected. Meaningful for CommercialWeighing tenants; harmless
    /// (ignored on read) if set on an AxleLoadEnforcement org.
    /// </summary>
    public string? Vertical { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }
    public string? AppUrl { get; set; }
    public string? StreetAddress { get; set; }
    public string? PoBox { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Address { get; set; }
}

public class UpdateOrganizationRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? OrgType { get; set; }
    public string? TenantType { get; set; }
    public List<string>? EnabledModules { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }
    public string? AppUrl { get; set; }
    public string? StreetAddress { get; set; }
    public string? PoBox { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Address { get; set; }
    public string? LogoUrl { get; set; }
    public string? PlatformLogoUrl { get; set; }
    public string? LoginPageImageUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public bool? IsActive { get; set; }

    // Commercial weighing config — accepted from tenant admins for CommercialWeighing orgs
    public decimal? CommercialWeighingFeeKes { get; set; }
    public int? DefaultTareExpiryDays { get; set; }
}

/// <summary>
/// Request to update only organisation branding (logos, login image, colours). Used by tenant admins in system config.
/// </summary>
public class UpdateOrganizationBrandingRequest
{
    /// <summary>Organisation logo (overlay on login page right panel).</summary>
    public string? LogoUrl { get; set; }
    /// <summary>Tenant platform logo (on login form left panel).</summary>
    public string? PlatformLogoUrl { get; set; }
    /// <summary>Login page background image (right panel).</summary>
    public string? LoginPageImageUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
}

/// <summary>
/// Request to update commercial weighing settings for the current tenant (config.update).
/// Only applies to CommercialWeighing tenants.
/// </summary>
public class UpdateCommercialSettingsRequest
{
    /// <summary>Flat weighing fee charged per session (KES). Must be >= 0.</summary>
    public decimal? CommercialWeighingFeeKes { get; set; }
    /// <summary>Org-wide tare expiry in days. Set to 0 to clear (no expiry).</summary>
    public int? DefaultTareExpiryDays { get; set; }
    /// <summary>
    /// Optional grace period in days past tare expiry before hard-blocking.
    /// 0 = no grace (block immediately on expiry). Must be >= 0.
    /// </summary>
    public int? TareGracePeriodDays { get; set; }
    /// <summary>Business model: "ThirdPartyWeighbridge" or "FacilityOwnedScale".</summary>
    public string? WeighingBusinessModel { get; set; }
}

/// <summary>
/// Request to update organization tenant type and enabled modules (superuser only).
/// </summary>
public class UpdateOrganizationModulesRequest
{
    public string? TenantType { get; set; }
    public List<string>? EnabledModules { get; set; }
}
