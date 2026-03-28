using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Services.Interfaces.Weighing;

namespace TruLoad.Backend.Services.Implementations.Weighing;

/// <summary>
/// Service for aggregating axle weights by group and calculating compliance.
/// Implements Kenya Traffic Act Cap 403 and EAC Act 2016 group-based compliance checking.
///
/// Key Regulatory Requirements:
/// - Axle and GVW tolerances are DB-driven via ToleranceSetting per legal framework
/// - Pavement Damage Factor using Fourth Power Law: (Actual/Permissible)^4
/// </summary>
public class AxleGroupAggregationService : IAxleGroupAggregationService
{
    private readonly IWeighingRepository _weighingRepository;
    private readonly IToleranceRepository _toleranceRepository;
    private readonly IAxleTypeFeeRepository _axleTypeFeeRepository;
    private readonly IAxleFeeScheduleRepository _gvwFeeRepository;
    private readonly IDemeritPointsRepository _demeritRepository;

    public AxleGroupAggregationService(
        IWeighingRepository weighingRepository,
        IToleranceRepository toleranceRepository,
        IAxleTypeFeeRepository axleTypeFeeRepository,
        IAxleFeeScheduleRepository gvwFeeRepository,
        IDemeritPointsRepository demeritRepository)
    {
        _weighingRepository = weighingRepository;
        _toleranceRepository = toleranceRepository;
        _axleTypeFeeRepository = axleTypeFeeRepository;
        _gvwFeeRepository = gvwFeeRepository;
        _demeritRepository = demeritRepository;
    }

    /// <inheritdoc />
    public async Task<List<AxleGroupResultDto>> AggregateAxleGroupsAsync(
        ICollection<WeighingAxle> axles,
        string legalFramework,
        int operationalToleranceKg = 200,
        string chargingCurrency = "USD")
    {
        if (axles == null || !axles.Any())
            return [];

        var results = new List<AxleGroupResultDto>();

        // Group axles by AxleGrouping (A, B, C, D)
        var groups = axles
            .GroupBy(a => a.AxleGrouping)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var group in groups)
        {
            var groupLabel = group.Key;
            var groupAxles = group.OrderBy(a => a.AxleNumber).ToList();
            var axleCount = groupAxles.Count;

            // Calculate group aggregates
            int groupWeightKg = groupAxles.Sum(a => a.MeasuredWeightKg);
            int groupPermissibleKg = groupAxles.Sum(a => a.PermissibleWeightKg);

            // Determine axle type
            string axleType = DetermineAxleType(axleCount, groupLabel);

            // Apply tolerance from DB settings (per legal framework)
            int toleranceKg = await CalculateGroupToleranceAsync(groupAxles, groupPermissibleKg, legalFramework);
            int effectiveLimitKg = groupPermissibleKg + toleranceKg;
            
            // Apply Additive Operational Allowance (if configured)
            int effectiveWeightForOverload = groupWeightKg - operationalToleranceKg; 
            int overloadKg = Math.Max(0, groupWeightKg - effectiveLimitKg);

            // Calculate Pavement Damage Factor
            decimal pdf = CalculatePavementDamageFactor(groupWeightKg, groupPermissibleKg);

            // Calculate fee for this group if overloaded
            decimal feeUsd = 0m;
            decimal feeKes = 0m;
            int demeritPoints = 0;

            if (overloadKg > 0)
            {
                // Traffic Act Cap 403: No fees or demerit points on axle overloads
                // EAC Act 2016: Fees and points applied to BOTH axle and GVW
                if (!legalFramework.Equals("TRAFFIC_ACT", StringComparison.OrdinalIgnoreCase))
                {
                    // Calculate fees in the act's charging currency
                    if (chargingCurrency.Equals("KES", StringComparison.OrdinalIgnoreCase))
                    {
                        feeKes = await _axleTypeFeeRepository.CalculateFeeAsync(
                            legalFramework, axleType, overloadKg, "KES");
                    }
                    else
                    {
                        feeUsd = await _axleTypeFeeRepository.CalculateFeeAsync(
                            legalFramework, axleType, overloadKg, "USD");
                    }

                    // Convert AxleType to violation type for demerit lookup
                    string violationType = axleType.ToUpper() switch
                    {
                        "STEERING" => "STEERING",
                        "SINGLEDRIVE" => "SINGLE_DRIVE",
                        "TANDEM" => "TANDEM",
                        "TRIDEM" => "TRIDEM",
                        "QUAD" => "TRIDEM", // Use tridem rate for quad
                        _ => "SINGLE_DRIVE"
                    };

                    demeritPoints = await _demeritRepository.CalculatePointsAsync(
                        legalFramework, violationType, overloadKg);
                }
            }

            // Determine status
            string status = DetermineStatus(overloadKg, operationalToleranceKg);

            results.Add(new AxleGroupResultDto
            {
                GroupLabel = groupLabel,
                AxleType = axleType,
                AxleCount = axleCount,
                GroupWeightKg = groupWeightKg,
                GroupPermissibleKg = groupPermissibleKg,
                ToleranceKg = toleranceKg,
                EffectiveLimitKg = effectiveLimitKg,
                OverloadKg = overloadKg,
                PavementDamageFactor = pdf,
                Status = status,
                OperationalToleranceKg = operationalToleranceKg,
                FeeUsd = feeUsd,
                FeeKes = feeKes,
                DemeritPoints = demeritPoints,
                Axles = groupAxles.Select(a => new AxleDetailDto
                {
                    AxleNumber = a.AxleNumber,
                    MeasuredWeightKg = a.MeasuredWeightKg,
                    PermissibleWeightKg = a.PermissibleWeightKg,
                    OverloadKg = a.OverloadKg,
                    TyreType = a.TyreType?.Code,
                    SpacingMeters = a.AxleSpacingMeters
                }).ToList()
            });
        }

        return results;
    }

    /// <inheritdoc />
    public decimal CalculatePavementDamageFactor(int measuredKg, int permissibleKg)
    {
        if (permissibleKg <= 0) return 0m;

        var ratio = (double)measuredKg / permissibleKg;
        var pdf = Math.Pow(ratio, 4);

        return (decimal)Math.Round(pdf, 4);
    }

    /// <inheritdoc />
    public string DetermineAxleType(int axleCount, string groupLabel)
    {
        // Group A is typically steering (front axle)
        if (groupLabel.Equals("A", StringComparison.OrdinalIgnoreCase) && axleCount == 1)
        {
            return "Steering";
        }

        return axleCount switch
        {
            1 => "SingleDrive",
            2 => "Tandem",
            3 => "Tridem",
            4 => "Quad",
            _ => "SingleDrive"
        };
    }

    /// <inheritdoc />
    public async Task<WeighingComplianceResultDto> CalculateComplianceAsync(Guid transactionId)
    {
        // Get transaction with axles
        var transaction = await _weighingRepository.GetTransactionByIdAsync(transactionId);
        if (transaction == null)
            throw new KeyNotFoundException($"Weighing transaction {transactionId} not found");

        var result = new WeighingComplianceResultDto
        {
            WeighingId = transactionId,
            TicketNumber = transaction.TicketNumber ?? string.Empty,
            VehicleRegNumber = transaction.VehicleRegNumber
        };

        if (transaction.WeighingAxles == null || !transaction.WeighingAxles.Any())
        {
            result.OverallStatus = "Pending";
            return result;
        }

        // Determine legal framework from the transaction's act (fall back to Traffic Act)
        string legalFramework = transaction.Act?.ActType?.Equals("EAC", StringComparison.OrdinalIgnoreCase) == true
            ? "EAC"
            : "TRAFFIC_ACT";

        // Determine charging currency from the act
        string chargingCurrency = transaction.Act?.ChargingCurrency ?? "KES";

        // Get operational tolerance settings
        var opAllowanceSetting = await _toleranceRepository.GetByCodeAsync("OPERATIONAL_ALLOWANCE");
        int operationalAllowanceKg = opAllowanceSetting?.ToleranceKg ?? 200;

        var opWarningSetting = await _toleranceRepository.GetByCodeAsync("OPERATIONAL_TOLERANCE");
        int operationalWarningKg = opWarningSetting?.ToleranceKg ?? 200;

        // Aggregate axle groups with currency-aware fee calculation
        var groupResults = await AggregateAxleGroupsAsync(
            transaction.WeighingAxles.ToList(),
            legalFramework,
            operationalAllowanceKg, // Used as additive buffer in AggregateAxleGroupsAsync? 
            chargingCurrency);

        // Update results to use the Warning threshold for status
        foreach (var gr in groupResults)
        {
            gr.Status = DetermineStatus(gr.OverloadKg, operationalWarningKg);
            gr.OperationalToleranceKg = operationalWarningKg;
        }

        result.GroupResults = groupResults;
        result.OperationalToleranceKg = operationalWarningKg;
        result.OperationalAllowanceUsed = operationalAllowanceKg;
        result.ChargingCurrency = chargingCurrency;

        // Calculate GVW compliance with DB-driven tolerance
        result.GvwMeasuredKg = transaction.WeighingAxles.Sum(a => a.MeasuredWeightKg);
        result.GvwPermissibleKg = transaction.GvwPermissibleKg;

        // Fetch GVW tolerance from database (per legal framework)
        int gvwToleranceKg = await _toleranceRepository.CalculateToleranceKgAsync(
            legalFramework, "GVW", result.GvwPermissibleKg);
        var gvwToleranceSetting = await _toleranceRepository.GetToleranceAsync(legalFramework, "GVW");

        // Fetch Axle tolerance setting for display
        var axleToleranceSetting = await _toleranceRepository.GetToleranceAsync(legalFramework, "AXLE");

        result.GvwToleranceKg = gvwToleranceKg;
        result.GvwEffectiveLimitKg = result.GvwPermissibleKg + gvwToleranceKg;
        result.GvwToleranceDisplay = BuildToleranceDisplay(gvwToleranceSetting, gvwToleranceKg);
        result.AxleToleranceDisplay = BuildToleranceDisplay(axleToleranceSetting, groupResults.FirstOrDefault()?.ToleranceKg ?? 0);
        result.GvwOverloadKg = Math.Max(0, result.GvwMeasuredKg - result.GvwEffectiveLimitKg);

        // Calculate fees in the act's charging currency
        result.TotalAxleFeeUsd = groupResults.Sum(g => g.FeeUsd);
        result.TotalAxleFeeKes = groupResults.Sum(g => g.FeeKes);

        if (result.GvwOverloadKg > 0)
        {
            var gvwFeeResult = await _gvwFeeRepository.CalculateFeeAsync(
                legalFramework, "GVW", result.GvwOverloadKg, chargingCurrency);

            if (chargingCurrency.Equals("KES", StringComparison.OrdinalIgnoreCase))
            {
                result.GvwFeeKes = gvwFeeResult?.FeeAmountUsd ?? 0m; // FeeAmountUsd returns the calculated amount regardless of currency name
            }
            else
            {
                result.GvwFeeUsd = gvwFeeResult?.FeeAmountUsd ?? 0m;
            }
        }

        // EAC Rule: Total fee is MAX(GVW fee, sum of axle fees)
        // Traffic Act: Charges on GVW only (not per-axle)
        if (legalFramework.Equals("TRAFFIC_ACT", StringComparison.OrdinalIgnoreCase))
        {
            // Traffic Act Cap 403: GVW-based flat fee only
            result.TotalFeeKes = result.GvwFeeKes;
            result.TotalFeeUsd = result.GvwFeeUsd;
        }
        else
        {
            // EAC Act 2016: MAX(GVW fee, sum of axle fees)
            result.TotalFeeUsd = Math.Max(result.GvwFeeUsd, result.TotalAxleFeeUsd);
            result.TotalFeeKes = Math.Max(result.GvwFeeKes, result.TotalAxleFeeKes);
        }

        // Calculate demerit points
        var demeritResult = new DemeritPointsResultDto();
        var demeritBreakdown = new List<DemeritPointBreakdownDto>();

        // Add group demerit points
        foreach (var group in groupResults.Where(g => g.DemeritPoints > 0))
        {
            demeritBreakdown.Add(new DemeritPointBreakdownDto
            {
                ViolationType = group.AxleType.ToUpper(),
                OverloadKg = group.OverloadKg,
                Points = group.DemeritPoints
            });
        }

        // Add GVW demerit points
        if (result.GvwOverloadKg > 0)
        {
            int gvwPoints = await _demeritRepository.CalculatePointsAsync(
                legalFramework, "GVW", result.GvwOverloadKg);

            if (gvwPoints > 0)
            {
                demeritBreakdown.Add(new DemeritPointBreakdownDto
                {
                    ViolationType = "GVW",
                    OverloadKg = result.GvwOverloadKg,
                    Points = gvwPoints
                });
            }
        }

        demeritResult.Breakdown = demeritBreakdown;
        demeritResult.TotalPoints = demeritBreakdown.Sum(d => d.Points);

        // Get applicable penalty
        if (demeritResult.TotalPoints > 0)
        {
            var penalty = await _demeritRepository.GetPenaltyScheduleAsync(demeritResult.TotalPoints);
            if (penalty != null)
            {
                demeritResult.ApplicablePenalty = new PenaltyDto
                {
                    Description = penalty.PenaltyDescription,
                    SuspensionDays = penalty.SuspensionDays,
                    RequiresCourt = penalty.RequiresCourt,
                    AdditionalFineUsd = penalty.AdditionalFineUsd,
                    AdditionalFineKes = penalty.AdditionalFineKes
                };
                demeritResult.RequiresCourt = penalty.RequiresCourt;
                demeritResult.SuspensionDays = penalty.SuspensionDays;
            }
        }

        result.DemeritPoints = demeritResult;

        // Build violation reasons
        var violations = new List<string>();

        foreach (var group in groupResults.Where(g => g.Status == "OVERLOAD"))
        {
            violations.Add($"Axle Group {group.GroupLabel} ({group.AxleType}) Overload: {group.OverloadKg}kg");
        }

        if (result.GvwOverloadKg > 0)
        {
            violations.Add($"GVW Overload: {result.GvwOverloadKg}kg (Permissible: {result.GvwPermissibleKg}kg, Tolerance: {result.GvwToleranceDisplay})");
        }

        result.ViolationReasons = violations;

        // Determine overall status
        // GVW is the primary compliance check (drives fees, yard, prosecution).
        // Per-axle overloads are informational/diagnostic — they affect PDF factor
        // but do NOT override a compliant GVW determination for enforcement.
        bool gvwOverloaded = result.GvwOverloadKg > 0;
        bool hasAxleOverload = groupResults.Any(g => g.Status == "OVERLOAD");
        bool hasAxleWarning = groupResults.Any(g => g.Status == "WARNING");

        if (gvwOverloaded)
        {
            // GVW exceeds effective limit (permissible + tolerance) → Overloaded
            result.OverallStatus = "Overloaded";
            result.IsCompliant = false;
            result.ShouldSendToYard = true;
        }
        else if (hasAxleOverload)
        {
            // GVW is within tolerance but individual axle groups exceed their limits.
            // Warning status — vehicle is within GVW tolerance but has axle distribution issues.
            result.OverallStatus = "Warning";
            result.IsCompliant = false;
            result.ShouldSendToYard = false; // Not yard-worthy when GVW is compliant
        }
        else if (hasAxleWarning)
        {
            // Axles within operational tolerance, GVW within tolerance
            result.OverallStatus = "Warning";
            result.IsCompliant = false;
            result.ShouldSendToYard = false;
        }
        else
        {
            result.OverallStatus = "Compliant";
            result.IsCompliant = true;
            result.ShouldSendToYard = false;
        }

        return result;
    }

    /// <summary>
    /// Calculate tolerance for an axle group using DB-driven tolerance settings.
    /// Fallback chain: Act-specific → AxleConfig-specific → 0% (strict).
    /// No hardcoded percentage fallbacks — the DB is the single source of truth.
    /// </summary>
    private async Task<int> CalculateGroupToleranceAsync(List<WeighingAxle> axles, int groupPermissibleKg, string legalFramework)
    {
        // 1. Act-Specific Regulatory Tolerance (highest priority)
        //    e.g. TRAFFIC_ACT_AXLE_TOLERANCE = 0%, EAC_AXLE_TOLERANCE = 5%
        var toleranceKg = await _toleranceRepository.CalculateToleranceKgAsync(
            legalFramework, "AXLE", groupPermissibleKg);
        if (toleranceKg > 0)
            return toleranceKg;

        // 2. Config-Specific Tolerance (per-axle configuration overrides)
        //    If multiple axles in group have different tolerances, use the MAX
        var configTolerances = axles
            .Select(a => {
                if (a.AxleConfiguration?.ToleranceKg > 0) return a.AxleConfiguration.ToleranceKg.Value;
                if (a.AxleConfiguration?.TolerancePercentage > 0)
                    return (int)Math.Round(a.PermissibleWeightKg * (a.AxleConfiguration.TolerancePercentage.Value / 100m));
                return 0;
            })
            .ToList();

        if (configTolerances.Any(t => t > 0))
            return configTolerances.Max();

        // 3. Strict (0%) — no tolerance found from Act or Config
        return 0;
    }

    /// <summary>
    /// Determine compliance status based on overload amount.
    /// </summary>
    private static string DetermineStatus(int overloadKg, int operationalToleranceKg)
    {
        if (overloadKg <= 0)
            return "LEGAL";

        if (overloadKg <= operationalToleranceKg)
            return "WARNING";

        return "OVERLOAD";
    }

    /// <summary>
    /// Build a human-readable tolerance display string from a tolerance setting.
    /// Examples: "5%", "2,000 kg", "0% (strict)"
    /// </summary>
    private static string BuildToleranceDisplay(ToleranceSetting? setting, int calculatedToleranceKg)
    {
        if (setting == null || calculatedToleranceKg == 0)
            return "0% (strict)";

        // If tolerance is percentage-based
        if (setting.TolerancePercentage > 0)
            return $"{setting.TolerancePercentage:0.##}%";

        // If tolerance is fixed kg
        if (setting.ToleranceKg.HasValue && setting.ToleranceKg.Value > 0)
            return $"{setting.ToleranceKg.Value:N0} kg";

        return "0% (strict)";
    }
}
