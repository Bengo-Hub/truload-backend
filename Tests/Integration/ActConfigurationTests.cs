using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Implementations.System;
using TruLoad.Backend.Tests.Integration.Helpers;
using Xunit;
using FluentAssertions;

namespace TruLoad.Backend.Tests.Integration;

/// <summary>
/// Integration tests for ActConfigurationService.
/// Tests act lookup, configuration retrieval, default act management, and caching.
/// </summary>
public class ActConfigurationTests : IAsyncLifetime
{
    private TruLoadDbContext _context = null!;
    private ActConfigurationService _service = null!;
    private SettingsService _settingsService = null!;

    public async Task InitializeAsync()
    {
        _context = TestDbContextFactory.Create();
        await _context.Database.EnsureCreatedAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        _settingsService = new SettingsService(
            _context, cache, ServiceFactory.CreateNullLogger<SettingsService>());
        _service = new ActConfigurationService(
            _context, cache, _settingsService, ServiceFactory.CreateNullLogger<ActConfigurationService>());

        await SeedTestData();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    #region Seed Data

    private async Task SeedTestData()
    {
        // Seed act definitions
        var acts = new List<ActDefinition>
        {
            new()
            {
                Code = "TRAFFIC_ACT",
                Name = "Kenya Traffic Act Cap 403",
                ActType = "Traffic",
                FullName = "The Traffic Act (Chapter 403 Laws of Kenya)",
                Description = "Kenya national traffic regulations",
                EffectiveDate = new DateOnly(1953, 1, 1),
                ChargingCurrency = "KES"
            },
            new()
            {
                Code = "EAC_ACT",
                Name = "EAC Vehicle Load Control Act 2016",
                ActType = "EAC",
                FullName = "The East African Community Vehicle Load Control Act, 2016",
                Description = "Regional harmonized vehicle load control regulations",
                EffectiveDate = new DateOnly(2016, 7, 1),
                ChargingCurrency = "USD"
            }
        };

        _context.ActDefinitions.AddRange(acts);

        // Seed default act setting
        _context.ApplicationSettings.Add(new ApplicationSettings
        {
            SettingKey = SettingKeys.DefaultActCode,
            SettingValue = "TRAFFIC_ACT",
            SettingType = "String",
            Category = SettingKeys.CategoryCompliance,
            DisplayName = "Default Act Code",
            IsEditable = true,
            DefaultValue = "TRAFFIC_ACT"
        });

        // Seed fee schedules
        _context.AxleFeeSchedules.AddRange(
            new AxleFeeSchedule
            {
                LegalFramework = "TRAFFIC_ACT", FeeType = "GVW",
                OverloadMinKg = 1, OverloadMaxKg = 500,
                FeePerKgUsd = 0.30m, FlatFeeUsd = 50m, DemeritPoints = 1,
                PenaltyDescription = "Minor overload", EffectiveFrom = DateTime.UtcNow.AddYears(-1)
            },
            new AxleFeeSchedule
            {
                LegalFramework = "EAC", FeeType = "GVW",
                OverloadMinKg = 1, OverloadMaxKg = 500,
                FeePerKgUsd = 0.50m, FlatFeeUsd = 0m, DemeritPoints = 1,
                PenaltyDescription = "Minor EAC overload", EffectiveFrom = DateTime.UtcNow.AddYears(-1)
            }
        );

        // Seed tolerance settings
        _context.ToleranceSettings.AddRange(
            new ToleranceSetting
            {
                Code = "TRAFFIC_ACT_GVW", Name = "Traffic Act GVW Tolerance",
                LegalFramework = "TRAFFIC_ACT", TolerancePercentage = 5.0m,
                AppliesTo = "GVW", Description = "5% tolerance on GVW",
                EffectiveFrom = DateTime.UtcNow.AddYears(-1)
            },
            new ToleranceSetting
            {
                Code = "EAC_GVW", Name = "EAC GVW Tolerance",
                LegalFramework = "EAC", TolerancePercentage = 5.0m,
                AppliesTo = "GVW", Description = "5% tolerance on GVW (EAC)",
                EffectiveFrom = DateTime.UtcNow.AddYears(-1)
            }
        );

        // Seed demerit point schedules
        _context.DemeritPointSchedules.AddRange(
            new DemeritPointSchedule
            {
                ViolationType = "GVW", OverloadMinKg = 1, OverloadMaxKg = 2000,
                Points = 1, LegalFramework = "TRAFFIC_ACT",
                EffectiveFrom = DateTime.UtcNow.AddYears(-1)
            },
            new DemeritPointSchedule
            {
                ViolationType = "GVW", OverloadMinKg = 1, OverloadMaxKg = 500,
                Points = 1, LegalFramework = "EAC",
                EffectiveFrom = DateTime.UtcNow.AddYears(-1)
            }
        );

        // Seed axle type overload fee schedules
        _context.AxleTypeOverloadFeeSchedules.Add(new AxleTypeOverloadFeeSchedule
        {
            OverloadMinKg = 1, OverloadMaxKg = 500,
            SteeringAxleFeeUsd = 10m, SingleDriveAxleFeeUsd = 15m,
            TandemAxleFeeUsd = 20m, TridemAxleFeeUsd = 25m, QuadAxleFeeUsd = 30m,
            LegalFramework = "EAC", EffectiveFrom = DateTime.UtcNow.AddYears(-1)
        });

        await _context.SaveChangesAsync();
    }

    #endregion

    #region GetAllActs Tests

    [Fact]
    public async Task GetAllActs_ReturnsAllActiveActs()
    {
        var acts = await _service.GetAllActsAsync();

        acts.Should().HaveCount(2);
        acts.Should().Contain(a => a.Code == "TRAFFIC_ACT");
        acts.Should().Contain(a => a.Code == "EAC_ACT");
    }

    [Fact]
    public async Task GetAllActs_MarksDefaultAct()
    {
        var acts = await _service.GetAllActsAsync();

        var trafficAct = acts.First(a => a.Code == "TRAFFIC_ACT");
        var eacAct = acts.First(a => a.Code == "EAC_ACT");

        trafficAct.IsDefault.Should().BeTrue();
        eacAct.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllActs_ReturnsCorrectCurrencies()
    {
        var acts = await _service.GetAllActsAsync();

        acts.First(a => a.Code == "TRAFFIC_ACT").ChargingCurrency.Should().Be("KES");
        acts.First(a => a.Code == "EAC_ACT").ChargingCurrency.Should().Be("USD");
    }

    #endregion

    #region GetActById Tests

    [Fact]
    public async Task GetActById_ReturnsAct_WhenExists()
    {
        var acts = await _service.GetAllActsAsync();
        var actId = acts.First(a => a.Code == "TRAFFIC_ACT").Id;

        var result = await _service.GetActByIdAsync(actId);

        result.Should().NotBeNull();
        result!.Code.Should().Be("TRAFFIC_ACT");
        result.Name.Should().Be("Kenya Traffic Act Cap 403");
    }

    [Fact]
    public async Task GetActById_ReturnsNull_WhenNotExists()
    {
        var result = await _service.GetActByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    #endregion

    #region GetDefaultAct Tests

    [Fact]
    public async Task GetDefaultAct_ReturnsTrafficAct()
    {
        var defaultAct = await _service.GetDefaultActAsync();

        defaultAct.Should().NotBeNull();
        defaultAct!.Code.Should().Be("TRAFFIC_ACT");
        defaultAct.IsDefault.Should().BeTrue();
        defaultAct.ChargingCurrency.Should().Be("KES");
    }

    #endregion

    #region SetDefaultAct Tests

    [Fact]
    public async Task SetDefaultAct_UpdatesSetting()
    {
        var acts = await _service.GetAllActsAsync();
        var eacActId = acts.First(a => a.Code == "EAC_ACT").Id;
        var userId = Guid.NewGuid();

        var result = await _service.SetDefaultActAsync(eacActId, userId);

        result.Code.Should().Be("EAC_ACT");
        result.IsDefault.Should().BeTrue();

        // Verify default changed via fresh service (avoids stale settings cache)
        var freshCache = new MemoryCache(new MemoryCacheOptions());
        var freshSettingsService = new SettingsService(
            _context, freshCache, ServiceFactory.CreateNullLogger<SettingsService>());
        var freshService = new ActConfigurationService(
            _context, freshCache, freshSettingsService, ServiceFactory.CreateNullLogger<ActConfigurationService>());

        var newDefault = await freshService.GetDefaultActAsync();
        newDefault!.Code.Should().Be("EAC_ACT");
    }

    [Fact]
    public async Task SetDefaultAct_ThrowsForInvalidId()
    {
        var act = () => _service.SetDefaultActAsync(Guid.NewGuid(), Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    #endregion

    #region GetActConfiguration Tests

    [Fact]
    public async Task GetActConfiguration_ReturnsFullConfig_ForTrafficAct()
    {
        var acts = await _service.GetAllActsAsync();
        var trafficActId = acts.First(a => a.Code == "TRAFFIC_ACT").Id;

        var config = await _service.GetActConfigurationAsync(trafficActId);

        config.Should().NotBeNull();
        config!.Act.Code.Should().Be("TRAFFIC_ACT");
        config.FeeSchedules.Should().NotBeEmpty();
        config.ToleranceSettings.Should().NotBeEmpty();
        config.DemeritPointSchedules.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetActConfiguration_ReturnsFullConfig_ForEacAct()
    {
        var acts = await _service.GetAllActsAsync();
        var eacActId = acts.First(a => a.Code == "EAC_ACT").Id;

        var config = await _service.GetActConfigurationAsync(eacActId);

        config.Should().NotBeNull();
        config!.Act.Code.Should().Be("EAC_ACT");
        config.FeeSchedules.Should().NotBeEmpty();
        config.AxleTypeFeeSchedules.Should().NotBeEmpty();
        config.ToleranceSettings.Should().NotBeEmpty();
        config.DemeritPointSchedules.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetActConfiguration_ReturnsNull_ForInvalidId()
    {
        var config = await _service.GetActConfigurationAsync(Guid.NewGuid());
        config.Should().BeNull();
    }

    #endregion

    #region Fee Schedule Tests

    [Fact]
    public async Task GetFeeSchedules_ReturnsForTrafficAct()
    {
        var schedules = await _service.GetFeeSchedulesAsync("TRAFFIC_ACT");

        schedules.Should().NotBeEmpty();
        schedules.Should().AllSatisfy(s => s.LegalFramework.Should().Be("TRAFFIC_ACT"));
    }

    [Fact]
    public async Task GetFeeSchedules_ReturnsForEac()
    {
        var schedules = await _service.GetFeeSchedulesAsync("EAC");

        schedules.Should().NotBeEmpty();
        schedules.Should().AllSatisfy(s => s.LegalFramework.Should().Be("EAC"));
    }

    [Fact]
    public async Task GetFeeSchedules_ReturnsEmpty_ForUnknownFramework()
    {
        var schedules = await _service.GetFeeSchedulesAsync("UNKNOWN");
        schedules.Should().BeEmpty();
    }

    #endregion

    #region Tolerance Tests

    [Fact]
    public async Task GetToleranceSettings_ReturnsForFramework()
    {
        var tolerances = await _service.GetToleranceSettingsAsync("TRAFFIC_ACT");

        tolerances.Should().NotBeEmpty();
        tolerances.First().TolerancePercentage.Should().Be(5.0m);
    }

    #endregion

    #region Demerit Points Tests

    [Fact]
    public async Task GetDemeritPointSchedules_ReturnsForFramework()
    {
        var schedules = await _service.GetDemeritPointSchedulesAsync("TRAFFIC_ACT");

        schedules.Should().NotBeEmpty();
        schedules.First().Points.Should().Be(1);
    }

    #endregion

    #region Summary Tests

    [Fact]
    public async Task GetSummary_ReturnsCorrectCounts()
    {
        var summary = await _service.GetSummaryAsync();

        summary.TotalActs.Should().Be(2);
        summary.DefaultActCode.Should().Be("TRAFFIC_ACT");
        summary.DefaultActName.Should().Be("Kenya Traffic Act Cap 403");
        summary.DefaultCurrency.Should().Be("KES");
        summary.TotalFeeSchedules.Should().BeGreaterThan(0);
        summary.TotalToleranceSettings.Should().BeGreaterThan(0);
        summary.TotalDemeritSchedules.Should().BeGreaterThan(0);
    }

    #endregion

    #region Axle Type Fee Tests

    [Fact]
    public async Task GetAxleTypeFeeSchedules_ReturnsForEac()
    {
        var schedules = await _service.GetAxleTypeFeeSchedulesAsync("EAC");

        schedules.Should().NotBeEmpty();
        schedules.First().SteeringAxleFeeUsd.Should().Be(10m);
    }

    #endregion

    #region Cache Tests

    [Fact]
    public async Task GetAllActs_ReturnsCachedResult_OnSecondCall()
    {
        // First call populates cache
        var acts1 = await _service.GetAllActsAsync();

        // Add a new act directly to DB (bypassing cache)
        _context.ActDefinitions.Add(new ActDefinition
        {
            Code = "TEST_ACT", Name = "Test", ActType = "Test", ChargingCurrency = "USD"
        });
        await _context.SaveChangesAsync();

        // Second call should return cached result (still 2 acts)
        var acts2 = await _service.GetAllActsAsync();
        acts2.Should().HaveCount(acts1.Count);
    }

    [Fact]
    public async Task InvalidateCache_ClearsAllCachedData()
    {
        // Populate cache
        await _service.GetAllActsAsync();

        // Add new act
        _context.ActDefinitions.Add(new ActDefinition
        {
            Code = "TEST_ACT", Name = "Test", ActType = "Test", ChargingCurrency = "USD"
        });
        await _context.SaveChangesAsync();

        // Invalidate cache
        _service.InvalidateCache();

        // Now should see 3 acts
        var acts = await _service.GetAllActsAsync();
        acts.Should().HaveCount(3);
    }

    #endregion
}
