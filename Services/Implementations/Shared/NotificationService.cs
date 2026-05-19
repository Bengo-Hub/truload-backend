using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Configuration;
using TruLoad.Backend.DTOs.Notifications;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Data;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.Notifications;
using TruLoad.Backend.Services.Interfaces.Shared;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.Shared;

/// <summary>
/// HTTP client-based implementation that integrates with the centralized Go notifications-service.
/// This service is shared across all TruLoad backend modules (weighing, case management, user management).
/// </summary>
public class NotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly NotificationServiceOptions _options;
    private readonly ITenantContext? _tenantContext;
    private readonly IIntegrationConfigService _configService;
    private readonly ISettingsService _settingsService;
    private readonly TruLoadDbContext _dbContext;
    private readonly ILogger<NotificationService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public NotificationService(
        HttpClient httpClient,
        IOptions<NotificationServiceOptions> options,
        IIntegrationConfigService configService,
        ISettingsService settingsService,
        TruLoadDbContext dbContext,
        ILogger<NotificationService> logger,
        ITenantContext? tenantContext = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _tenantContext = tenantContext;
        _configService = configService;
        _settingsService = settingsService;
        _dbContext = dbContext;
        _logger = logger;

        // Configure HttpClient base address and timeout
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        // Add API key header if configured
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _options.ApiKey);
        }
    }

    /// <summary>
    /// Returns the tenant slug to use for notification routing.
    /// Uses the current request's organisation code (e.g. "KURA" → "kura") when available,
    /// falling back to the configured TenantId for background jobs.
    /// </summary>
    private string GetTenantSlug()
    {
        var orgCode = _tenantContext?.OrganizationCode;
        if (!string.IsNullOrWhiteSpace(orgCode))
            return orgCode.ToLowerInvariant();
        return _options.TenantId;
    }

    /// <summary>
    /// Loads the current tenant's branding fields from the Organization table.
    /// Returns null if tenant context is unavailable or the org cannot be found.
    /// </summary>
    private async Task<Dictionary<string, object>?> GetTenantBrandingAsync(CancellationToken ct)
    {
        if (_tenantContext == null || _tenantContext.OrganizationId == Guid.Empty)
            return null;

        try
        {
            var org = await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == _tenantContext.OrganizationId && o.IsActive, ct);

            if (org == null) return null;

            var branding = new Dictionary<string, object>
            {
                ["brand_name"] = org.Name
            };

            if (!string.IsNullOrWhiteSpace(org.PrimaryColor))
                branding["brand_primary_color"] = org.PrimaryColor;

            if (!string.IsNullOrWhiteSpace(org.SecondaryColor))
                branding["brand_secondary_color"] = org.SecondaryColor;

            if (!string.IsNullOrWhiteSpace(org.ContactEmail))
                branding["brand_email"] = org.ContactEmail;

            if (!string.IsNullOrWhiteSpace(org.ContactPhone))
                branding["brand_phone"] = org.ContactPhone;

            // Build absolute logo URL so email clients can load it
            var logoPath = org.PlatformLogoUrl ?? org.LogoUrl;
            if (!string.IsNullOrWhiteSpace(logoPath))
            {
                if (logoPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    logoPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    branding["brand_logo_url"] = logoPath;
                }
                else if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
                {
                    var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
                    var path = logoPath.StartsWith('/') ? logoPath : "/" + logoPath;
                    branding["brand_logo_url"] = baseUrl + path;
                }
            }

            return branding;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load tenant branding for org {OrgId}", _tenantContext.OrganizationId);
            return null;
        }
    }

    public async Task<bool> SendEmailAsync(
        string templateName,
        string recipientEmail,
        string recipientName,
        Dictionary<string, object> templateData,
        string? subject = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Start with tenant branding as baseline; caller-provided values take precedence
            var branding = await GetTenantBrandingAsync(cancellationToken);
            var enhancedData = branding != null
                ? new Dictionary<string, object>(branding)
                : new Dictionary<string, object>();

            // Merge caller-provided data (overrides branding defaults)
            foreach (var kvp in templateData)
                enhancedData[kvp.Key] = kvp.Value;

            // Always inject recipient identity
            enhancedData["name"] = recipientName;
            enhancedData["recipient_name"] = recipientName;

            var metadata = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(subject))
            {
                metadata["subject"] = subject;
            }

            var request = new NotificationMessageRequest
            {
                Channel = "email",
                Tenant = GetTenantSlug(),
                Template = templateName,
                Data = enhancedData,
                To = new List<string> { recipientEmail },
                Metadata = metadata.Count > 0 ? metadata : null
            };

            return await SendNotificationAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification to {Email} using template {Template}",
                recipientEmail, templateName);
            return false;
        }
    }

    public async Task<bool> SendSmsAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check for dynamic SMS provider overrides (twilio, africastalking)
            var metadata = new Dictionary<string, object>();
            
            var twilioConfig = await _configService.GetByProviderAsync("sms_twilio", cancellationToken);
            if (twilioConfig != null && twilioConfig.IsActive)
            {
                metadata["provider_override"] = "twilio";
            }
            else
            {
                var atConfig = await _configService.GetByProviderAsync("sms_africastalking", cancellationToken);
                if (atConfig != null && atConfig.IsActive)
                {
                    metadata["provider_override"] = "africastalking";
                }
            }

            var request = new NotificationMessageRequest
            {
                Channel = "sms",
                Tenant = GetTenantSlug(),
                Template = "shared/plain_sms", // Generic SMS template
                Data = new Dictionary<string, object>
                {
                    ["message"] = message
                },
                To = new List<string> { phoneNumber },
                Metadata = metadata.Count > 0 ? metadata : null
            };

            return await SendNotificationAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS notification to {PhoneNumber}", phoneNumber);
            return false;
        }
    }

    public async Task<bool> SendPushNotificationAsync(
        Guid userId,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notificationData = new Dictionary<string, object>
            {
                ["title"] = title,
                ["body"] = body
            };

            if (data != null)
            {
                foreach (var kvp in data)
                {
                    notificationData[kvp.Key] = kvp.Value;
                }
            }

            var request = new NotificationMessageRequest
            {
                Channel = "push",
                Tenant = GetTenantSlug(),
                Template = "push_notification", // Generic push notification template
                Data = notificationData,
                To = new List<string> { userId.ToString() }
            };

            return await SendNotificationAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification to user {UserId}", userId);
            return false;
        }
    }

    public async Task<Dictionary<string, bool>> SendMultiChannelAsync(
        Guid userId,
        string emailTemplate,
        string smsMessage,
        string pushTitle,
        string pushBody,
        Dictionary<string, object> templateData,
        string[] channels,
        CancellationToken cancellationToken = default)
    {
        channels ??= new[] { "email" };

        var results = new Dictionary<string, bool>();

        // Execute all channel sends in parallel
        var tasks = new List<Task<(string channel, bool success)>>();

        if (channels.Contains("email"))
        {
            tasks.Add(SendChannelAsync("email", async ct =>
            {
                var email = templateData.GetValueOrDefault("email")?.ToString();
                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning("Email channel requested but no email provided in templateData");
                    return false;
                }

                var name = templateData.GetValueOrDefault("name")?.ToString() ?? "User";
                return await SendEmailAsync(emailTemplate, email, name, templateData, null, ct);
            }, cancellationToken));
        }

        if (channels.Contains("sms"))
        {
            tasks.Add(SendChannelAsync("sms", async ct =>
            {
                var phone = templateData.GetValueOrDefault("phone")?.ToString();
                if (string.IsNullOrWhiteSpace(phone))
                {
                    _logger.LogWarning("SMS channel requested but no phone provided in templateData");
                    return false;
                }

                return await SendSmsAsync(phone, smsMessage, ct);
            }, cancellationToken));
        }

        if (channels.Contains("push"))
        {
            tasks.Add(SendChannelAsync("push", async ct =>
                await SendPushNotificationAsync(userId, pushTitle, pushBody, null, ct),
                cancellationToken));
        }

        var taskResults = await Task.WhenAll(tasks);

        foreach (var (channel, success) in taskResults)
        {
            results[channel] = success;
        }

        return results;
    }

    public async Task<int> SendBatchEmailAsync(
        List<(string Email, string Name)> recipients,
        string templateName,
        Dictionary<string, object> templateData,
        string? subject = null,
        CancellationToken cancellationToken = default)
    {
        var successCount = 0;

        // Send emails in parallel with max degree of parallelism
        var tasks = recipients.Select(async recipient =>
        {
            var success = await SendEmailAsync(
                templateName,
                recipient.Email,
                recipient.Name,
                templateData,
                subject,
                cancellationToken);

            if (success)
            {
                Interlocked.Increment(ref successCount);
            }

            return success;
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Batch email sent: {SuccessCount}/{TotalCount} successful using template {Template}",
            successCount, recipients.Count, templateName);

        return successCount;
    }

    public async Task<bool> UpdatePushSubscriptionAsync(
        Guid userId,
        PushSubscriptionDto subscription,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _dbContext.PushSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == subscription.Endpoint, cancellationToken);

            if (existing != null)
            {
                existing.P256dh = subscription.Keys.P256dh;
                existing.Auth = subscription.Keys.Auth;
                existing.DeviceName = subscription.DeviceName;
                existing.LastUsedAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var newSub = new PushSubscription
                {
                    UserId = userId,
                    Endpoint = subscription.Endpoint,
                    P256dh = subscription.Keys.P256dh,
                    Auth = subscription.Keys.Auth,
                    DeviceName = subscription.DeviceName,
                    LastUsedAt = DateTime.UtcNow
                };
                _dbContext.PushSubscriptions.Add(newSub);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update push subscription for user {UserId}", userId);
            return false;
        }
    }

    public async Task<List<NotificationTemplateDto>> GetTemplatesAsync(
        string? channel = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = "/api/v1/templates";
            if (!string.IsNullOrEmpty(channel))
            {
                endpoint += $"?channel={channel}";
            }

            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<List<NotificationTemplateDto>>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }) ?? new List<NotificationTemplateDto>();
            }

            _logger.LogWarning("Failed to fetch templates from notification service: {StatusCode}", response.StatusCode);
            return new List<NotificationTemplateDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notification templates");
            return new List<NotificationTemplateDto>();
        }
    }

    public async Task<Guid> SendInternalNotificationAsync(Guid userId, string title, string message, string type = "info", string? linkUrl = null, CancellationToken ct = default)
    {
        try
        {
            var notification = new UserNotification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                LinkUrl = linkUrl,
                Timestamp = DateTime.UtcNow,
                IsRead = false
            };

            _dbContext.UserNotifications.Add(notification);
            await _dbContext.SaveChangesAsync(ct);

            // Trigger actual push if user has subscriptions
            var hasSubscriptions = await _dbContext.PushSubscriptions.AnyAsync(s => s.UserId == userId, ct);
            if (hasSubscriptions)
            {
                await SendPushNotificationAsync(userId, title, message, linkUrl != null ? new Dictionary<string, string> { ["url"] = linkUrl } : null, ct);
            }

            return notification.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send internal notification to user {UserId}", userId);
            return Guid.Empty;
        }
    }

    public async Task<List<UserNotification>> GetUserNotificationsAsync(Guid userId, bool? isRead = null, int limit = 50, CancellationToken ct = default)
    {
        var query = _dbContext.UserNotifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.Timestamp)
            .AsQueryable();

        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        return await query.Take(limit).ToListAsync(ct);
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notification = await _dbContext.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);

        if (notification == null) return false;

        notification.IsRead = true;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notification = await _dbContext.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);

        if (notification == null) return false;

        _dbContext.UserNotifications.Remove(notification);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Core method to send notification request to Go notifications-service.
    /// </summary>
    private async Task<bool> SendNotificationAsync(
        NotificationMessageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = "/api/v1/notifications/messages";

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug(
                "Sending {Channel} notification to Go service: Endpoint={Endpoint}, Template={Template}, Recipients={RecipientCount}",
                request.Channel, endpoint, request.Template, request.To.Count);

            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var enqueueResponse = JsonSerializer.Deserialize<NotificationEnqueueResponse>(
                    responseContent,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                _logger.LogInformation(
                    "Notification enqueued successfully: Status={Status}, RequestId={RequestId}, Channel={Channel}, Template={Template}",
                    enqueueResponse?.Status, enqueueResponse?.RequestId, request.Channel, request.Template);

                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Notification service returned error: StatusCode={StatusCode}, Error={Error}",
                response.StatusCode, errorContent);

            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP request failed when sending notification: Channel={Channel}, Template={Template}",
                request.Channel, request.Template);
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex,
                "Notification request timed out: Channel={Channel}, Template={Template}",
                request.Channel, request.Template);
            return false;
        }
    }

    /// <summary>
    /// Helper method to execute channel send and capture result.
    /// </summary>
    private async Task<(string channel, bool success)> SendChannelAsync(
        string channel,
        Func<CancellationToken, Task<bool>> sendFunc,
        CancellationToken cancellationToken)
    {
        try
        {
            var success = await sendFunc(cancellationToken);
            return (channel, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification via {Channel} channel", channel);
            return (channel, false);
        }
    }

    // ── Provider proxy ───────────────────────────────────────────────────────

    public async Task<List<NotificationProviderDto>> GetAvailableProvidersAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/providers/available", ct);
            if (!response.IsSuccessStatusCode) return new List<NotificationProviderDto>();
            var body = await response.Content.ReadAsStringAsync(ct);
            var wrapper = JsonSerializer.Deserialize<ProviderListWrapper>(body, _jsonOpts);
            return wrapper?.Providers ?? new List<NotificationProviderDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch available providers");
            return new List<NotificationProviderDto>();
        }
    }

    public async Task<List<NotificationProviderDto>> GetSelectedProvidersAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/providers/selected", ct);
            if (!response.IsSuccessStatusCode) return new List<NotificationProviderDto>();
            var body = await response.Content.ReadAsStringAsync(ct);
            var wrapper = JsonSerializer.Deserialize<SelectedWrapper>(body, _jsonOpts);
            return wrapper?.Selected?.Select(s => new NotificationProviderDto
            {
                ProviderType = s.GetValueOrDefault("provider_type") ?? string.Empty,
                ProviderName = s.GetValueOrDefault("provider_name") ?? string.Empty,
            }).ToList() ?? new List<NotificationProviderDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch selected providers");
            return new List<NotificationProviderDto>();
        }
    }

    public async Task<bool> SelectProviderAsync(SelectProviderRequest request, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/v1/providers/select", content, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select provider");
            return false;
        }
    }

    public async Task<ProviderSettingsDto?> GetProviderSettingsAsync(string providerType, string providerName, CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/v1/providers/settings?provider_type={providerType}&provider_name={providerName}";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<ProviderSettingsDto>(body, _jsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get provider settings for {Type}/{Name}", providerType, providerName);
            return null;
        }
    }

    public async Task<bool> SaveProviderSettingsAsync(SaveProviderSettingsRequest request, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/v1/providers/settings", content, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save provider settings");
            return false;
        }
    }

    // ── Workflow preferences ─────────────────────────────────────────────────

    public async Task<WorkflowPreferencesDto> GetWorkflowPreferencesAsync(CancellationToken ct = default)
    {
        var defaults = new WorkflowPreferencesDto();
        var raw = await _settingsService.GetSettingValueAsync("notification.workflow.preferences", string.Empty, ct);
        if (string.IsNullOrWhiteSpace(raw)) return defaults;
        try
        {
            return JsonSerializer.Deserialize<WorkflowPreferencesDto>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? defaults;
        }
        catch
        {
            return defaults;
        }
    }

    public async Task SaveWorkflowPreferencesAsync(WorkflowPreferencesDto prefs, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        const string key = "notification.workflow.preferences";
        try
        {
            var existing = await _dbContext.ApplicationSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key, ct);

            if (existing != null)
            {
                existing.SettingValue = json;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _dbContext.ApplicationSettings.Add(new TruLoad.Backend.Models.System.ApplicationSettings
                {
                    SettingKey = key,
                    SettingValue = json,
                    SettingType = "Json",
                    Category = TruLoad.Backend.Models.System.SettingKeys.CategoryNotifications,
                    DisplayName = "Notification Workflow Preferences",
                    IsEditable = true,
                });
            }

            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save workflow preferences");
            throw;
        }
    }

    // ── Push device tokens ───────────────────────────────────────────────────

    public async Task<List<DeviceTokenItemDto>> GetDeviceTokensAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/push/tokens");
            request.Headers.TryAddWithoutValidation("X-User-ID", userId.ToString());
            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return new List<DeviceTokenItemDto>();
            var body = await response.Content.ReadAsStringAsync(ct);
            var wrapper = JsonSerializer.Deserialize<DeviceTokenListWrapper>(body, _jsonOpts);
            return wrapper?.Tokens ?? new List<DeviceTokenItemDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list device tokens for user {UserId}", userId);
            return new List<DeviceTokenItemDto>();
        }
    }

    public async Task<bool> RegisterDeviceTokenAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/push/tokens") { Content = content };
            msg.Headers.TryAddWithoutValidation("X-User-ID", userId.ToString());
            var response = await _httpClient.SendAsync(msg, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device token for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> DeleteDeviceTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { token }, _jsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/push/tokens") { Content = content };
            var response = await _httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete device token");
            return false;
        }
    }

    // ── Private deserialization helpers ──────────────────────────────────────

    private sealed class ProviderListWrapper
    {
        [JsonPropertyName("providers")]
        public List<NotificationProviderDto> Providers { get; set; } = new();
    }

    private sealed class SelectedWrapper
    {
        [JsonPropertyName("selected")]
        public List<Dictionary<string, string>> Selected { get; set; } = new();
    }

    private sealed class DeviceTokenListWrapper
    {
        [JsonPropertyName("tokens")]
        public List<DeviceTokenItemDto> Tokens { get; set; } = new();
    }
}
