using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Interfaces.Financial;

namespace TruLoad.Backend.Services.Implementations.Financial;

/// <summary>
/// Service for exchange rate management and currency conversion.
/// Supports manual rates and automated API sync. Caches current rate for 1 hour.
/// </summary>
public class CurrencyService : ICurrencyService
{
    private readonly TruLoadDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CurrencyService> _logger;

    private const string CacheKeyPrefix = "ExchangeRate_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public CurrencyService(
        TruLoadDbContext context,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<CurrencyService> logger)
    {
        _context = context;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<CurrentRateResponse> GetCurrentRateAsync(string from = "USD", string to = "KES", CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}Current_{from}_{to}";

        if (_cache.TryGetValue(cacheKey, out CurrentRateResponse? cached) && cached != null)
            return cached;

        var rate = await _context.Set<ExchangeRate>()
            .AsNoTracking()
            .Where(r => r.FromCurrency == from && r.ToCurrency == to && r.IsActive && r.DeletedAt == null)
            .OrderByDescending(r => r.EffectiveDate)
            .FirstOrDefaultAsync(ct);

        var response = new CurrentRateResponse
        {
            Rate = rate?.Rate ?? 130.0m,
            FromCurrency = from,
            ToCurrency = to,
            EffectiveDate = rate?.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Source = rate?.Source ?? "default",
            LastUpdated = rate?.CreatedAt
        };

        _cache.Set(cacheKey, response, CacheDuration);
        return response;
    }

    public async Task<List<ExchangeRateDto>> GetRateHistoryAsync(string from = "USD", string to = "KES", int days = 30, CancellationToken ct = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));

        return await _context.Set<ExchangeRate>()
            .AsNoTracking()
            .Where(r => r.FromCurrency == from && r.ToCurrency == to
                && r.EffectiveDate >= cutoff && r.IsActive && r.DeletedAt == null)
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => new ExchangeRateDto
            {
                Id = r.Id,
                FromCurrency = r.FromCurrency,
                ToCurrency = r.ToCurrency,
                Rate = r.Rate,
                EffectiveDate = r.EffectiveDate,
                Source = r.Source,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<ExchangeRateDto> SetManualRateAsync(SetManualRateRequest request, Guid userId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Check for existing rate today
        var existing = await _context.Set<ExchangeRate>()
            .FirstOrDefaultAsync(r => r.FromCurrency == request.FromCurrency
                && r.ToCurrency == request.ToCurrency
                && r.EffectiveDate == today && r.DeletedAt == null, ct);

        if (existing != null)
        {
            existing.Rate = request.Rate;
            existing.Source = "manual";
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new ExchangeRate
            {
                FromCurrency = request.FromCurrency,
                ToCurrency = request.ToCurrency,
                Rate = request.Rate,
                EffectiveDate = today,
                Source = "manual"
            };
            _context.Set<ExchangeRate>().Add(existing);
        }

        await _context.SaveChangesAsync(ct);
        InvalidateCache(request.FromCurrency, request.ToCurrency);

        _logger.LogInformation("Manual exchange rate set: {From}/{To} = {Rate} by user {UserId}",
            request.FromCurrency, request.ToCurrency, request.Rate, userId);

        return new ExchangeRateDto
        {
            Id = existing.Id,
            FromCurrency = existing.FromCurrency,
            ToCurrency = existing.ToCurrency,
            Rate = existing.Rate,
            EffectiveDate = existing.EffectiveDate,
            Source = existing.Source,
            IsActive = existing.IsActive,
            CreatedAt = existing.CreatedAt
        };
    }

    public async Task<decimal> ConvertAsync(decimal amount, string from, string to, CancellationToken ct = default)
    {
        if (from == to) return amount;

        var currentRate = await GetCurrentRateAsync(from, to, ct);
        return amount * currentRate.Rate;
    }

    public async Task<ExchangeRateApiSettingsDto?> GetApiSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _context.Set<ExchangeRateApiSettings>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IsActive && s.DeletedAt == null, ct);

        if (settings == null) return null;

        return new ExchangeRateApiSettingsDto
        {
            Id = settings.Id,
            Provider = settings.Provider,
            ProviderName = settings.ProviderName,
            ApiEndpoint = settings.ApiEndpoint,
            HasAccessKey = !string.IsNullOrEmpty(settings.EncryptedAccessKey),
            SourceCurrency = settings.SourceCurrency,
            TargetCurrenciesJson = settings.TargetCurrenciesJson,
            FetchTime = settings.FetchTime,
            LastFetchAt = settings.LastFetchAt,
            LastFetchStatus = settings.LastFetchStatus,
            LastFetchError = settings.LastFetchError,
            IsActive = settings.IsActive
        };
    }

    public async Task<ExchangeRateApiSettingsDto> UpdateApiSettingsAsync(UpdateApiSettingsRequest request, Guid userId, CancellationToken ct = default)
    {
        var settings = await _context.Set<ExchangeRateApiSettings>()
            .FirstOrDefaultAsync(s => s.DeletedAt == null, ct);

        if (settings == null)
        {
            settings = new ExchangeRateApiSettings();
            _context.Set<ExchangeRateApiSettings>().Add(settings);
        }

        if (request.Provider != null) settings.Provider = request.Provider;
        if (request.ProviderName != null) settings.ProviderName = request.ProviderName;
        if (request.ApiEndpoint != null) settings.ApiEndpoint = request.ApiEndpoint;
        if (request.AccessKey != null) settings.EncryptedAccessKey = request.AccessKey; // TODO: encrypt
        if (request.SourceCurrency != null) settings.SourceCurrency = request.SourceCurrency;
        if (request.TargetCurrenciesJson != null) settings.TargetCurrenciesJson = request.TargetCurrenciesJson;
        if (request.FetchTime.HasValue) settings.FetchTime = request.FetchTime.Value;
        if (request.IsActive.HasValue) settings.IsActive = request.IsActive.Value;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Exchange rate API settings updated by user {UserId}", userId);

        return (await GetApiSettingsAsync(ct))!;
    }

    public async Task FetchRatesFromApiAsync(CancellationToken ct = default)
    {
        var settings = await _context.Set<ExchangeRateApiSettings>()
            .FirstOrDefaultAsync(s => s.IsActive && s.DeletedAt == null, ct);

        if (settings == null)
        {
            _logger.LogWarning("No active exchange rate API settings found, skipping fetch");
            return;
        }

        if (string.IsNullOrEmpty(settings.EncryptedAccessKey))
        {
            _logger.LogWarning("No API access key configured, skipping fetch");
            settings.LastFetchStatus = "failed";
            settings.LastFetchError = "No API access key configured";
            settings.LastFetchAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Skip if already fetched today
        var existingToday = await _context.Set<ExchangeRate>()
            .AnyAsync(r => r.Source == "api" && r.EffectiveDate == today
                && r.FromCurrency == settings.SourceCurrency && r.DeletedAt == null, ct);

        if (existingToday)
        {
            _logger.LogDebug("Exchange rates already fetched today, skipping");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{settings.ApiEndpoint}?access_key={settings.EncryptedAccessKey}&source={settings.SourceCurrency}&currencies=KES";

            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<ExchangeRateApiResponse>(cancellationToken: ct);

            if (json?.Success == true && json.Quotes != null)
            {
                foreach (var quote in json.Quotes)
                {
                    var toCurrency = quote.Key.Length >= 6 ? quote.Key[3..] : quote.Key;
                    var rate = new ExchangeRate
                    {
                        FromCurrency = settings.SourceCurrency,
                        ToCurrency = toCurrency,
                        Rate = quote.Value,
                        EffectiveDate = today,
                        Source = "api"
                    };
                    _context.Set<ExchangeRate>().Add(rate);

                    // Also add reverse rate
                    if (quote.Value > 0)
                    {
                        _context.Set<ExchangeRate>().Add(new ExchangeRate
                        {
                            FromCurrency = toCurrency,
                            ToCurrency = settings.SourceCurrency,
                            Rate = 1.0m / quote.Value,
                            EffectiveDate = today,
                            Source = "api"
                        });
                    }
                }

                settings.LastFetchStatus = "success";
                settings.LastFetchError = null;
                InvalidateCache(settings.SourceCurrency, "KES");
            }
            else
            {
                settings.LastFetchStatus = "failed";
                settings.LastFetchError = json?.Error?.Info ?? "Unknown API error";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch exchange rates from API");
            settings.LastFetchStatus = "failed";
            settings.LastFetchError = ex.Message;
        }

        settings.LastFetchAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    private void InvalidateCache(string from, string to)
    {
        _cache.Remove($"{CacheKeyPrefix}Current_{from}_{to}");
        _cache.Remove($"{CacheKeyPrefix}Current_{to}_{from}");
    }

    /// <summary>
    /// Response structure from exchangerate.host API
    /// </summary>
    private class ExchangeRateApiResponse
    {
        public bool Success { get; set; }
        public Dictionary<string, decimal>? Quotes { get; set; }
        public ApiError? Error { get; set; }
    }

    private class ApiError
    {
        public int Code { get; set; }
        public string? Info { get; set; }
    }
}
