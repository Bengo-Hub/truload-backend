using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Portal;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Common;

/// <summary>
/// Centralized date-range and control-status helpers for weighing stats/dashboard/report queries -
/// single source of truth so "select 12 Aug" means the same real-world window everywhere a date
/// range is filtered, and a status filter always matches what the compliance engine actually
/// persists. Extracted after a 2026-08-12 audit found the dashboard/stats endpoints in
/// <c>WeighingController</c> had drifted into several inconsistent, independently-wrong
/// implementations of both (see the reporting plan's "NEW WORKSTREAM" section for the full list).
/// </summary>
public static class WeighingQueryHelpers
{
    /// <summary>
    /// Kenya (EAT) is a single fixed UTC+3 offset with no daylight-saving changes, so a static
    /// shift is correct here and needs no <see cref="TimeZoneInfo"/> database lookup.
    /// </summary>
    private static readonly TimeSpan EatOffset = TimeSpan.FromHours(3);

    /// <summary>
    /// Converts a true-UTC <c>WeighedAt</c> instant to its EAT-local bucket start for a tonnage
    /// trend/report, per <paramref name="granularity"/>. Week buckets start Monday (ISO-8601, this
    /// codebase's existing convention for weekly reporting elsewhere). Callers must materialize rows
    /// before calling this (it's plain C#, not translatable to SQL) - the existing daily-only trend
    /// endpoints instead group via a SQL-translatable <c>.AddHours(3).Date</c> LINQ GroupBy, which
    /// can't express week/month buckets safely, hence this separate in-memory helper.
    /// </summary>
    public static DateTime BucketEatStart(DateTime weighedAtUtc, TonnageTrendGranularity granularity)
    {
        var eatLocal = weighedAtUtc.Add(EatOffset);
        return granularity switch
        {
            TonnageTrendGranularity.Hour => new DateTime(eatLocal.Year, eatLocal.Month, eatLocal.Day, eatLocal.Hour, 0, 0, DateTimeKind.Unspecified),
            TonnageTrendGranularity.Day => eatLocal.Date,
            TonnageTrendGranularity.Week => eatLocal.Date.AddDays(-(((int)eatLocal.DayOfWeek + 6) % 7)), // Monday of that ISO week
            TonnageTrendGranularity.Month => new DateTime(eatLocal.Year, eatLocal.Month, 1, 0, 0, 0, DateTimeKind.Unspecified),
            _ => eatLocal.Date,
        };
    }

    /// <summary>Display label for a <see cref="BucketEatStart"/> result, per granularity.</summary>
    public static string FormatBucketLabel(DateTime bucketStart, TonnageTrendGranularity granularity) => granularity switch
    {
        TonnageTrendGranularity.Hour => bucketStart.ToString("MMM dd HH:00"),
        TonnageTrendGranularity.Day => bucketStart.ToString("MMM dd"),
        TonnageTrendGranularity.Week => $"Wk of {bucketStart:MMM dd}",
        TonnageTrendGranularity.Month => bucketStart.ToString("MMM yyyy"),
        _ => bucketStart.ToString("MMM dd"),
    };

    /// <summary>
    /// Resolves a `[fromUtc, toUtcExclusive)` instant range from date-only (or full) inputs,
    /// treating them as Nairobi-local (EAT) calendar days - "12 Aug" means the real EAT day, not a
    /// UTC day wearing an EAT label. Previously every call site did
    /// <c>DateTime.SpecifyKind(dateFrom, DateTimeKind.Utc)</c>, which only relabels the value
    /// without converting it, silently shifting every "selected day" by 3 hours against
    /// <c>WeighedAt</c> (a true UTC instant). Defaults to the trailing 30 EAT days when both
    /// inputs are absent, matching this codebase's existing default-range convention.
    /// </summary>
    public static (DateTime FromUtc, DateTime ToUtcExclusive) ResolveEatDayRange(DateTime? dateFrom, DateTime? dateTo)
    {
        var nowEatDate = DateTime.UtcNow.Add(EatOffset).Date;
        var toLocalDate = dateTo?.Date ?? nowEatDate;
        var fromLocalDate = dateFrom?.Date ?? toLocalDate.AddDays(-30);

        var fromUtc = DateTime.SpecifyKind(fromLocalDate, DateTimeKind.Utc) - EatOffset;
        var toUtcExclusive = DateTime.SpecifyKind(toLocalDate.AddDays(1), DateTimeKind.Utc) - EatOffset;

        return (fromUtc, toUtcExclusive);
    }

    /// <summary>
    /// Filters by time-of-day (EAT local clock) regardless of which day it falls on, e.g. "only
    /// tickets weighed between 06:00 and 18:00" across the whole selected date range. Compares the
    /// EAT-local hour/minute/second of <c>WeighedAt</c> (a true UTC instant) against the requested
    /// bounds - <c>.Hour</c>/<c>.Minute</c>/<c>.Second</c> are used (rather than <c>.TimeOfDay</c>)
    /// because they are the properties reliably translated to SQL by the Npgsql EF Core provider.
    /// A <paramref name="fromTime"/> greater than <paramref name="toTime"/> is treated as an
    /// overnight window that wraps past midnight (e.g. 22:00-06:00). Either bound may be omitted,
    /// in which case it is treated as the start/end of the day respectively.
    /// </summary>
    public static IQueryable<WeighingTransaction> ApplyTimeOfDayFilter(
        IQueryable<WeighingTransaction> query, TimeSpan? fromTime, TimeSpan? toTime)
    {
        if (!fromTime.HasValue && !toTime.HasValue)
            return query;

        var fromSeconds = (int)(fromTime ?? TimeSpan.Zero).TotalSeconds;
        var toSeconds = (int)(toTime ?? new TimeSpan(23, 59, 59)).TotalSeconds;

        // Expression is inlined (rather than calling a helper method) because EF Core can only
        // translate the LINQ expression tree it sees directly in the lambda - a call to a plain
        // C# static method here would not be recognized and would throw at query execution time.
        if (fromTime.HasValue && toTime.HasValue && fromTime.Value > toTime.Value)
        {
            // Overnight window wraps past midnight, e.g. 22:00-06:00.
            return query.Where(t =>
                ((t.WeighedAt.AddHours(3).Hour * 3600) + (t.WeighedAt.AddHours(3).Minute * 60) + t.WeighedAt.AddHours(3).Second) >= fromSeconds
                || ((t.WeighedAt.AddHours(3).Hour * 3600) + (t.WeighedAt.AddHours(3).Minute * 60) + t.WeighedAt.AddHours(3).Second) <= toSeconds);
        }

        return query.Where(t =>
            ((t.WeighedAt.AddHours(3).Hour * 3600) + (t.WeighedAt.AddHours(3).Minute * 60) + t.WeighedAt.AddHours(3).Second) >= fromSeconds
            && ((t.WeighedAt.AddHours(3).Hour * 3600) + (t.WeighedAt.AddHours(3).Minute * 60) + t.WeighedAt.AddHours(3).Second) <= toSeconds);
    }

    /// <summary>
    /// Known aliases for each real, persisted <c>ControlStatus</c> value. The canonical value
    /// (what <c>AxleGroupAggregationService</c>/<c>WeighingService</c> actually write) is always
    /// first; the rest are values a UI dropdown or older client has been confirmed to send instead.
    /// </summary>
    private static readonly Dictionary<string, string[]> StatusAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Compliant"] = ["Compliant", "LEGAL", "Legal"],
        ["Warning"] = ["Warning", "WARNING"],
        ["Overloaded"] = ["Overloaded", "OVERLOAD", "Overload", "OVERLOADED"],
        ["Pending"] = ["Pending", "PENDING"],
        ["TagHold"] = ["TagHold", "TAGHOLD"],
        ["Released"] = ["Released", "RELEASED"],
    };

    /// <summary>
    /// Filters by control status, matching any known alias of the requested value rather than an
    /// exact string match - fixes the confirmed bug class where a UI sends <c>LEGAL</c>/<c>OVERLOAD</c>
    /// but the DB only ever stores <c>Compliant</c>/<c>Overloaded</c>, silently returning zero rows.
    /// Unrecognised values fall through to a literal match (still correctly returns nothing for a
    /// genuinely bogus value like the frontend's stale "Charged" option, rather than guessing).
    /// </summary>
    public static IQueryable<WeighingTransaction> ApplyControlStatusFilter(
        IQueryable<WeighingTransaction> query, string? controlStatus)
    {
        if (string.IsNullOrWhiteSpace(controlStatus))
            return query;

        var aliases = StatusAliases.TryGetValue(controlStatus, out var known)
            ? known
            : StatusAliases.Values.FirstOrDefault(v => v.Contains(controlStatus, StringComparer.OrdinalIgnoreCase))
              ?? [controlStatus];

        return query.Where(w => aliases.Contains(w.ControlStatus));
    }

    /// <summary>
    /// Aggregates a transporter's tonnage-based commercial billing for a statement period — total
    /// net weight weighed, weighing count, and the tariff amount ALREADY computed and persisted at
    /// ticket-close time by <c>CommercialWeighingService</c> (<c>Invoice.AmountDue</c> for
    /// Immediate-billed weighings, plus <c>CommercialTariffAccrual.ComputedAmountKes</c> for
    /// Daily/Weekly/Monthly-billed ones not yet rolled into an invoice). Deliberately sums the
    /// already-persisted amounts rather than re-deriving a rate from today's
    /// <c>CommercialTariffRule</c> config - re-deriving could silently disagree with what was
    /// actually invoiced if a rate changed since the weighing happened. Shared by
    /// <c>TransporterController</c>'s commercial-ops statement and
    /// <c>TransporterPortalService</c>'s self-service portal statement, both of which present this
    /// alongside (not instead of) the existing treasury AR-ledger view.
    /// </summary>
    public static async Task<PortalTonnageSummaryDto> ComputeTransporterTonnageSummaryAsync(
        TruLoadDbContext context, Guid organizationId, Guid transporterId,
        DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default)
    {
        var weighings = await context.WeighingTransactions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(wt => wt.OrganizationId == organizationId && wt.TransporterId == transporterId
                && wt.WeighedAt >= fromUtc && wt.WeighedAt < toUtcExclusive && wt.DeletedAt == null)
            .Select(wt => new { wt.Id, wt.NetWeightKg })
            .ToListAsync(ct);

        var weighingIds = weighings.Select(w => w.Id).ToList();

        var invoiceRevenue = weighingIds.Count == 0 ? 0m : await context.Invoices
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(inv => inv.OrganizationId == organizationId && inv.InvoiceType == "commercial_weighing_fee"
                && inv.WeighingId != null && weighingIds.Contains(inv.WeighingId.Value))
            .SumAsync(inv => inv.AmountDue, ct);

        var accrualRevenue = weighingIds.Count == 0 ? 0m : await context.CommercialTariffAccruals
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(acc => acc.OrganizationId == organizationId && weighingIds.Contains(acc.WeighingId))
            .SumAsync(acc => acc.ComputedAmountKes, ct);

        // Surface the applicable rate/basis ONLY when this transporter has an active
        // transporter-specific contract override - that rule applies unconditionally to every one
        // of their weighings (same top-priority match CommercialWeighingService.
        // ResolveCommercialTariffAsync uses), unlike an org-wide bracket rule that can vary per
        // vehicle/axle/weight, so it's the one case a single displayed rate is actually honest.
        var now = DateTime.UtcNow;
        var contractRule = await context.CommercialTariffRules
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.OrganizationId == organizationId && r.TransporterId == transporterId && r.IsActive
                && r.EffectiveFrom <= now && (r.EffectiveTo == null || r.EffectiveTo >= now))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        return new PortalTonnageSummaryDto
        {
            PeriodFrom = fromUtc,
            PeriodTo = toUtcExclusive.AddTicks(-1),
            TotalNetWeightKg = weighings.Sum(w => (long)(w.NetWeightKg ?? 0)),
            WeighingCount = weighings.Count,
            TariffAmountKes = invoiceRevenue + accrualRevenue,
            RateBasis = contractRule?.RateBasis,
            RateKes = contractRule?.FeeKes
        };
    }
}

/// <summary>
/// Time-bucket grain for a tonnage trend/report - the "aggregated by hour, day, weeks, and months"
/// need raised by commercial (quarry/waste-treatment) tenants who bill their own downstream client
/// off periodic tonnage rollups.
/// </summary>
public enum TonnageTrendGranularity
{
    Hour,
    Day,
    Week,
    Month,
}
