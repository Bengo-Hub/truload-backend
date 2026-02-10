using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Prosecution;
using TruLoad.Backend.Models.Prosecution;
using TruLoad.Backend.Services.Interfaces.Prosecution;
using TruLoad.Backend.Services.Interfaces.Weighing;

namespace TruLoad.Backend.Services.Implementations.Prosecution;

/// <summary>
/// Service implementation for prosecution case management.
/// Handles charge calculation, case creation, and status tracking.
/// </summary>
public class ProsecutionService : IProsecutionService
{
    private readonly TruLoadDbContext _context;
    private readonly IAxleGroupAggregationService _axleGroupService;

    // Default forex rate (should be fetched from external API in production)
    private const decimal DefaultForexRate = 130.0m;

    public ProsecutionService(
        TruLoadDbContext context,
        IAxleGroupAggregationService axleGroupService)
    {
        _context = context;
        _axleGroupService = axleGroupService;
    }

    public async Task<ProsecutionCaseDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var prosecutionCase = await _context.ProsecutionCases
            .Include(p => p.CaseRegister)
            .Include(p => p.Weighing)
            .Include(p => p.ProsecutionOfficer)
            .Include(p => p.Act)
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, ct);

        return prosecutionCase == null ? null : MapToDto(prosecutionCase);
    }

    public async Task<ProsecutionCaseDto?> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var prosecutionCase = await _context.ProsecutionCases
            .Include(p => p.CaseRegister)
            .Include(p => p.Weighing)
            .Include(p => p.ProsecutionOfficer)
            .Include(p => p.Act)
            .FirstOrDefaultAsync(p => p.CaseRegisterId == caseRegisterId && p.DeletedAt == null, ct);

        return prosecutionCase == null ? null : MapToDto(prosecutionCase);
    }

    public async Task<IEnumerable<ProsecutionCaseDto>> SearchAsync(ProsecutionSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = _context.ProsecutionCases
            .Include(p => p.CaseRegister)
            .Include(p => p.Weighing)
            .Include(p => p.ProsecutionOfficer)
            .Include(p => p.Act)
            .Where(p => p.DeletedAt == null)
            .AsQueryable();

        if (criteria.CaseRegisterId.HasValue)
            query = query.Where(p => p.CaseRegisterId == criteria.CaseRegisterId.Value);

        if (criteria.WeighingId.HasValue)
            query = query.Where(p => p.WeighingId == criteria.WeighingId.Value);

        if (criteria.ActId.HasValue)
            query = query.Where(p => p.ActId == criteria.ActId.Value);

        if (!string.IsNullOrWhiteSpace(criteria.Status))
            query = query.Where(p => p.Status == criteria.Status);

        if (criteria.CreatedFrom.HasValue)
            query = query.Where(p => p.CreatedAt >= criteria.CreatedFrom.Value);

        if (criteria.CreatedTo.HasValue)
            query = query.Where(p => p.CreatedAt <= criteria.CreatedTo.Value);

        if (criteria.MinTotalFee.HasValue)
            query = query.Where(p => p.TotalFeeUsd >= criteria.MinTotalFee.Value);

        if (criteria.MaxTotalFee.HasValue)
            query = query.Where(p => p.TotalFeeUsd <= criteria.MaxTotalFee.Value);

        var prosecutionCases = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return prosecutionCases.Select(MapToDto);
    }

    public async Task<ChargeCalculationResult> CalculateChargesAsync(Guid weighingId, string legalFramework, CancellationToken ct = default)
    {
        var weighing = await _context.WeighingTransactions
            .Include(w => w.WeighingAxles)
            .FirstOrDefaultAsync(w => w.Id == weighingId, ct)
            ?? throw new InvalidOperationException($"Weighing transaction {weighingId} not found");

        // Calculate compliance with group-based fees
        var compliance = await _axleGroupService.CalculateComplianceAsync(weighingId);

        // Get forex rate (in production, fetch from external API)
        var forexRate = DefaultForexRate;

        // Calculate GVW-based fee
        var gvwOverloadKg = weighing.OverloadKg;
        var gvwFeeUsd = await CalculateGvwFeeAsync(gvwOverloadKg, legalFramework, ct);
        var gvwFeeKes = gvwFeeUsd * forexRate;

        // Get axle-based fees from compliance results
        var maxAxleOverloadKg = compliance.GroupResults.Any() ? compliance.GroupResults.Max(g => g.OverloadKg) : 0;
        var totalAxleFeeUsd = compliance.GroupResults.Sum(g => g.FeeUsd);
        var totalAxleFeeKes = totalAxleFeeUsd * forexRate;

        // Determine best charge basis (higher of GVW or Axle fees)
        var bestChargeBasis = gvwFeeUsd >= totalAxleFeeUsd ? "gvw" : "axle";
        var baseFeeUsd = bestChargeBasis == "gvw" ? gvwFeeUsd : totalAxleFeeUsd;

        // Check for repeat offender (within 12 months)
        var (isRepeatOffender, priorOffenseCount, multiplier) = await CheckRepeatOffenderAsync(weighing.VehicleId, ct);

        var totalFeeUsd = baseFeeUsd * multiplier;
        var totalFeeKes = totalFeeUsd * forexRate;

        // Get demerit points from compliance result
        var demeritPoints = compliance.DemeritPoints?.TotalPoints ?? 0;

        return new ChargeCalculationResult
        {
            WeighingId = weighingId,
            LegalFramework = legalFramework,
            GvwOverloadKg = gvwOverloadKg,
            GvwFeeUsd = gvwFeeUsd,
            GvwFeeKes = gvwFeeKes,
            MaxAxleOverloadKg = maxAxleOverloadKg,
            MaxAxleFeeUsd = totalAxleFeeUsd,
            MaxAxleFeeKes = totalAxleFeeKes,
            AxleBreakdown = compliance.GroupResults.Select(g => new AxleChargeBreakdown
            {
                AxleType = g.AxleType,
                AxleNumber = g.Axles.FirstOrDefault()?.AxleNumber ?? 0,
                MeasuredKg = g.GroupWeightKg,
                PermissibleKg = g.GroupPermissibleKg,
                OverloadKg = g.OverloadKg,
                FeeUsd = g.FeeUsd,
                FeeKes = g.FeeUsd * forexRate
            }).ToList(),
            BestChargeBasis = bestChargeBasis,
            TotalFeeUsd = totalFeeUsd,
            TotalFeeKes = totalFeeKes,
            PenaltyMultiplier = multiplier,
            IsRepeatOffender = isRepeatOffender,
            PriorOffenseCount = priorOffenseCount,
            DemeritPoints = demeritPoints,
            ForexRate = forexRate,
            CalculatedAt = DateTime.UtcNow
        };
    }

    public async Task<ProsecutionCaseDto> CreateFromCaseAsync(Guid caseRegisterId, CreateProsecutionRequest request, Guid userId, CancellationToken ct = default)
    {
        // Verify case exists
        var caseRegister = await _context.CaseRegisters
            .FirstOrDefaultAsync(c => c.Id == caseRegisterId && c.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Case {caseRegisterId} not found");

        // Check if prosecution already exists
        var existingProsecution = await _context.ProsecutionCases
            .FirstOrDefaultAsync(p => p.CaseRegisterId == caseRegisterId && p.DeletedAt == null, ct);
        if (existingProsecution != null)
            throw new InvalidOperationException($"Prosecution case already exists for case {caseRegister.CaseNo}");

        // Calculate charges if not provided
        var chargeCalculation = request.ChargeCalculation;
        if (chargeCalculation == null && caseRegister.WeighingId.HasValue)
        {
            var legalFramework = request.ActId != Guid.Empty
                ? await GetLegalFrameworkCodeAsync(request.ActId, ct)
                : "TRAFFIC_ACT";

            chargeCalculation = await CalculateChargesAsync(caseRegister.WeighingId.Value, legalFramework, ct);
        }

        // Generate certificate number
        var certificateNo = await GenerateCertificateNumberAsync(ct);

        var prosecutionCase = new ProsecutionCase
        {
            Id = Guid.NewGuid(),
            CaseRegisterId = caseRegisterId,
            WeighingId = caseRegister.WeighingId,
            ProsecutionOfficerId = userId,
            ActId = request.ActId,
            GvwOverloadKg = chargeCalculation?.GvwOverloadKg ?? 0,
            GvwFeeUsd = chargeCalculation?.GvwFeeUsd ?? 0,
            GvwFeeKes = chargeCalculation?.GvwFeeKes ?? 0,
            MaxAxleOverloadKg = chargeCalculation?.MaxAxleOverloadKg ?? 0,
            MaxAxleFeeUsd = chargeCalculation?.MaxAxleFeeUsd ?? 0,
            MaxAxleFeeKes = chargeCalculation?.MaxAxleFeeKes ?? 0,
            BestChargeBasis = chargeCalculation?.BestChargeBasis ?? "gvw",
            PenaltyMultiplier = chargeCalculation?.PenaltyMultiplier ?? 1.0m,
            OffenseCount = chargeCalculation?.PriorOffenseCount ?? 0,
            DemeritPoints = chargeCalculation?.DemeritPoints ?? 0,
            TotalFeeUsd = chargeCalculation?.TotalFeeUsd ?? 0,
            TotalFeeKes = chargeCalculation?.TotalFeeKes ?? 0,
            ForexRate = chargeCalculation?.ForexRate ?? DefaultForexRate,
            CertificateNo = certificateNo,
            CaseNotes = request.CaseNotes,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ProsecutionCases.Add(prosecutionCase);
        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(prosecutionCase.Id, ct))!;
    }

    public async Task<ProsecutionCaseDto> UpdateAsync(Guid id, UpdateProsecutionRequest request, Guid userId, CancellationToken ct = default)
    {
        var prosecutionCase = await _context.ProsecutionCases.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Prosecution case {id} not found");

        if (prosecutionCase.DeletedAt != null)
            throw new InvalidOperationException("Cannot update a deleted prosecution case");

        if (request.ActId.HasValue)
            prosecutionCase.ActId = request.ActId.Value;

        if (!string.IsNullOrWhiteSpace(request.CaseNotes))
            prosecutionCase.CaseNotes = request.CaseNotes;

        if (!string.IsNullOrWhiteSpace(request.Status))
            prosecutionCase.Status = request.Status;

        prosecutionCase.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var prosecutionCase = await _context.ProsecutionCases.FindAsync(new object[] { id }, ct);
        if (prosecutionCase == null)
            return false;

        prosecutionCase.DeletedAt = DateTime.UtcNow;
        prosecutionCase.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Dictionary<string, object>> GetStatisticsAsync(CancellationToken ct = default)
    {
        var stats = new Dictionary<string, object>();

        var total = await _context.ProsecutionCases
            .CountAsync(p => p.DeletedAt == null, ct);
        stats["total"] = total;

        var pending = await _context.ProsecutionCases
            .CountAsync(p => p.Status == "pending" && p.DeletedAt == null, ct);
        stats["pending"] = pending;

        var invoiced = await _context.ProsecutionCases
            .CountAsync(p => p.Status == "invoiced" && p.DeletedAt == null, ct);
        stats["invoiced"] = invoiced;

        var paid = await _context.ProsecutionCases
            .CountAsync(p => p.Status == "paid" && p.DeletedAt == null, ct);
        stats["paid"] = paid;

        var court = await _context.ProsecutionCases
            .CountAsync(p => p.Status == "court" && p.DeletedAt == null, ct);
        stats["court"] = court;

        var totalFeesUsd = await _context.ProsecutionCases
            .Where(p => p.DeletedAt == null)
            .SumAsync(p => p.TotalFeeUsd, ct);
        stats["totalFeesUsd"] = totalFeesUsd;

        return stats;
    }

    public async Task<string> GenerateCertificateNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _context.ProsecutionCases
            .CountAsync(p => p.CreatedAt.Year == year, ct);

        return $"PROS-{year}-{(count + 1):D6}";
    }

    private async Task<decimal> CalculateGvwFeeAsync(int overloadKg, string legalFramework, CancellationToken ct)
    {
        if (overloadKg <= 0)
            return 0m;

        // Get fee from GVW fee schedule
        var feeSchedule = await _context.AxleFeeSchedules
            .Where(f => f.LegalFramework == legalFramework && f.FeeType == "GVW")
            .Where(f => overloadKg >= f.OverloadMinKg && (f.OverloadMaxKg == null || overloadKg <= f.OverloadMaxKg))
            .FirstOrDefaultAsync(ct);

        if (feeSchedule != null)
            return feeSchedule.FeePerKgUsd * overloadKg + feeSchedule.FlatFeeUsd;

        // Default fallback rate
        return overloadKg * 0.50m; // $0.50 per kg
    }

    private async Task<(bool IsRepeatOffender, int PriorOffenseCount, decimal Multiplier)> CheckRepeatOffenderAsync(Guid vehicleId, CancellationToken ct)
    {
        var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-12);

        var priorCount = await _context.ProsecutionCases
            .Join(_context.CaseRegisters,
                p => p.CaseRegisterId,
                c => c.Id,
                (p, c) => new { Prosecution = p, Case = c })
            .CountAsync(x => x.Case.VehicleId == vehicleId
                && x.Prosecution.CreatedAt >= twelveMonthsAgo
                && x.Prosecution.DeletedAt == null, ct);

        if (priorCount == 0)
            return (false, 0, 1.0m);

        // 5x multiplier for repeat offenders
        return (true, priorCount, 5.0m);
    }

    private async Task<string> GetLegalFrameworkCodeAsync(Guid actId, CancellationToken ct)
    {
        var act = await _context.ActDefinitions.FindAsync(new object[] { actId }, ct);
        return act?.Code ?? "TRAFFIC_ACT";
    }

    private ProsecutionCaseDto MapToDto(ProsecutionCase p)
    {
        return new ProsecutionCaseDto
        {
            Id = p.Id,
            CaseRegisterId = p.CaseRegisterId,
            CaseNo = p.CaseRegister?.CaseNo ?? string.Empty,
            WeighingId = p.WeighingId,
            WeighingTicketNo = p.Weighing?.TicketNumber,
            ProsecutionOfficerId = p.ProsecutionOfficerId,
            ProsecutionOfficerName = p.ProsecutionOfficer?.FullName,
            ActId = p.ActId,
            ActName = p.Act?.Name,
            GvwOverloadKg = p.GvwOverloadKg,
            GvwFeeUsd = p.GvwFeeUsd,
            GvwFeeKes = p.GvwFeeKes,
            MaxAxleOverloadKg = p.MaxAxleOverloadKg,
            MaxAxleFeeUsd = p.MaxAxleFeeUsd,
            MaxAxleFeeKes = p.MaxAxleFeeKes,
            BestChargeBasis = p.BestChargeBasis,
            PenaltyMultiplier = p.PenaltyMultiplier,
            OffenseCount = p.OffenseCount,
            DemeritPoints = p.DemeritPoints,
            TotalFeeUsd = p.TotalFeeUsd,
            TotalFeeKes = p.TotalFeeKes,
            ForexRate = p.ForexRate,
            CertificateNo = p.CertificateNo,
            CaseNotes = p.CaseNotes,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
