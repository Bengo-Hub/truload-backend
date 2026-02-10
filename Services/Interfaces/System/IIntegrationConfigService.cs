using TruLoad.Backend.DTOs.Financial;

namespace TruLoad.Backend.Services.Interfaces.System;

/// <summary>
/// Service for managing integration configurations with encrypted credentials.
/// </summary>
public interface IIntegrationConfigService
{
    Task<IntegrationConfigDto?> GetByProviderAsync(string providerName, CancellationToken ct = default);
    Task<IEnumerable<IntegrationConfigDto>> GetAllAsync(CancellationToken ct = default);
    Task<IntegrationConfigDto> CreateOrUpdateAsync(UpsertIntegrationConfigRequest request, CancellationToken ct = default);
    Task<Dictionary<string, string>> GetDecryptedCredentialsAsync(string providerName, CancellationToken ct = default);
    Task<string> GenerateWebhookUrlAsync(string providerName, CancellationToken ct = default);
    Task<string> GenerateCallbackUrlAsync(string providerName, CancellationToken ct = default);
}
