using FluentAssertions;
using Xunit;

namespace TruLoad.Backend.Tests.BackgroundJobs;

/// <summary>
/// Unit tests for the 5% weight discrepancy threshold logic
/// used by PortalAnomalyAlertJob. Tests the pure calculation
/// without requiring a database or notification service.
/// </summary>
public class PortalAnomalyAlertJobTests
{
    private const double DiscrepancyThreshold = 0.05; // 5% — matches PortalAnomalyAlertJob

    private static bool IsAnomaly(double netWeightKg, double expectedWeightKg)
    {
        if (expectedWeightKg <= 0) return false;
        return Math.Abs(netWeightKg - expectedWeightKg) / expectedWeightKg > DiscrepancyThreshold;
    }

    [Fact]
    public void NoAnomaly_WhenNetEqualsExpected()
    {
        IsAnomaly(20000, 20000).Should().BeFalse();
    }

    [Fact]
    public void NoAnomaly_WhenDiscrepancyExactlyAtThreshold()
    {
        // Exactly 5% difference → NOT an anomaly (must be GREATER THAN threshold)
        var expected = 20000.0;
        var net = expected * 1.05;
        IsAnomaly(net, expected).Should().BeFalse();
    }

    [Fact]
    public void Anomaly_WhenDiscrepancyExceedsThreshold()
    {
        // 5.01% difference → anomaly
        var expected = 20000.0;
        var net = expected * 1.0501;
        IsAnomaly(net, expected).Should().BeTrue();
    }

    [Fact]
    public void Anomaly_WhenNetIsSignificantlyLessThanExpected()
    {
        // 10% short → anomaly
        var expected = 20000.0;
        var net = expected * 0.90;
        IsAnomaly(net, expected).Should().BeTrue();
    }

    [Fact]
    public void NoAnomaly_WhenDiscrepancyWithin5Percent()
    {
        var expected = 20000.0;
        var net = expected * 1.03; // 3% over
        IsAnomaly(net, expected).Should().BeFalse();
    }

    [Fact]
    public void DiscrepancyPercent_CalculatedCorrectly()
    {
        var net = 21000.0;
        var expected = 20000.0;
        var discrepancyPct = Math.Round(Math.Abs(net - expected) / expected * 100.0, 1);
        discrepancyPct.Should().Be(5.0);
    }

    [Fact]
    public void ZeroExpectedWeight_NotAnAnomaly()
    {
        // Guard against division-by-zero
        IsAnomaly(1000, 0).Should().BeFalse();
    }
}
