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
}
