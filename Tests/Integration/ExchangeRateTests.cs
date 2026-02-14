using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Implementations.Financial;
using TruLoad.Backend.Tests.Integration.Helpers;
using Xunit;

namespace TruLoad.Backend.Tests.Integration;

/// <summary>
/// Integration tests for CurrencyService exchange rate management.
/// Tests rate CRUD, conversion, caching, manual rate setting, and API settings.
/// </summary>
public class ExchangeRateTests
{
    private CurrencyService CreateService(string? dbName = null)
    {
        var context = dbName != null
            ? TestDbContextFactory.Create(dbName)
            : TestDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var logger = NullLogger<CurrencyService>.Instance;
        return new CurrencyService(context, cache, httpFactory.Object, logger);
    }

    private (CurrencyService service, string dbName) CreateServiceWithDbName()
    {
        var dbName = Guid.NewGuid().ToString();
        return (CreateService(dbName), dbName);
    }

    private async Task SeedRate(string dbName, decimal rate, string from = "USD", string to = "KES",
        string source = "manual", DateOnly? effectiveDate = null)
    {
        var context = TestDbContextFactory.Create(dbName);
        context.ExchangeRates.Add(new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = from,
            ToCurrency = to,
            Rate = rate,
            EffectiveDate = effectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Source = source,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private async Task SeedApiSettings(string dbName, bool isActive = true, string? accessKey = null)
    {
        var context = TestDbContextFactory.Create(dbName);
        context.ExchangeRateApiSettings.Add(new ExchangeRateApiSettings
        {
            Id = Guid.NewGuid(),
            Provider = "EXCHANGERATE_HOST",
            ProviderName = "exchangerate.host",
            ApiEndpoint = "https://api.exchangerate.host/live",
            EncryptedAccessKey = accessKey,
            SourceCurrency = "USD",
            TargetCurrenciesJson = "[\"KES\"]",
            FetchTime = new TimeOnly(0, 0),
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    // ========================================================================
    // GetCurrentRateAsync
    // ========================================================================

    [Fact]
    public async Task GetCurrentRate_NoRates_ReturnsFallback130()
    {
        var service = CreateService();
        var result = await service.GetCurrentRateAsync();

        Assert.Equal(130.0m, result.Rate);
        Assert.Equal("USD", result.FromCurrency);
        Assert.Equal("KES", result.ToCurrency);
        Assert.Equal("default", result.Source);
    }

    [Fact]
    public async Task GetCurrentRate_WithSeededRate_ReturnsLatest()
    {
        var (service, dbName) = CreateServiceWithDbName();
        await SeedRate(dbName, 129.50m);

        var result = await service.GetCurrentRateAsync();

        Assert.Equal(129.50m, result.Rate);
        Assert.Equal("manual", result.Source);
    }

    [Fact]
    public async Task GetCurrentRate_MultipleRates_ReturnsLatestByDate()
    {
        var (service, dbName) = CreateServiceWithDbName();
        await SeedRate(dbName, 120.0m, effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));
        await SeedRate(dbName, 135.0m, effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow));

        var result = await service.GetCurrentRateAsync();

        Assert.Equal(135.0m, result.Rate);
    }

    [Fact]
    public async Task GetCurrentRate_CacheHit_ReturnsCached()
    {
        var (service, dbName) = CreateServiceWithDbName();
        await SeedRate(dbName, 129.50m);

        var first = await service.GetCurrentRateAsync();
        var second = await service.GetCurrentRateAsync();

        Assert.Equal(first.Rate, second.Rate);
        Assert.Equal(first.Source, second.Source);
    }

    // ========================================================================
    // GetRateHistoryAsync
    // ========================================================================

    [Fact]
    public async Task GetRateHistory_ReturnsRatesWithinDays()
    {
        var (service, dbName) = CreateServiceWithDbName();
        await SeedRate(dbName, 130.0m, effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow));
        await SeedRate(dbName, 129.0m, effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)));
        await SeedRate(dbName, 100.0m, effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));

        var result = await service.GetRateHistoryAsync("USD", "KES", 30);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetRateHistory_OrderedByDateDescending()
    {
        var (service, dbName) = CreateServiceWithDbName();
        await SeedRate(dbName, 125.0m, effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)));
        await SeedRate(dbName, 130.0m, effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow));

        var result = await service.GetRateHistoryAsync();

        Assert.Equal(130.0m, result[0].Rate);
        Assert.Equal(125.0m, result[1].Rate);
    }

    // ========================================================================
    // SetManualRateAsync
    // ========================================================================

    [Fact]
    public async Task SetManualRate_CreatesNewRate()
    {
        var service = CreateService();
        var request = new SetManualRateRequest { Rate = 131.25m };

        var result = await service.SetManualRateAsync(request, Guid.NewGuid());

        Assert.Equal(131.25m, result.Rate);
        Assert.Equal("manual", result.Source);
        Assert.Equal("USD", result.FromCurrency);
        Assert.Equal("KES", result.ToCurrency);
    }

    [Fact]
    public async Task SetManualRate_UpsertsTodaysRate()
    {
        var (service, dbName) = CreateServiceWithDbName();
        await SeedRate(dbName, 130.0m);

        // Create a fresh service to get the rate
        var service2 = CreateService(dbName);
        var request = new SetManualRateRequest { Rate = 132.0m };
        var result = await service2.SetManualRateAsync(request, Guid.NewGuid());

        Assert.Equal(132.0m, result.Rate);

        // Verify only one rate for today
        var history = await service2.GetRateHistoryAsync("USD", "KES", 1);
        Assert.Single(history);
    }

    [Fact]
    public async Task SetManualRate_InvalidatesCache()
    {
        var (service, dbName) = CreateServiceWithDbName();
        await SeedRate(dbName, 130.0m);

        // Warm cache
        var service2 = CreateService(dbName);
        var before = await service2.GetCurrentRateAsync();
        Assert.Equal(130.0m, before.Rate);

        // Set new rate (invalidates cache internally)
        await service2.SetManualRateAsync(new SetManualRateRequest { Rate = 140.0m }, Guid.NewGuid());

        // Next call should see new rate
        var after = await service2.GetCurrentRateAsync();
        Assert.Equal(140.0m, after.Rate);
    }

    // ========================================================================
    // ConvertAsync
    // ========================================================================

    [Fact]
    public async Task ConvertAsync_SameCurrency_ReturnsSameAmount()
    {
        var service = CreateService();
        var result = await service.ConvertAsync(100m, "USD", "USD");
        Assert.Equal(100m, result);
    }

    [Fact]
    public async Task ConvertAsync_UsdToKes_MultipliesByRate()
    {
        var (service, dbName) = CreateServiceWithDbName();
        await SeedRate(dbName, 130.0m);

        var service2 = CreateService(dbName);
        var result = await service2.ConvertAsync(10m, "USD", "KES");
        Assert.Equal(1300.0m, result);
    }

    // ========================================================================
    // API Settings
    // ========================================================================

    [Fact]
    public async Task GetApiSettings_NoSettings_ReturnsNull()
    {
        var service = CreateService();
        var result = await service.GetApiSettingsAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task GetApiSettings_WithSettings_ReturnsDto()
    {
        var (service, dbName) = CreateServiceWithDbName();
        await SeedApiSettings(dbName, isActive: true, accessKey: "test-key-123");

        var service2 = CreateService(dbName);
        var result = await service2.GetApiSettingsAsync();

        Assert.NotNull(result);
        Assert.Equal("EXCHANGERATE_HOST", result!.Provider);
        Assert.Equal("exchangerate.host", result.ProviderName);
        Assert.True(result.HasAccessKey);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetApiSettings_WithoutAccessKey_HasAccessKeyFalse()
    {
        var (service, dbName) = CreateServiceWithDbName();
        await SeedApiSettings(dbName, isActive: true, accessKey: null);

        var service2 = CreateService(dbName);
        var result = await service2.GetApiSettingsAsync();

        Assert.NotNull(result);
        Assert.False(result!.HasAccessKey);
    }

    [Fact]
    public async Task UpdateApiSettings_CreatesWhenNoneExist()
    {
        var service = CreateService();
        var request = new UpdateApiSettingsRequest
        {
            Provider = "CUSTOM",
            ProviderName = "Custom Provider",
            IsActive = true
        };

        var result = await service.UpdateApiSettingsAsync(request, Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("CUSTOM", result.Provider);
        Assert.Equal("Custom Provider", result.ProviderName);
    }

    [Fact]
    public async Task UpdateApiSettings_UpdatesExisting()
    {
        var (service, dbName) = CreateServiceWithDbName();
        await SeedApiSettings(dbName, isActive: true);

        var service2 = CreateService(dbName);
        var request = new UpdateApiSettingsRequest
        {
            ProviderName = "Updated Provider"
        };

        var result = await service2.UpdateApiSettingsAsync(request, Guid.NewGuid());
        Assert.Equal("Updated Provider", result.ProviderName);
    }

    // ========================================================================
    // FetchRatesFromApiAsync
    // ========================================================================

    [Fact]
    public async Task FetchRatesFromApi_NoSettings_DoesNotThrow()
    {
        var service = CreateService();
        await service.FetchRatesFromApiAsync();
        // Should log warning and return without error
    }

    [Fact]
    public async Task FetchRatesFromApi_NoAccessKey_SetsFailedStatus()
    {
        var (service, dbName) = CreateServiceWithDbName();
        await SeedApiSettings(dbName, isActive: true, accessKey: null);

        var service2 = CreateService(dbName);
        await service2.FetchRatesFromApiAsync();

        var settings = await service2.GetApiSettingsAsync();
        Assert.Equal("failed", settings?.LastFetchStatus);
        Assert.Contains("No API access key", settings?.LastFetchError);
    }
}
