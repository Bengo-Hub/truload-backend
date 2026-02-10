using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Controllers.System;

/// <summary>
/// Admin endpoints for managing integration configurations.
/// Credentials are stored encrypted and never exposed via the API.
/// </summary>
[ApiController]
[Authorize]
public class IntegrationConfigController : ControllerBase
{
    private readonly IIntegrationConfigService _integrationConfigService;
    private readonly ILogger<IntegrationConfigController> _logger;

    public IntegrationConfigController(
        IIntegrationConfigService integrationConfigService,
        ILogger<IntegrationConfigController> logger)
    {
        _integrationConfigService = integrationConfigService;
        _logger = logger;
    }

    /// <summary>
    /// List all integration configurations (credentials excluded).
    /// </summary>
    [HttpGet("api/v1/system/integrations")]
    [HasPermission("config.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var configs = await _integrationConfigService.GetAllAsync(ct);
        return Ok(configs);
    }

    /// <summary>
    /// Get a specific integration configuration by provider name.
    /// </summary>
    [HttpGet("api/v1/system/integrations/{providerName}")]
    [HasPermission("config.read")]
    public async Task<IActionResult> GetByProvider(string providerName, CancellationToken ct)
    {
        var config = await _integrationConfigService.GetByProviderAsync(providerName, ct);

        if (config == null)
            return NotFound(new { message = $"Integration config not found: {providerName}" });

        return Ok(config);
    }

    /// <summary>
    /// Create or update an integration configuration.
    /// Credentials are encrypted before storage.
    /// </summary>
    [HttpPut("api/v1/system/integrations/{providerName}")]
    [HasPermission("config.update")]
    public async Task<IActionResult> Upsert(
        string providerName,
        [FromBody] UpsertIntegrationConfigRequest request,
        CancellationToken ct)
    {
        request.ProviderName = providerName;
        var result = await _integrationConfigService.CreateOrUpdateAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Test connectivity to an integration provider.
    /// </summary>
    [HttpPost("api/v1/system/integrations/{providerName}/test")]
    [HasPermission("config.read")]
    public async Task<IActionResult> TestConnectivity(string providerName, CancellationToken ct)
    {
        try
        {
            var credentials = await _integrationConfigService.GetDecryptedCredentialsAsync(providerName, ct);
            var config = await _integrationConfigService.GetByProviderAsync(providerName, ct);

            if (config == null)
                return NotFound(new { message = $"Integration config not found: {providerName}" });

            return Ok(new
            {
                success = true,
                provider = providerName,
                baseUrl = config.BaseUrl,
                environment = config.Environment,
                hasCredentials = credentials.Count > 0,
                credentialKeys = credentials.Keys.ToList(),
                message = "Configuration is valid and credentials are decryptable"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connectivity test failed for provider {Provider}", providerName);
            return Ok(new
            {
                success = false,
                provider = providerName,
                message = $"Test failed: {ex.Message}"
            });
        }
    }
}
