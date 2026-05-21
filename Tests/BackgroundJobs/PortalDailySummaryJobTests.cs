using FluentAssertions;
using Xunit;

namespace TruLoad.Backend.Tests.BackgroundJobs;

/// <summary>
/// Unit tests for the portal daily summary job date-range logic.
/// The job queries transactions from "yesterday" (UTC). These tests
/// verify the date-range computation is correct.
/// </summary>
public class PortalDailySummaryJobTests
{
    private static (DateTime From, DateTime To) GetYesterdayRange(DateTime utcNow)
    {
        var yesterday = utcNow.Date.AddDays(-1);
        return (yesterday, yesterday.AddDays(1).AddTicks(-1));
    }

    [Fact]
    public void YesterdayRange_StartsAtMidnight()
    {
        var now = new DateTime(2026, 5, 21, 4, 0, 0, DateTimeKind.Utc);
        var (from, _) = GetYesterdayRange(now);
        from.Should().Be(new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void YesterdayRange_EndsAtEndOfDay()
    {
        var now = new DateTime(2026, 5, 21, 4, 0, 0, DateTimeKind.Utc);
        var (_, to) = GetYesterdayRange(now);
        to.Date.Should().Be(new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc));
        to.Should().BeAfter(to.Date); // Should be 23:59:59.9999999
    }

    [Fact]
    public void YesterdayRange_SpansExactly24Hours()
    {
        var now = new DateTime(2026, 5, 21, 4, 0, 0, DateTimeKind.Utc);
        var (from, to) = GetYesterdayRange(now);
        (to - from).TotalHours.Should().BeApproximately(24.0, precision: 0.001);
    }

    [Fact]
    public void YesterdayRange_CrossesMonthBoundaryCorrectly()
    {
        var now = new DateTime(2026, 5, 1, 4, 0, 0, DateTimeKind.Utc);
        var (from, _) = GetYesterdayRange(now);
        from.Should().Be(new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void YesterdayRange_CrossesYearBoundaryCorrectly()
    {
        var now = new DateTime(2026, 1, 1, 4, 0, 0, DateTimeKind.Utc);
        var (from, _) = GetYesterdayRange(now);
        from.Should().Be(new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc));
    }
}
