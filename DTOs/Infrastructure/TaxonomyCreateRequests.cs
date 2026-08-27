using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Infrastructure;

/// <summary>
/// Request to create a new cargo type. Distinct from the <c>CargoTypes</c> entity itself (which is
/// used directly as the request/response body for Update/GetAll etc. - this codebase's minimalist
/// convention for this controller) only because Create needs one extra, unambiguous signal:
/// <see cref="MakeGlobal"/>. A plain nullable OrganizationId field on the entity can't distinguish
/// "caller omitted it" from "caller explicitly wants it null" once JSON-bound, so a dedicated flag is
/// used instead - honored only for callers with the Superuser role (see CargoTypesController.Create).
/// </summary>
public class CreateCargoTypeRequest
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = "General";

    [Range(0, 100)]
    public decimal? MoistureTargetPercent { get; set; }

    [Range(0, 100)]
    public decimal? ForeignMatterLimitPercent { get; set; }

    /// <summary>
    /// Explicit request to create a shared/global entry (OrganizationId stays null, visible to every
    /// tenant) instead of the normal auto-stamp-to-caller's-org behavior. Only honored for callers
    /// with the Superuser role - silently ignored (the row is always org-scoped) for everyone else.
    /// </summary>
    public bool MakeGlobal { get; set; } = false;
}

/// <summary>
/// Request to create a new origin/destination location. See <see cref="CreateCargoTypeRequest"/> for
/// why this exists as a dedicated Create-only DTO rather than binding directly to the entity.
/// </summary>
public class CreateOriginDestinationRequest
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string LocationType { get; set; } = "city";

    [MaxLength(100)]
    public string Country { get; set; } = "Kenya";

    /// <summary>
    /// Explicit request to create a shared/global entry (OrganizationId stays null, visible to every
    /// tenant) instead of the normal auto-stamp-to-caller's-org behavior. Only honored for callers
    /// with the Superuser role - silently ignored (the row is always org-scoped) for everyone else.
    /// </summary>
    public bool MakeGlobal { get; set; } = false;
}
