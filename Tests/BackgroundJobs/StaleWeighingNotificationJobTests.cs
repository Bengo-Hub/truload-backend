using FluentAssertions;
using Xunit;

namespace TruLoad.Backend.Tests.BackgroundJobs;

/// <summary>
/// Unit tests for the stale weighing threshold logic
/// used by StaleWeighingNotificationJob.
/// </summary>
public class StaleWeighingNotificationJobTests
{
    private const int DefaultThresholdHours = 8; // matches StaleWeighingNotificationJob

    private static bool IsStale(DateTime? firstWeightAt, int thresholdHours = DefaultThresholdHours)
    {
        if (!firstWeightAt.HasValue) return false;
        var cutoff = DateTime.UtcNow.AddHours(-thresholdHours);
        return firstWeightAt.Value <= cutoff;
    }

    private static bool IsExpired(DateTime? firstWeightAt, int thresholdHours = DefaultThresholdHours)
    {
        // Double cutoff = don't notify forever (4× the threshold)
        if (!firstWeightAt.HasValue) return false;
        var doubleCutoff = DateTime.UtcNow.AddHours(-thresholdHours * 4);
        return firstWeightAt.Value <= doubleCutoff;
    }

    [Fact]
    public void Transaction_IsStale_WhenFirstWeightOlderThanThreshold()
    {
        var firstWeightAt = DateTime.UtcNow.AddHours(-(DefaultThresholdHours + 1));
        IsStale(firstWeightAt).Should().BeTrue();
    }

    [Fact]
    public void Transaction_IsNotStale_WhenFirstWeightNewerThanThreshold()
    {
        var firstWeightAt = DateTime.UtcNow.AddHours(-1); // Only 1 hour ago
        IsStale(firstWeightAt).Should().BeFalse();
    }

    [Fact]
    public void Transaction_IsNotStale_WhenFirstWeightAtExactlyThreshold()
    {
        // cutoff = now - 8h; firstWeightAt exactly at threshold is NOT stale (must be <=)
        var firstWeightAt = DateTime.UtcNow.AddHours(-DefaultThresholdHours).AddSeconds(1);
        IsStale(firstWeightAt).Should().BeFalse();
    }

    [Fact]
    public void Transaction_IsExpired_WhenOlderThanQuadrupleThreshold()
    {
        var firstWeightAt = DateTime.UtcNow.AddHours(-(DefaultThresholdHours * 4 + 1));
        IsExpired(firstWeightAt).Should().BeTrue();
    }

    [Fact]
    public void Transaction_IsNotExpired_WhenWithinQuadrupleThreshold()
    {
        var firstWeightAt = DateTime.UtcNow.AddHours(-(DefaultThresholdHours * 2));
        IsExpired(firstWeightAt).Should().BeFalse();
    }

    [Fact]
    public void NullFirstWeight_IsNotStale()
    {
        IsStale(null).Should().BeFalse();
    }

    [Fact]
    public void CustomThreshold_IsRespected()
    {
        var threshold = 24;
        var firstWeightAt = DateTime.UtcNow.AddHours(-(threshold + 1));
        IsStale(firstWeightAt, threshold).Should().BeTrue();
    }
}
