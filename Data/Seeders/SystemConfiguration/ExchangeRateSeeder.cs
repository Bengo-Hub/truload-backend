using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Data.Seeders.SystemConfiguration;

/// <summary>
/// Seeds default exchange rate (USD/KES) and API settings.
/// Idempotent - skips if records already exist.
/// </summary>
public static class ExchangeRateSeeder
{
    public static async Task SeedAsync(TruLoadDbContext context)
    {
        // Seed default USD→KES rate if none exists
        var hasRates = await context.ExchangeRates.AnyAsync();
        if (!hasRates)
        {
            context.ExchangeRates.Add(new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrency = "USD",
                ToCurrency = "KES",
                Rate = 130.0m,
                EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Source = "manual",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        // Seed default API settings if none exists
        var hasSettings = await context.ExchangeRateApiSettings.AnyAsync();
        if (!hasSettings)
        {
            context.ExchangeRateApiSettings.Add(new ExchangeRateApiSettings
            {
                Id = Guid.NewGuid(),
                Provider = "EXCHANGERATE_HOST",
                ProviderName = "exchangerate.host",
                ApiEndpoint = "https://api.exchangerate.host/live",
                SourceCurrency = "USD",
                TargetCurrenciesJson = "[\"KES\"]",
                FetchTime = new TimeOnly(0, 0),
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }
    }
}
