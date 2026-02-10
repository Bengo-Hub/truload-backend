using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Infrastructure.Security;
using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Data.Seeders.SystemConfiguration;

/// <summary>
/// Seeds IntegrationConfig records for eCitizen/Pesaflow, KeNHA, and NTSA from appsettings.
/// Skips if records already exist. KeNHA and NTSA are seeded as inactive (awaiting live credentials).
/// Only runs in Development environment.
/// </summary>
public class IntegrationConfigSeeder
{
    private readonly TruLoadDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IntegrationConfigSeeder> _logger;

    public IntegrationConfigSeeder(
        TruLoadDbContext context,
        IEncryptionService encryptionService,
        IConfiguration configuration,
        ILogger<IntegrationConfigSeeder> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await SeedECitizenAsync();
        await SeedKeNHAAsync();
        await SeedNTSAAsync();
    }

    private async Task SeedECitizenAsync()
    {
        const string providerName = "ecitizen_pesaflow";

        var existing = await _context.IntegrationConfigs
            .FirstOrDefaultAsync(c => c.ProviderName == providerName && c.DeletedAt == null);

        if (existing != null)
        {
            _logger.LogInformation("IntegrationConfig for {Provider} already exists, skipping seed", providerName);
            return;
        }

        var eCitizenSection = _configuration.GetSection("Services:eCitizen");
        var baseUrl = eCitizenSection["BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("Services:eCitizen:BaseUrl not configured, skipping IntegrationConfig seed");
            return;
        }

        var credentials = new Dictionary<string, string>
        {
            ["ApiKey"] = eCitizenSection["ApiKey"] ?? "",
            ["ApiSecret"] = eCitizenSection["ApiSecret"] ?? "",
            ["ApiClientId"] = eCitizenSection["ApiClientId"] ?? ""
        };

        var credentialsJson = JsonSerializer.Serialize(credentials);
        var encryptedCredentials = _encryptionService.Encrypt(credentialsJson);

        var endpoints = new Dictionary<string, string>
        {
            ["OAuth"] = eCitizenSection["Endpoints:OAuth"] ?? "/api/oauth/generate/token",
            ["OnlineCheckout"] = eCitizenSection["Endpoints:OnlineCheckout"] ?? "/PaymentAPI/iframev2.1.php",
            ["CreateInvoice"] = eCitizenSection["Endpoints:CreateInvoice"] ?? "/api/invoice/create",
            ["CheckPaymentStatus"] = eCitizenSection["Endpoints:CheckInvoicePaymentStatus"]
                ?? "/api/invoice/payment/status"
        };

        var appBaseUrl = "http://localhost:4000";

        var config = new IntegrationConfig
        {
            ProviderName = providerName,
            DisplayName = "eCitizen Pesaflow Payment Gateway",
            BaseUrl = baseUrl,
            EncryptedCredentials = encryptedCredentials,
            EndpointsJson = JsonSerializer.Serialize(endpoints),
            AppBaseUrl = appBaseUrl,
            WebhookUrl = $"{appBaseUrl}/api/v1/payments/webhook/ecitizen-pesaflow",
            CallbackUrl = $"{appBaseUrl}/api/v1/payments/callback/ecitizen-pesaflow",
            Environment = "test",
            Description = "eCitizen Pesaflow test environment for overload fine payments",
            CredentialsRotatedAt = DateTime.UtcNow
        };

        _context.IntegrationConfigs.Add(config);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Seeded IntegrationConfig for {Provider} with BaseUrl={BaseUrl}, WebhookUrl={WebhookUrl}",
            providerName, baseUrl, config.WebhookUrl);
    }

    private async Task SeedKeNHAAsync()
    {
        const string providerName = "kenha";

        var existing = await _context.IntegrationConfigs
            .FirstOrDefaultAsync(c => c.ProviderName == providerName && c.DeletedAt == null);

        if (existing != null)
        {
            _logger.LogInformation("IntegrationConfig for {Provider} already exists, skipping seed", providerName);
            return;
        }

        var kenhaSection = _configuration.GetSection("Services:KeNHA");
        var baseUrl = kenhaSection["BaseUrl"] ?? "https://kenload.kenha.co.ke";

        var credentials = new Dictionary<string, string>
        {
            ["ApiKey"] = kenhaSection["ApiKey"] ?? "AWAITING_LIVE_CREDENTIALS"
        };

        var credentialsJson = JsonSerializer.Serialize(credentials);
        var encryptedCredentials = _encryptionService.Encrypt(credentialsJson);

        var endpoints = new Dictionary<string, string>
        {
            ["VerifyTag"] = kenhaSection["Endpoints:VerifyTag"] ?? "/api/v3/vehicle/tag/verify",
            ["WeighbridgeData"] = kenhaSection["Endpoints:WeighbridgeData"] ?? "/api/weighbridge/data?api_key={api_key}"
        };

        var config = new IntegrationConfig
        {
            ProviderName = providerName,
            DisplayName = "KeNHA Vehicle Tag Verification",
            BaseUrl = baseUrl,
            EncryptedCredentials = encryptedCredentials,
            EndpointsJson = JsonSerializer.Serialize(endpoints),
            Environment = "test",
            IsActive = false,
            Description = "Kenya National Highways Authority tag verification. Checks if vehicle has existing KeNHA tag/prohibition during weighing capture.",
            CredentialsRotatedAt = DateTime.UtcNow
        };

        _context.IntegrationConfigs.Add(config);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Seeded IntegrationConfig for {Provider} (inactive - awaiting live credentials), BaseUrl={BaseUrl}",
            providerName, baseUrl);
    }

    private async Task SeedNTSAAsync()
    {
        const string providerName = "ntsa";

        var existing = await _context.IntegrationConfigs
            .FirstOrDefaultAsync(c => c.ProviderName == providerName && c.DeletedAt == null);

        if (existing != null)
        {
            _logger.LogInformation("IntegrationConfig for {Provider} already exists, skipping seed", providerName);
            return;
        }

        var ntsaSection = _configuration.GetSection("Services:NTSA");
        var baseUrl = ntsaSection["BaseUrl"] ?? "https://api.ntsa.go.ke";

        var credentials = new Dictionary<string, string>
        {
            ["ApiKey"] = ntsaSection["ApiKey"] ?? "AWAITING_LIVE_CREDENTIALS"
        };

        var credentialsJson = JsonSerializer.Serialize(credentials);
        var encryptedCredentials = _encryptionService.Encrypt(credentialsJson);

        var endpoints = new Dictionary<string, string>
        {
            ["VehicleSearch"] = ntsaSection["Endpoints:VehicleSearch"] ?? "/vsearch/sp/qregno",
            ["VehicleDetails"] = ntsaSection["Endpoints:VehicleDetails"] ?? "/api/vehicle/details?reg_no={reg_no}&api_key={api_key}"
        };

        var config = new IntegrationConfig
        {
            ProviderName = providerName,
            DisplayName = "NTSA Vehicle Search & Demerit Points",
            BaseUrl = baseUrl,
            EncryptedCredentials = encryptedCredentials,
            EndpointsJson = JsonSerializer.Serialize(endpoints),
            Environment = "test",
            IsActive = false,
            Description = "National Transport and Safety Authority vehicle search. Provides vehicle owner details lookup and demerit points integration for case register workflows.",
            CredentialsRotatedAt = DateTime.UtcNow
        };

        _context.IntegrationConfigs.Add(config);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Seeded IntegrationConfig for {Provider} (inactive - awaiting live credentials), BaseUrl={BaseUrl}",
            providerName, baseUrl);
    }
}
