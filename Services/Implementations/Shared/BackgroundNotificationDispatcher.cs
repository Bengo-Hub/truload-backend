using TruLoad.Backend.Services.Interfaces.Shared;

namespace TruLoad.Backend.Services.Implementations.Shared;

/// <summary>
/// Default <see cref="IBackgroundNotificationDispatcher"/>. Singleton; creates a fresh DI scope
/// per send so the background task never touches the (already-disposed) request scope.
/// Mirrors the scope pattern used by background jobs (e.g. StaleWeighingNotificationJob).
/// </summary>
public class BackgroundNotificationDispatcher : IBackgroundNotificationDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundNotificationDispatcher> _logger;

    public BackgroundNotificationDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundNotificationDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void DispatchWorkflowEmail(
        string? tenantSlug,
        string workflowKey,
        string templateName,
        string? primaryRecipientEmail,
        string? primaryRecipientName,
        Dictionary<string, object> templateData,
        string? subject = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var sent = await notifier.SendWorkflowEmailAsync(
                    workflowKey,
                    templateName,
                    primaryRecipientEmail,
                    primaryRecipientName,
                    templateData,
                    subject,
                    tenantSlug: tenantSlug);

                if (!sent)
                    _logger.LogError(
                        "Workflow email '{WorkflowKey}' (template {Template}) was not sent for tenant {Tenant} — check workflow EmailEnabled, recipients, and notifications-api.",
                        workflowKey, templateName, tenantSlug ?? "(default)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to dispatch workflow email '{WorkflowKey}' (template {Template}) for tenant {Tenant}",
                    workflowKey, templateName, tenantSlug ?? "(default)");
            }
        });
    }
}
