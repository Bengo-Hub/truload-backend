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
/// Result of creating a treasury-api Invoice.
/// </summary>
public record TreasuryInvoiceResult(
    string InvoiceId,
    string InvoiceNumber,
    string? CrmCustomerId = null
);

/// <summary>
/// One line of a treasury customer AR statement.
/// </summary>
public record StatementLine(
    DateTime Date,
    string DocType,
    string Reference,
    decimal Debit,
    decimal Credit,
    decimal Balance,
    string Status
);

/// <summary>
/// A customer's period AR statement from treasury-api.
/// </summary>
public record CustomerStatement(
    string? CrmContactId,
    string? CustomerName,
    DateTime From,
    DateTime To,
    decimal TotalInvoiced,
    decimal TotalPaid,
    decimal ClosingBalance,
    List<StatementLine> Lines
);

/// <summary>
/// Request to create a real treasury-api Invoice (AR/GL-backed), as opposed to a bare payment
/// intent. Customer identity (CustomerId/CrmCustomerId) is what lets treasury build a running
/// statement across multiple invoices for the same transporter — omit only for a genuinely
/// anonymous one-off charge.
/// </summary>
public record TreasuryInvoiceRequest(
    string InvoiceType,
    string Description,
    decimal AmountKes,
    string ReferenceId,
    string ReferenceType,
    DateTime DueDate,
    string? CustomerId = null,
    string? CrmCustomerId = null,
    string? CustomerName = null,
    string? CustomerEmail = null,
    string? CustomerPhone = null
);

/// <summary>
/// Client for the treasury-api payment intent and invoice endpoints.
/// Used by commercial tenants whose PaymentGateway = "treasury".
/// </summary>
public interface ITreasuryService
{
    /// <summary>
    /// Creates a pending payment intent in treasury-api. Uses payment_method="pending" — the user
    /// selects the gateway on the shared pay page. Pass referenceType="invoice" with
    /// referenceId=the treasury Invoice.Id (from CreateInvoiceAsync) so treasury's GLSubscriber
    /// correctly skips re-posting revenue that the invoice's Send already posted to AR.
    /// </summary>
    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        string tenantSlug,
        decimal amountKes,
        string referenceId,
        string description,
        string referenceType = "weighing_invoice",
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current status of a payment intent.
    /// </summary>
    Task<PaymentIntentResult> GetPaymentIntentAsync(
        string tenantSlug,
        string intentId,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a real treasury-api Invoice (draft status). Does NOT post any GL entry by itself —
    /// call SendInvoiceAsync to post the AR journal (Dr AR / Cr Revenue).
    /// </summary>
    Task<TreasuryInvoiceResult> CreateInvoiceAsync(
        string tenantSlug,
        TreasuryInvoiceRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a treasury-api Invoice, posting its AR journal entry (Dr AR / Cr Revenue) and
    /// projecting it into the customer's running statement.
    /// </summary>
    Task SendInvoiceAsync(
        string tenantSlug,
        string treasuryInvoiceId,
        CancellationToken ct = default);

    /// <summary>
    /// Records a payment against a treasury-api Invoice, posting Dr Cash / Cr AR and closing out
    /// (fully or partially) the AR balance opened by SendInvoiceAsync. There is no automatic
    /// treasury-side link from a successful payment intent to an invoice — GLSubscriber
    /// deliberately skips GL posting for reference_type="invoice" intents specifically because
    /// this call is expected to do it instead. Must be called explicitly once a payment intent
    /// tied to this invoice is confirmed (see TreasuryWebhookController).
    /// </summary>
    Task RecordInvoicePaymentAsync(
        string tenantSlug,
        string treasuryInvoiceId,
        decimal amountKes,
        string method,
        string? reference = null,
        CancellationToken ct = default);

    /// <summary>
    /// Fetches a customer's period AR statement — the running balance built from every treasury
    /// Invoice sent for this transporter (via CreateInvoiceAsync/SendInvoiceAsync) plus every
    /// payment recorded against them (via RecordInvoicePaymentAsync). Defaults to a wide range
    /// covering effectively "all time" when from/to are omitted.
    /// </summary>
    Task<CustomerStatement> GetCustomerStatementAsync(
        string tenantSlug,
        Guid crmContactId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);
}
