using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Services.Interfaces.Financial;
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
    private readonly IECitizenService _eCitizenService;
    private readonly ILogger<IntegrationConfigController> _logger;

    public IntegrationConfigController(
        IIntegrationConfigService integrationConfigService,
        IECitizenService eCitizenService,
        ILogger<IntegrationConfigController> logger)
    {
        _integrationConfigService = integrationConfigService;
        _eCitizenService = eCitizenService;
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
    // Notification channel providers (SMS, SMTP email) are managed by the centralized
    // notifications-service and must not be configured here.
    private static readonly HashSet<string> NotificationManagedProviders =
        new(StringComparer.OrdinalIgnoreCase) { "sms_twilio", "sms_africastalking", "email_smtp" };

    [HttpPut("api/v1/system/integrations/{providerName}")]
    [HasPermission("config.update")]
    public async Task<IActionResult> Upsert(
        string providerName,
        [FromBody] UpsertIntegrationConfigRequest request,
        CancellationToken ct)
    {
        if (NotificationManagedProviders.Contains(providerName))
            return BadRequest(new
            {
                message = $"Provider '{providerName}' is managed by the notifications-service. Configure it in the notifications admin panel."
            });

        request.ProviderName = providerName;
        var result = await _integrationConfigService.CreateOrUpdateAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Test connectivity to an integration provider.
    /// For eCitizen Pesaflow: clears the OAuth token cache and attempts a fresh token request.
    /// For other providers: verifies credentials are present and decryptable.
    /// </summary>
    [HttpPost("api/v1/system/integrations/{providerName}/test")]
    [HasPermission("config.read")]
    public async Task<IActionResult> TestConnectivity(string providerName, CancellationToken ct)
    {
        try
        {
            if (providerName == "ecitizen_pesaflow")
            {
                var (success, message) = await _eCitizenService.TestConnectivityAsync(ct);
                return Ok(new { success, provider = providerName, message });
            }

            // Generic fallback: verify credentials are present and decryptable
            var config = await _integrationConfigService.GetByProviderAsync(providerName, ct);
            if (config == null)
                return NotFound(new { message = $"Integration config not found: {providerName}" });

            var credentials = await _integrationConfigService.GetDecryptedCredentialsAsync(providerName, ct);
            return Ok(new
            {
                success = credentials.Count > 0,
                provider = providerName,
                message = credentials.Count > 0
                    ? "Configuration is valid and credentials are decryptable"
                    : "No credentials found — please configure the integration first"
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
