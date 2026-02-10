using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Infrastructure.Security;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.System;

/// <summary>
/// Manages integration configurations with encrypted credential storage.
/// Provides in-memory caching with 5-minute TTL for decrypted configs.
/// </summary>
public class IntegrationConfigService : IIntegrationConfigService
{
    private readonly TruLoadDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<IntegrationConfigService> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public IntegrationConfigService(
        TruLoadDbContext context,
        IEncryptionService encryptionService,
        IMemoryCache cache,
        ILogger<IntegrationConfigService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IntegrationConfigDto?> GetByProviderAsync(string providerName, CancellationToken ct = default)
    {
        var config = await _context.IntegrationConfigs
            .FirstOrDefaultAsync(c => c.ProviderName == providerName && c.DeletedAt == null, ct);

        return config == null ? null : MapToDto(config);
    }

    public async Task<IEnumerable<IntegrationConfigDto>> GetAllAsync(CancellationToken ct = default)
    {
        var configs = await _context.IntegrationConfigs
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.ProviderName)
            .ToListAsync(ct);

        return configs.Select(MapToDto);
    }

    public async Task<IntegrationConfigDto> CreateOrUpdateAsync(UpsertIntegrationConfigRequest request, CancellationToken ct = default)
    {
        var existing = await _context.IntegrationConfigs
            .FirstOrDefaultAsync(c => c.ProviderName == request.ProviderName && c.DeletedAt == null, ct);

        var credentialsJson = JsonSerializer.Serialize(request.Credentials);
        var encryptedCredentials = _encryptionService.Encrypt(credentialsJson);

        if (existing != null)
        {
            existing.DisplayName = request.DisplayName;
            existing.BaseUrl = request.BaseUrl;
            existing.EncryptedCredentials = encryptedCredentials;
            existing.EndpointsJson = request.EndpointsJson;
            existing.AppBaseUrl = request.AppBaseUrl;
            existing.Environment = request.Environment;
            existing.Description = request.Description;
            existing.CredentialsRotatedAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;

            // Auto-generate URLs
            existing.WebhookUrl = BuildWebhookUrl(existing.AppBaseUrl, request.ProviderName);
            existing.CallbackUrl = BuildCallbackUrl(existing.AppBaseUrl, request.ProviderName);
        }
        else
        {
            var config = new IntegrationConfig
            {
                ProviderName = request.ProviderName,
                DisplayName = request.DisplayName,
                BaseUrl = request.BaseUrl,
                EncryptedCredentials = encryptedCredentials,
                EndpointsJson = request.EndpointsJson,
                AppBaseUrl = request.AppBaseUrl,
                Environment = request.Environment,
                Description = request.Description,
                CredentialsRotatedAt = DateTime.UtcNow
            };

            config.WebhookUrl = BuildWebhookUrl(config.AppBaseUrl, request.ProviderName);
            config.CallbackUrl = BuildCallbackUrl(config.AppBaseUrl, request.ProviderName);

            _context.IntegrationConfigs.Add(config);
            existing = config;
        }

        await _context.SaveChangesAsync(ct);

        // Invalidate cache
        _cache.Remove($"integration:credentials:{request.ProviderName}");

        _logger.LogInformation("Integration config created/updated for provider {Provider}", request.ProviderName);

        return MapToDto(existing);
    }

    public async Task<Dictionary<string, string>> GetDecryptedCredentialsAsync(string providerName, CancellationToken ct = default)
    {
        var cacheKey = $"integration:credentials:{providerName}";

        if (_cache.TryGetValue(cacheKey, out Dictionary<string, string>? cached) && cached != null)
            return cached;

        var config = await _context.IntegrationConfigs
            .FirstOrDefaultAsync(c => c.ProviderName == providerName && c.IsActive && c.DeletedAt == null, ct);

        if (config == null)
            throw new InvalidOperationException($"Integration config not found for provider: {providerName}");

        var decryptedJson = _encryptionService.Decrypt(config.EncryptedCredentials);
        var credentials = JsonSerializer.Deserialize<Dictionary<string, string>>(decryptedJson)
            ?? new Dictionary<string, string>();

        _cache.Set(cacheKey, credentials, CacheDuration);

        return credentials;
    }

    public async Task<string> GenerateWebhookUrlAsync(string providerName, CancellationToken ct = default)
    {
        var config = await _context.IntegrationConfigs
            .FirstOrDefaultAsync(c => c.ProviderName == providerName && c.DeletedAt == null, ct);

        return config?.WebhookUrl ?? BuildWebhookUrl(null, providerName);
    }

    public async Task<string> GenerateCallbackUrlAsync(string providerName, CancellationToken ct = default)
    {
        var config = await _context.IntegrationConfigs
            .FirstOrDefaultAsync(c => c.ProviderName == providerName && c.DeletedAt == null, ct);

        return config?.CallbackUrl ?? BuildCallbackUrl(null, providerName);
    }

    private static string BuildWebhookUrl(string? appBaseUrl, string providerName)
    {
        var baseUrl = appBaseUrl?.TrimEnd('/') ?? "http://localhost:4000";
        return $"{baseUrl}/api/v1/payments/webhook/{providerName.Replace("_", "-")}";
    }

    private static string BuildCallbackUrl(string? appBaseUrl, string providerName)
    {
        var baseUrl = appBaseUrl?.TrimEnd('/') ?? "http://localhost:4000";
        return $"{baseUrl}/api/v1/payments/callback/{providerName.Replace("_", "-")}";
    }

    private static IntegrationConfigDto MapToDto(IntegrationConfig config)
    {
        return new IntegrationConfigDto
        {
            Id = config.Id,
            ProviderName = config.ProviderName,
            DisplayName = config.DisplayName,
            BaseUrl = config.BaseUrl,
            EndpointsJson = config.EndpointsJson,
            WebhookUrl = config.WebhookUrl,
            CallbackUrl = config.CallbackUrl,
            AppBaseUrl = config.AppBaseUrl,
            Environment = config.Environment,
            Description = config.Description,
            CredentialsRotatedAt = config.CredentialsRotatedAt,
            IsActive = config.IsActive,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }
}
