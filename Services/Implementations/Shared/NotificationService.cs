using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TruLoad.Backend.Configuration;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.Notifications;
using TruLoad.Backend.Services.Interfaces.Shared;

namespace TruLoad.Backend.Services.Implementations.Shared;

/// <summary>
/// HTTP client-based implementation that integrates with the centralized Go notifications-service.
/// This service is shared across all TruLoad backend modules (weighing, case management, user management).
/// </summary>
public class NotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly NotificationServiceOptions _options;
    private readonly Services.Interfaces.System.IIntegrationConfigService _configService;
    private readonly TruLoadDbContext _dbContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        HttpClient httpClient,
        IOptions<NotificationServiceOptions> options,
        Services.Interfaces.System.IIntegrationConfigService configService,
        TruLoadDbContext dbContext,
        ILogger<NotificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _configService = configService;
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
            // Enhance template data with recipient name
            var enhancedData = new Dictionary<string, object>(templateData)
            {
                ["name"] = recipientName,
                ["recipient_name"] = recipientName
            };

            var metadata = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(subject))
            {
                metadata["subject"] = subject;
            }

            var request = new NotificationMessageRequest
            {
                Channel = "email",
                Tenant = _options.TenantId,
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
                Tenant = _options.TenantId,
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
                Tenant = _options.TenantId,
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
            var endpoint = $"/api/v1/{_options.TenantId}/notifications/templates";
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
            var endpoint = $"/api/v1/{_options.TenantId}/notifications/messages";

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
}
