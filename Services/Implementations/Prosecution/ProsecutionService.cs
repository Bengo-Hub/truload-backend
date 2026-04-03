using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Prosecution;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Models.Prosecution;
using TruLoad.Backend.Services.Interfaces.Prosecution;
using TruLoad.Backend.Services.Interfaces.Weighing;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Interfaces.Financial;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.Prosecution;

/// <summary>
/// Service implementation for prosecution case management.
/// Handles charge calculation, case creation, and status tracking.
/// </summary>
public class ProsecutionService : IProsecutionService
{
    private readonly TruLoadDbContext _context;
    private readonly IAxleGroupAggregationService _axleGroupService;
    private readonly ISettingsService _settingsService;
    private readonly ICurrencyService _currencyService;

    public ProsecutionService(
        TruLoadDbContext context,
        IAxleGroupAggregationService axleGroupService,
        ISettingsService settingsService,
        ICurrencyService currencyService)
    {
        _context = context;
        _axleGroupService = axleGroupService;
        _settingsService = settingsService;
        _currencyService = currencyService;
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

    public async Task<PagedResponse<ProsecutionCaseDto>> SearchAsync(ProsecutionSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = _context.ProsecutionCases
            .Include(p => p.CaseRegister)
            .Include(p => p.Weighing)
            .Include(p => p.ProsecutionOfficer)
            .Include(p => p.Act)
            .Where(p => p.DeletedAt == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.CaseNo))
            query = query.Where(p => p.CaseRegister != null && p.CaseRegister.CaseNo.Contains(criteria.CaseNo));

        if (criteria.CaseRegisterId.HasValue)
            query = query.Where(p => p.CaseRegisterId == criteria.CaseRegisterId.Value);

        if (criteria.WeighingId.HasValue)
            query = query.Where(p => p.WeighingId == criteria.WeighingId.Value);

        if (criteria.StationId.HasValue)
            query = query.Where(p => p.Weighing != null && p.Weighing.StationId == criteria.StationId.Value);

        if (criteria.ActId.HasValue)
            query = query.Where(p => p.ActId == criteria.ActId.Value);

        if (!string.IsNullOrWhiteSpace(criteria.Status))
            query = query.Where(p => p.Status == criteria.Status);

        if (criteria.EffectiveFromDate.HasValue)
            query = query.Where(p => p.CreatedAt >= criteria.EffectiveFromDate.Value);

        if (criteria.EffectiveToDate.HasValue)
            query = query.Where(p => p.CreatedAt <= criteria.EffectiveToDate.Value);

        if (criteria.MinTotalFee.HasValue)
            query = query.Where(p => p.TotalFeeUsd >= criteria.MinTotalFee.Value);

        if (criteria.MaxTotalFee.HasValue)
            query = query.Where(p => p.TotalFeeUsd <= criteria.MaxTotalFee.Value);

        var totalCount = await query.CountAsync(ct);

        var prosecutionCases = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return PagedResponse<ProsecutionCaseDto>.Create(
            prosecutionCases.Select(MapToDto).ToList(),
            totalCount,
            criteria.PageNumber,
            criteria.PageSize);
    }

    public async Task<ChargeCalculationResult> CalculateChargesAsync(Guid weighingId, string legalFramework, CancellationToken ct = default)
    {
        var weighing = await _context.WeighingTransactions
            .Include(w => w.WeighingAxles)
            .FirstOrDefaultAsync(w => w.Id == weighingId, ct)
            ?? throw new InvalidOperationException($"Weighing transaction {weighingId} not found");

        // Calculate compliance with group-based fees
        var compliance = await _axleGroupService.CalculateComplianceAsync(weighingId);

        // Resolve the charging currency from the act definition for this legal framework
        var chargingCurrency = await GetChargingCurrencyAsync(legalFramework, ct);

        // Get live forex rate from currency service (falls back to latest DB rate)
        var currentRateResponse = await _currencyService.GetCurrentRateAsync("USD", "KES", ct);
        var forexRate = currentRateResponse.Rate;

        // Check for repeat offender (within 12 months) — need conviction number before fee lookup
        var (isRepeatOffender, priorOffenseCount, convictionNumber, multiplier) =
            await CheckRepeatOffenderAsync(weighing.VehicleId, legalFramework, ct);

        // Calculate GVW-based fee (returns native currency amounts)
        var gvwOverloadKg = weighing.OverloadKg;
        var (gvwFeeUsdRaw, gvwFeeKesRaw) = await CalculateGvwFeeAsync(gvwOverloadKg, legalFramework, convictionNumber, ct);

        // Resolve cross-currency values for reporting
        decimal gvwFeeUsd, gvwFeeKes;
        if (legalFramework == "TRAFFIC_ACT")
        {
            // Traffic Act: native fee is KES; derive USD for reporting
            gvwFeeKes = gvwFeeKesRaw;
            gvwFeeUsd = forexRate > 0 ? gvwFeeKesRaw / forexRate : 0m;
        }
        else
        {
            // EAC: native fee is USD; derive KES for reporting
            gvwFeeUsd = gvwFeeUsdRaw;
            gvwFeeKes = gvwFeeUsdRaw * forexRate;
        }

        // Get axle-based fees from compliance results
        var maxAxleOverloadKg = compliance.GroupResults.Any() ? compliance.GroupResults.Max(g => g.OverloadKg) : 0;
        var totalAxleFeeUsd = compliance.GroupResults.Sum(g => g.FeeUsd);
        var totalAxleFeeKes = totalAxleFeeUsd * forexRate;

        // Determine best charge basis (higher of GVW or Axle fees)
        var bestChargeBasis = gvwFeeUsd >= totalAxleFeeUsd ? "gvw" : "axle";
        var baseFeeUsd = bestChargeBasis == "gvw" ? gvwFeeUsd : totalAxleFeeUsd;

        // Apply multiplier (always 1.0 — repeat-offender penalties are encoded in conviction-specific fee bands)
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
            ChargingCurrency = chargingCurrency,
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
        var defaultForexRateResponse = await _currencyService.GetCurrentRateAsync("USD", "KES", ct);
        var defaultForexRate = defaultForexRateResponse.Rate;

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
            ForexRate = chargeCalculation?.ForexRate ?? defaultForexRate,
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

    public async Task<ProsecutionStatisticsDto> GetStatisticsAsync(DateTime? dateFrom = null, DateTime? dateTo = null, Guid? stationId = null, CancellationToken ct = default)
    {
        var cases = _context.ProsecutionCases.Where(p => p.DeletedAt == null);

        if (stationId.HasValue)
            cases = cases.Where(p => p.StationId == stationId.Value);
        if (dateFrom.HasValue)
        {
            var from = DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc);
            cases = cases.Where(p => p.CreatedAt >= from);
        }
        if (dateTo.HasValue)
        {
            var to = DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc);
            cases = cases.Where(p => p.CreatedAt <= to);
        }

        var total = await cases.CountAsync(ct);
        var pending = await cases.CountAsync(p => p.Status == "pending", ct);
        var invoiced = await cases.CountAsync(p => p.Status == "invoiced", ct);
        var paid = await cases.CountAsync(p => p.Status == "paid", ct);

        // Count prosecutions whose linked CaseRegister has court hearings, OR status is "court"
        var caseRegisterIdsWithHearings = _context.CourtHearings
            .Where(ch => ch.DeletedAt == null)
            .Select(ch => ch.CaseRegisterId)
            .Distinct();
        var court = await cases.CountAsync(p =>
            p.Status == "court" || caseRegisterIdsWithHearings.Contains(p.CaseRegisterId), ct);

        // Fee totals (all prosecutions) and collected (paid only)
        var totalFeesKes = await cases.SumAsync(p => p.TotalFeeKes, ct);
        var totalFeesUsd = await cases.SumAsync(p => p.TotalFeeUsd, ct);
        var paidCases = cases.Where(p => p.Status == "paid");
        var collectedFeesKes = await paidCases.SumAsync(p => p.TotalFeeKes, ct);
        var collectedFeesUsd = await paidCases.SumAsync(p => p.TotalFeeUsd, ct);

        return new ProsecutionStatisticsDto
        {
            TotalCases = total,
            PendingCases = pending,
            InvoicedCases = invoiced,
            PaidCases = paid,
            CourtCases = court,
            TotalFeesKes = totalFeesKes,
            TotalFeesUsd = totalFeesUsd,
            CollectedFeesKes = collectedFeesKes,
            CollectedFeesUsd = collectedFeesUsd
        };
    }

    public async Task<string> GenerateCertificateNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _context.ProsecutionCases
            .CountAsync(p => p.CreatedAt.Year == year, ct);

        return $"PROS-{year}-{(count + 1):D6}";
    }

    private async Task<(decimal FeeUsd, decimal FeeKes)> CalculateGvwFeeAsync(
        int overloadKg, string legalFramework, int convictionNumber, CancellationToken ct)
    {
        if (overloadKg <= 0)
            return (0m, 0m);

        var feeSchedule = await _context.AxleFeeSchedules
            .Where(f => f.LegalFramework == legalFramework && f.FeeType == "GVW")
            .Where(f => f.ConvictionNumber == convictionNumber)
            .Where(f => overloadKg >= f.OverloadMinKg && (f.OverloadMaxKg == null || overloadKg <= f.OverloadMaxKg))
            .FirstOrDefaultAsync(ct);

        if (feeSchedule == null)
            return (overloadKg * 0.50m, 0m); // fallback

        if (legalFramework == "TRAFFIC_ACT")
            return (0m, feeSchedule.FlatFeeKes); // Traffic Act: native KES flat fee

        // EAC: per-kg USD calculation
        return (feeSchedule.FeePerKgUsd * overloadKg + feeSchedule.FlatFeeUsd, 0m);
    }

    private async Task<(bool IsRepeatOffender, int PriorOffenseCount, int ConvictionNumber, decimal Multiplier)>
        CheckRepeatOffenderAsync(Guid vehicleId, string legalFramework, CancellationToken ct)
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

        if (legalFramework == "TRAFFIC_ACT")
        {
            // Traffic Act: conviction number selects the fee band; no blanket multiplier
            var convictionNumber = priorCount >= 1 ? 2 : 1;
            return (priorCount > 0, priorCount, convictionNumber, 1.0m);
        }

        // EAC: conviction number selects the fee band with 5× rates; no blanket multiplier
        var eacConvictionNumber = priorCount >= 1 ? 2 : 1;
        return (priorCount > 0, priorCount, eacConvictionNumber, 1.0m);
    }

    private async Task<string> GetLegalFrameworkCodeAsync(Guid actId, CancellationToken ct)
    {
        var act = await _context.ActDefinitions.FindAsync(new object[] { actId }, ct);
        return act?.Code ?? "TRAFFIC_ACT";
    }

    /// <summary>
    /// Resolves the charging currency for a given legal framework.
    /// Traffic Act charges in KES, EAC Act charges in USD.
    /// </summary>
    private async Task<string> GetChargingCurrencyAsync(string legalFramework, CancellationToken ct)
    {
        var act = await _context.ActDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => (a.Code == legalFramework ||
                (legalFramework == "EAC" && a.ActType == "EAC") ||
                (legalFramework == "TRAFFIC_ACT" && a.ActType == "Traffic"))
                && a.IsActive && a.DeletedAt == null, ct);
        return act?.ChargingCurrency ?? "KES";
    }

    public async Task<List<ConvictionRecordDto>> GetConvictionHistoryAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var records = await _context.ProsecutionCases
            .Join(_context.CaseRegisters,
                p => p.CaseRegisterId,
                c => c.Id,
                (p, c) => new { Prosecution = p, Case = c })
            .Where(x => x.Case.VehicleId == vehicleId && x.Prosecution.DeletedAt == null)
            .OrderBy(x => x.Prosecution.CreatedAt)
            .Select(x => new
            {
                x.Prosecution.Id,
                x.Case.CaseNo,
                x.Case.VehicleId,
                x.Prosecution.GvwOverloadKg,
                x.Prosecution.TotalFeeKes,
                x.Prosecution.TotalFeeUsd,
                x.Prosecution.ActId,
                x.Prosecution.CreatedAt,
                x.Prosecution.Status,
            })
            .ToListAsync(ct);

        // Resolve vehicle reg number
        var vehicleRegNumber = string.Empty;
        if (records.Count > 0)
        {
            var vehicleInfo = await _context.CaseRegisters
                .Where(c => c.VehicleId == vehicleId && c.DeletedAt == null && c.WeighingId != null)
                .Join(_context.WeighingTransactions,
                    c => c.WeighingId,
                    w => w.Id,
                    (c, w) => new { w.VehicleRegNumber })
                .FirstOrDefaultAsync(ct);

            vehicleRegNumber = vehicleInfo?.VehicleRegNumber ?? string.Empty;
        }

        // Resolve act names for legal framework
        var actIds = records.Select(r => r.ActId).Distinct().ToList();
        var acts = await _context.ActDefinitions
            .Where(a => actIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, ct);

        return records.Select((r, index) => new ConvictionRecordDto
        {
            ProsecutionCaseId = r.Id,
            CaseNo = r.CaseNo,
            VehicleRegNumber = vehicleRegNumber,
            DriverName = null,
            OverloadKg = r.GvwOverloadKg,
            ChargeAmountKes = r.TotalFeeKes,
            ChargeAmountUsd = r.TotalFeeUsd,
            LegalFramework = acts.TryGetValue(r.ActId, out var actName) ? actName : "Unknown",
            ConvictionDate = r.CreatedAt,
            ConvictionNumber = index + 1,
            Status = r.Status,
        }).ToList();
    }

    public async Task<PagedResponse<HabitualOffenderDto>> GetHabitualOffendersAsync(
        int minConvictions,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.ProsecutionCases
            .Join(_context.CaseRegisters,
                p => p.CaseRegisterId,
                c => c.Id,
                (p, c) => new { Prosecution = p, Case = c })
            .Where(x => x.Prosecution.DeletedAt == null);

        if (fromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            query = query.Where(x => x.Prosecution.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var to = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
            query = query.Where(x => x.Prosecution.CreatedAt <= to);
        }

        var grouped = query
            .GroupBy(x => x.Case.VehicleId)
            .Select(g => new
            {
                VehicleId = g.Key,
                TotalConvictions = g.Count(),
                FirstConvictionDate = g.Min(x => x.Prosecution.CreatedAt),
                LastConvictionDate = g.Max(x => x.Prosecution.CreatedAt),
                TotalFinesKes = g.Sum(x => x.Prosecution.TotalFeeKes),
                TotalFinesUsd = g.Sum(x => x.Prosecution.TotalFeeUsd),
            })
            .Where(g => g.TotalConvictions >= minConvictions);

        var totalCount = await grouped.CountAsync(ct);

        var items = await grouped
            .OrderByDescending(g => g.TotalConvictions)
            .ThenByDescending(g => g.LastConvictionDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Resolve vehicle registration numbers
        var vehicleIds = items.Select(i => i.VehicleId).ToList();
        var vehicleRegs = await _context.CaseRegisters
            .Where(c => vehicleIds.Contains(c.VehicleId) && c.DeletedAt == null && c.WeighingId != null)
            .Join(_context.WeighingTransactions,
                c => c.WeighingId,
                w => w.Id,
                (c, w) => new { c.VehicleId, w.VehicleRegNumber })
            .GroupBy(x => x.VehicleId)
            .Select(g => new { VehicleId = g.Key, VehicleRegNumber = g.First().VehicleRegNumber })
            .ToDictionaryAsync(x => x.VehicleId, x => x.VehicleRegNumber, ct);

        var dtos = items.Select(i => new HabitualOffenderDto
        {
            VehicleId = i.VehicleId,
            VehicleRegNumber = vehicleRegs.TryGetValue(i.VehicleId, out var reg) ? reg : "Unknown",
            TotalConvictions = i.TotalConvictions,
            FirstConvictionDate = i.FirstConvictionDate,
            LastConvictionDate = i.LastConvictionDate,
            TotalFinesKes = i.TotalFinesKes,
            TotalFinesUsd = i.TotalFinesUsd,
        }).ToList();

        return PagedResponse<HabitualOffenderDto>.Create(dtos, totalCount, pageNumber, pageSize);
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
            ChargingCurrency = p.Act?.ChargingCurrency ?? "KES",
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
