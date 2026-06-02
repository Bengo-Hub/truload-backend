namespace TruLoad.Backend.Services.Interfaces.Shared;

/// <summary>
/// Dispatches workflow notification emails on a background task using a fresh DI scope.
/// Trigger sites (weighing/case/invoice) fire-and-forget without holding the request's
/// scoped <c>DbContext</c>/<c>ITenantContext</c> — which are disposed once the HTTP request
/// completes. This dispatcher resolves a new <see cref="INotificationService"/> per send and
/// passes the tenant slug explicitly, so emails are sent reliably after the request returns.
/// </summary>
public interface IBackgroundNotificationDispatcher
{
    /// <summary>
    /// Queues a workflow email to be sent on a background task with its own DI scope.
    /// </summary>
    /// <param name="tenantSlug">Tenant slug captured from the request (e.g. "kura"); resolved off-request.</param>
    void DispatchWorkflowEmail(
        string? tenantSlug,
        string workflowKey,
        string templateName,
        string? primaryRecipientEmail,
        string? primaryRecipientName,
        Dictionary<string, object> templateData,
        string? subject = null);
}
