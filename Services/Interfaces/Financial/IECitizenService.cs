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
    Task<PesaflowInvoiceResponse> CreatePesaflowInvoiceAsync(CreatePesaflowInvoiceRequest request, CancellationToken ct = default);
    Task<PesaflowPaymentStatusResponse?> QueryPaymentStatusAsync(string invoiceRefNo, CancellationToken ct = default);
    string ComputeSecureHash(string dataString, string apiKey);
    bool VerifyWebhookToken(string tokenHash, string expectedData, string apiKey);
    Task<WebhookProcessingResult> ProcessWebhookNotificationAsync(PesaflowIpnPayload payload, CancellationToken ct = default);
    Task<int> ReconcileUnpaidInvoicesAsync(CancellationToken ct = default);
    Task<bool> ReconcileInvoiceAsync(Guid invoiceId, string? transactionReference, decimal? amountPaid, CancellationToken ct = default);
}
