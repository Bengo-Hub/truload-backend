namespace TruLoad.Backend.Services.Interfaces.Financial;

/// <summary>
/// Result of a treasury-api payment intent creation or query.
/// </summary>
public record PaymentIntentResult(
    string IntentId,
    string Status,
    decimal Amount,
    string Currency,
    string? AuthorizationUrl = null,
    string? CheckoutRequestId = null
);

/// <summary>
/// Client for the treasury-api payment intent endpoints.
/// Used by commercial tenants whose PaymentGateway = "treasury".
/// </summary>
public interface ITreasuryService
{
    /// <summary>
    /// Creates a pending payment intent in treasury-api for a commercial weighing invoice.
    /// Uses payment_method="pending" — the user selects the gateway on the shared pay page.
    /// </summary>
    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        string tenantSlug,
        decimal amountKes,
        string referenceId,
        string description,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current status of a payment intent.
    /// </summary>
    Task<PaymentIntentResult> GetPaymentIntentAsync(
        string tenantSlug,
        string intentId,
        CancellationToken ct = default);
}
