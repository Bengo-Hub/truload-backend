namespace TruLoad.Backend.DTOs.User;

public class OrganizationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? OrgType { get; set; }
    public string? TenantType { get; set; }
    public List<string>? EnabledModules { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }
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
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }
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
