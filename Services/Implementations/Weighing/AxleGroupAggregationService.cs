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
/// - 5% tolerance for SINGLE axles only
/// - 0% tolerance for grouped axles (Tandem, Tridem)
/// - 0% tolerance for GVW
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

            // Apply tolerance based on Kenya Traffic Act Cap 403:
            // - 5% for single axles ONLY
            // - 0% for grouped axles (Tandem, Tridem) - STRICT enforcement
            int toleranceKg = CalculateGroupTolerance(axleCount, groupPermissibleKg, legalFramework);
            int effectiveLimitKg = groupPermissibleKg + toleranceKg;
            int overloadKg = Math.Max(0, groupWeightKg - effectiveLimitKg);

            // Calculate Pavement Damage Factor
            decimal pdf = CalculatePavementDamageFactor(groupWeightKg, groupPermissibleKg);

            // Calculate fee for this group if overloaded
            decimal feeUsd = 0m;
            decimal feeKes = 0m;
            int demeritPoints = 0;

            if (overloadKg > 0)
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

        // Get operational tolerance
        var operationalTolerance = await _toleranceRepository.GetByCodeAsync("OPERATIONAL_TOLERANCE");
        int operationalToleranceKg = operationalTolerance?.ToleranceKg ?? 200;

        // Aggregate axle groups with currency-aware fee calculation
        var groupResults = await AggregateAxleGroupsAsync(
            transaction.WeighingAxles.ToList(),
            legalFramework,
            operationalToleranceKg,
            chargingCurrency);

        result.GroupResults = groupResults;
        result.OperationalToleranceKg = operationalToleranceKg;
        result.ChargingCurrency = chargingCurrency;

        // Calculate GVW compliance (0% tolerance)
        result.GvwMeasuredKg = transaction.WeighingAxles.Sum(a => a.MeasuredWeightKg);
        result.GvwPermissibleKg = transaction.GvwPermissibleKg;
        result.GvwOverloadKg = Math.Max(0, result.GvwMeasuredKg - result.GvwPermissibleKg);

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
        // Traffic Act: Primarily charges on GVW, but same MAX rule applies
        result.TotalFeeUsd = Math.Max(result.GvwFeeUsd, result.TotalAxleFeeUsd);
        result.TotalFeeKes = Math.Max(result.GvwFeeKes, result.TotalAxleFeeKes);

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
            violations.Add($"GVW Overload: {result.GvwOverloadKg}kg (Permissible: {result.GvwPermissibleKg}kg, 0% tolerance)");
        }

        result.ViolationReasons = violations;

        // Determine overall status
        bool hasOverload = groupResults.Any(g => g.Status == "OVERLOAD") || result.GvwOverloadKg > 0;
        bool hasWarning = groupResults.Any(g => g.Status == "WARNING");

        if (!hasOverload && !hasWarning)
        {
            result.OverallStatus = "Compliant";
            result.IsCompliant = true;
            result.ShouldSendToYard = false;
        }
        else if (hasOverload)
        {
            result.OverallStatus = "Overloaded";
            result.IsCompliant = false;
            result.ShouldSendToYard = true;
        }
        else
        {
            // Warning only (within operational tolerance)
            result.OverallStatus = "Warning";
            result.IsCompliant = false;
            result.ShouldSendToYard = false; // Auto-release
        }

        return result;
    }

    /// <summary>
    /// Calculate tolerance for a group based on Kenya Traffic Act Cap 403.
    /// CRITICAL: 5% tolerance for SINGLE axles only, 0% for grouped axles.
    /// </summary>
    private int CalculateGroupTolerance(int axleCount, int groupPermissibleKg, string legalFramework)
    {
        // Kenya Traffic Act Cap 403 Schedule 2:
        // - Single axles: 5% tolerance
        // - Tandem axles (2 axles): 0% tolerance (STRICT)
        // - Tridem axles (3 axles): 0% tolerance (STRICT)
        // - GVW: 0% tolerance (STRICT)

        if (axleCount == 1)
        {
            // 5% tolerance for single axles
            return (int)Math.Round(groupPermissibleKg * 0.05m);
        }

        // 0% tolerance for grouped axles (Tandem, Tridem, etc.)
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
}
