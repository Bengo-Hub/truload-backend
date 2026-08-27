using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models;

/// <summary>
/// Cargo type taxonomy for weighing operations
/// </summary>
public class CargoTypes : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // General, Hazardous, Perishable

    // ── Quality Parameters (used for quality deduction calculations) ──

    /// <summary>
    /// Target moisture percentage for this commodity.
    /// Used for quality deduction calculations (e.g., grain moisture content).
    /// Null means no moisture target defined.
    /// </summary>
    public decimal? MoistureTargetPercent { get; set; }

    /// <summary>
    /// Maximum allowed foreign matter percentage for this commodity.
    /// Used for quality deduction calculations (e.g., stones/chaff in grain).
    /// Null means no foreign matter limit defined.
    /// </summary>
    public decimal? ForeignMatterLimitPercent { get; set; }

    /// <summary>
    /// Organization that owns this cargo type.
    /// Null = shared/global (visible to every tenant, same as today). Non-null = tenant-specific
    /// (only visible to that organization, in addition to the shared/global rows).
    /// Mirrors the same NULL=shared convention already used by Driver.OrganizationId.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Navigation property to the owning Organization (null for shared/global rows).
    /// </summary>
    public virtual Organization? Organization { get; set; }
}