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
}