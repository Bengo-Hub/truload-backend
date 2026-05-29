using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Models.Financial;

/// <summary>
/// Payment receipts with idempotency support.
/// Records all payments made against invoices, prevents duplicate processing.
/// </summary>
public class Receipt : TenantAwareEntity
{
    /// <summary>
    /// Unique receipt number
    /// </summary>
    public string ReceiptNo { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to the invoice being paid
    /// </summary>
    public Guid InvoiceId { get; set; }

    /// <summary>
    /// Amount paid with this receipt
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Currency code (USD, KES, etc.)
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Payment method: cash, mobile_money, bank_transfer, card
    /// </summary>
    public string PaymentMethod { get; set; } = "cash";

    /// <summary>
    /// External transaction reference (e.g., M-Pesa transaction code)
    /// </summary>
    public string? TransactionReference { get; set; }

    /// <summary>
    /// Client-generated idempotency key for duplicate prevention
    /// </summary>
    public Guid IdempotencyKey { get; set; }

    /// <summary>
    /// Officer who received the payment
    /// </summary>
    public Guid? ReceivedById { get; set; }

    /// <summary>
    /// Timestamp when payment was received
    /// </summary>
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Payment channel from Pesaflow IPN (e.g., "MPESA", "CARD", "BANK", "AIRTEL")
    /// </summary>
    public string? PaymentChannel { get; set; }

    /// <summary>
    /// Alternate eCitizen/Pesaflow reference used to verify this payment when
    /// the original invoice reference could not be used (e.g. iframe blocked on live).
    /// </summary>
    public string? AlternateReference { get; set; }

    /// <summary>
    /// Reconciliation notes explaining why the payment was recorded, especially
    /// when an alternate reference was used. Required when AlternateReference differs
    /// from the invoice's pesaflow reference.
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties
    public Invoice? Invoice { get; set; }
    public ApplicationUser? ReceivedBy { get; set; }
}
