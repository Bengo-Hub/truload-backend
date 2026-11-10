using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Common;
using TruLoad.Backend.Data;
using Xunit;

namespace TruLoad.Backend.Tests.Unit.Common;

/// <summary>
/// The tonnage trend now sums in SQL per EAT hour or day and folds those partial sums into its
/// buckets. These tests prove the grouping translates to PostgreSQL SQL (the in-memory provider
/// used elsewhere cannot) and that folding partials gives exactly the buckets the old row-by-row
/// code produced, for every granularity.
/// </summary>
public class TonnageTrendSqlTests
{
    private static TruLoadDbContext NpgsqlContext()
    {
        // No connection is opened: ToQueryString only needs the provider to translate the query.
        var options = new DbContextOptionsBuilder<TruLoadDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=x;Password=x", o => o.UseVector())
            .Options;
        return new TruLoadDbContext(options);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TonnagePartials_TranslatesToSqlGroupBy(bool byHour)
    {
        using var ctx = NpgsqlContext();

        var sql = WeighingQueryHelpers
            .TonnagePartials(ctx.WeighingTransactions.AsNoTracking(), byHour)
            .ToQueryString();

        sql.Should().Contain("GROUP BY");
        sql.Should().Contain("sum(", "the net weight must be summed by the database");
    }

    [Theory]
    [InlineData(TonnageTrendGranularity.Hour)]
    [InlineData(TonnageTrendGranularity.Day)]
    [InlineData(TonnageTrendGranularity.Week)]
    [InlineData(TonnageTrendGranularity.Month)]
    public void FoldingPartials_MatchesRowByRowBuckets(TonnageTrendGranularity granularity)
    {
        var rng = new Random(20261110);
        var start = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);
        var rows = Enumerable.Range(0, 2000)
            .Select(_ => (WeighedAt: start.AddMinutes(rng.Next(0, 60 * 24 * 40)), Kg: (int?)rng.Next(0, 40000)))
            .Append((WeighedAt: new DateTime(2026, 10, 31, 21, 30, 0, DateTimeKind.Utc), Kg: (int?)null)) // 00:30 EAT, month edge
            .ToList();

        // The old code: bucket every row.
        var expected = rows
            .GroupBy(r => WeighingQueryHelpers.BucketEatStart(r.WeighedAt, granularity))
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, Kg: g.Sum(r => (long)(r.Kg ?? 0)), Count: g.Count()))
            .ToList();

        // The new code: partial sums per EAT hour or day (what SQL returns), then fold.
        var byHour = granularity == TonnageTrendGranularity.Hour;
        var partials = rows
            .GroupBy(r =>
            {
                var eat = r.WeighedAt.AddHours(3);
                return (Day: eat.Date, Hour: byHour ? eat.Hour : 0);
            })
            .Select(g => new TonnagePartial(g.Key.Day, g.Key.Hour, g.Sum(r => (long)(r.Kg ?? 0)), g.Count()));
        var actual = partials
            .GroupBy(p => WeighingQueryHelpers.BucketFromEatLocal(p.EatLocal, granularity))
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, Kg: g.Sum(p => p.NetWeightKg), Count: g.Sum(p => p.Count)))
            .ToList();

        actual.Should().Equal(expected);
    }
}
