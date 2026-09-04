using TruLoad.Backend.DTOs.Financial;

namespace TruLoad.Backend.Services.Interfaces.Financial;

/// <summary>
/// Service for eCitizen/Pesaflow payment integration.
/// Handles OAuth authentication, invoice creation, checkout, webhooks, and reconciliation.
/// </summary>
public interface IECitizenService
{
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
    Task<(bool Success, string Message)> TestConnectivityAsync(CancellationToken ct = default);
    Task<PesaflowInvoiceResponse> CreatePesaflowInvoiceAsync(CreatePesaflowInvoiceRequest request, CancellationToken ct = default);
    Task<PesaflowPaymentStatusResponse?> QueryPaymentStatusAsync(string invoiceRefNo, CancellationToken ct = default);
    string ComputeSecureHash(string dataString, string apiKey);
    bool VerifyWebhookToken(string tokenHash, string expectedData, string apiKey);
    Task<WebhookProcessingResult> ProcessWebhookNotificationAsync(PesaflowIpnPayload payload, CancellationToken ct = default);
    Task<int> ReconcileUnpaidInvoicesAsync(CancellationToken ct = default);
    Task<bool> ReconcileInvoiceAsync(Guid invoiceId, string? transactionReference, decimal? amountPaid, string? alternateReference = null, string? notes = null, CancellationToken ct = default);

    /// <summary>
    /// Builds the absolute frontend URL for the legacy/fallback GET payment-callback endpoint
    /// (used only when Pesaflow doesn't honor the per-invoice callBackURLOnSuccess/Failure/Timeout
    /// URLs already sent at checkout time — see CreatePesaflowInvoiceAsync/BuildResultPageUrl for
    /// the primary path). Resolves the invoice's organisation from invoiceRef (when present) so the
    /// result page loads with tenant context, same as the primary path.
    /// </summary>
    Task<string> BuildFallbackResultRedirectAsync(string? invoiceRef, string? status, CancellationToken ct = default);
}
