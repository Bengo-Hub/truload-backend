using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Models;

namespace TruLoad.Backend.Services.Interfaces.Weighing;

/// <summary>
/// Service for aggregating axle weights by group and calculating compliance.
/// Implements Kenya Traffic Act Cap 403 and EAC Act 2016 group-based compliance checking.
/// </summary>
public interface IAxleGroupAggregationService
{
    /// <summary>
    /// Aggregate axle weights by group (A, B, C, D) and calculate compliance.
    /// Applies proper tolerance: 5% for single axles, 0% for grouped axles (Tandem/Tridem).
    /// </summary>
    /// <param name="axles">Collection of weighing axles</param>
    /// <param name="legalFramework">Legal framework: EAC or TRAFFIC_ACT</param>
    /// <param name="operationalToleranceKg">Operational tolerance for warning status (default 200kg)</param>
    /// <returns>List of group results with compliance status</returns>
    Task<List<AxleGroupResultDto>> AggregateAxleGroupsAsync(
        ICollection<WeighingAxle> axles,
        string legalFramework,
        int operationalToleranceKg = 200,
        string chargingCurrency = "USD");

    /// <summary>
    /// Calculate Pavement Damage Factor using Fourth Power Law.
    /// Formula: PDF = (ActualWeight / PermissibleWeight)^4
    /// </summary>
    /// <param name="measuredKg">Measured weight in kg</param>
    /// <param name="permissibleKg">Permissible weight in kg</param>
    /// <returns>Pavement Damage Factor</returns>
    decimal CalculatePavementDamageFactor(int measuredKg, int permissibleKg);

    /// <summary>
    /// Determine axle type based on group characteristics.
    /// </summary>
    /// <param name="axleCount">Number of axles in group</param>
    /// <param name="groupLabel">Group label (A, B, C, D)</param>
    /// <returns>Axle type: Steering, SingleDrive, Tandem, Tridem, Quad</returns>
    string DetermineAxleType(int axleCount, string groupLabel);

    /// <summary>
    /// Calculate compliance for a complete weighing transaction.
    /// </summary>
    /// <param name="transactionId">Weighing transaction ID</param>
    /// <returns>Complete compliance result with fees and demerit points</returns>
    Task<WeighingComplianceResultDto> CalculateComplianceAsync(Guid transactionId);

    /// <summary>
    /// Resolves the DB-driven axle-group tolerance RULE (not a final kg amount) for a given
    /// axle-count classification and legal framework, following the same 3-level fallback chain
    /// used internally for per-transaction group compliance (act-specific AXLE tolerance -&gt;
    /// standard-law-by-axle-count -&gt; 0%/strict). Exposed separately from
    /// <see cref="AggregateAxleGroupsAsync"/> so bulk/stats callers (e.g. axle-violation
    /// distribution endpoints covering a date range) can resolve and cache the rule once per
    /// (legalFramework, isSingleAxle) combination - there are at most a handful of these
    /// system-wide - instead of re-querying ToleranceSetting per axle group in the range.
    /// Returns a fixed kg tolerance when configured, otherwise a percentage-of-permissible-weight
    /// tolerance (apply as <c>Math.Round(groupPermissibleKg * TolerancePercentage / 100m)</c>);
    /// both are 0/null-percentage when strict (no tolerance).
    /// </summary>
    Task<(int? FixedToleranceKg, decimal TolerancePercentage)> ResolveGroupToleranceRuleAsync(
        bool isSingleAxle, string legalFramework);
}