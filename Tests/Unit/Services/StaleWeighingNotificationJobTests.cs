using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TruLoad.Backend.Common;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.Repositories.Weighing;
using TruLoad.Backend.Services.BackgroundJobs;
using TruLoad.Backend.Services.Interfaces.Shared;
using TruLoad.Backend.Services.Interfaces.System;
using Xunit;

namespace Truload.Backend.Tests.Unit.Services;

public class StaleWeighingNotificationJobTests
{
    private readonly string _dbName = $"AutoRecordAlert_{Guid.NewGuid()}";
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Guid _operatorId = Guid.NewGuid();

    private TruLoadDbContext NewContext() => new(
        new DbContextOptionsBuilder<TruLoadDbContext>().UseInMemoryDatabase(_dbName).Options);

    private WeighingTransaction Weighing(string captureStatus, string controlStatus, DateTime weighedAt) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        StationId = Guid.NewGuid(),
        TicketNumber = Guid.NewGuid().ToString(),
        VehicleRegNumber = "KDA 123X",
        VehicleId = Guid.NewGuid(),
        WeighedByUserId = _operatorId,
        CaptureStatus = captureStatus,
        ControlStatus = controlStatus,
        WeighedAt = weighedAt
    };

    private async Task RunJobAsync()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingValueAsync(SettingKeys.WeighingAutoRecordAlertMinutes, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(60);
        settings.Setup(s => s.GetSettingValueAsync(SettingKeys.CommercialPendingWeighingThresholdHours, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);

        var services = new ServiceCollection();
        services.AddScoped(_ => NewContext());
        services.AddScoped(_ => _notifications.Object);
        services.AddScoped(_ => settings.Object);
        using var provider = services.BuildServiceProvider();

        var job = new StaleWeighingNotificationJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<StaleWeighingNotificationJob>.Instance);
        await job.ExecuteAsync();
    }

    private void VerifyAlerts(Times times) => _notifications.Verify(
        n => n.SendInternalNotificationAsync(_operatorId, It.IsAny<string>(), It.IsAny<string>(), "warning", It.IsAny<string>(), It.IsAny<CancellationToken>()),
        times);

    [Fact]
    public async Task OverloadedAutoRecordPastTheWindow_AlertsOnce()
    {
        await using (var db = NewContext())
        {
            db.WeighingTransactions.Add(Weighing("auto", "Overloaded", DateTime.UtcNow.AddHours(-2)));
            await db.SaveChangesAsync();
        }

        await RunJobAsync();
        await RunJobAsync();

        VerifyAlerts(Times.Once());
    }

    [Theory]
    [InlineData("captured", "Overloaded", -2)]
    [InlineData("not_weighed", "Overloaded", -2)]
    [InlineData("auto", "Compliant", -2)]
    [InlineData("auto", "Overloaded", -1000)]
    public async Task OtherRecords_DoNotAlert(string captureStatus, string controlStatus, int hoursAgo)
    {
        await using (var db = NewContext())
        {
            db.WeighingTransactions.Add(Weighing(captureStatus, controlStatus, DateTime.UtcNow.AddHours(hoursAgo)));
            await db.SaveChangesAsync();
        }

        await RunJobAsync();

        VerifyAlerts(Times.Never());
    }

    [Fact]
    public async Task OverloadedAutoRecordInsideTheWindow_DoesNotAlertYet()
    {
        await using (var db = NewContext())
        {
            db.WeighingTransactions.Add(Weighing("auto", "Overloaded", DateTime.UtcNow.AddMinutes(-30)));
            await db.SaveChangesAsync();
        }

        await RunJobAsync();

        VerifyAlerts(Times.Never());
    }

    [Fact]
    public async Task AutoLookup_FindsARecordFromTheEarlierCalendarDay()
    {
        var stationId = Guid.NewGuid();
        var record = Weighing("auto", "Overloaded", DateTime.UtcNow.AddHours(-11));
        record.StationId = stationId;
        var tooOld = Weighing("auto", "Overloaded", DateTime.UtcNow.AddHours(-13));
        tooOld.StationId = stationId;
        await using var db = NewContext();
        db.WeighingTransactions.AddRange(record, tooOld);
        await db.SaveChangesAsync();

        var found = await new WeighingRepository(db).GetLatestAutoweighByVehicleAsync("kda 123x", stationId);

        found!.Id.Should().Be(record.Id);
    }

    [Theory]
    [InlineData("2026-11-03T21:00:00", "2026-11-04")] // midnight EAT
    [InlineData("2026-11-03T20:59:59", "2026-11-03")]
    [InlineData("2026-11-04T07:00:00", "2026-11-04")]
    public void ToEatDate_UsesTheKenyanCalendarDay(string utc, string expected)
    {
        WeighingQueryHelpers.ToEatDate(DateTime.SpecifyKind(DateTime.Parse(utc), DateTimeKind.Utc))
            .Should().Be(DateTime.Parse(expected));
    }

    [Fact]
    public void StatsRangeForOneDay_CoversExactlyThatDay()
    {
        var (from, toExclusive) = WeighingQueryHelpers.ResolveEatDayRange(new DateTime(2026, 11, 4), new DateTime(2026, 11, 4));

        WeighingQueryHelpers.ToEatDate(from).Should().Be(new DateTime(2026, 11, 4));
        WeighingQueryHelpers.ToEatDate(toExclusive.AddTicks(-1)).Should().Be(new DateTime(2026, 11, 4));
    }
}
