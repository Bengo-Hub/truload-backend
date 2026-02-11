using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TruLoad.Backend.Models.Financial;

/// <summary>
/// Tracks payment callback events from Pesaflow (eCitizen) payment gateway.
/// Provides audit trail for all payment notifications (success, failure, timeout, IPN webhook).
/// </summary>
[Table("payment_callbacks")]
public class PaymentCallback
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// FK to Invoice that this callback relates to
    /// </summary>
    [Column("invoice_id")]
    public Guid? InvoiceId { get; set; }

    /// <summary>
    /// Type of callback: success, failure, timeout, ipn_webhook
    /// </summary>
    [Column("callback_type")]
    [MaxLength(20)]
    [Required]
    public string CallbackType { get; set; } = string.Empty;

    /// <summary>
    /// Pesaflow invoice number (from their system)
    /// </summary>
    [Column("pesaflow_invoice_number")]
    [MaxLength(100)]
    public string? PesaflowInvoiceNumber { get; set; }

    /// <summary>
    /// Payment reference/transaction ID from Pesaflow
    /// </summary>
    [Column("payment_reference")]
    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    /// <summary>
    /// Amount paid (for verification)
    /// </summary>
    [Column("amount")]
    [Precision(18, 2)]
    public decimal? Amount { get; set; }

    /// <summary>
    /// Currency code (e.g., KES)
    /// </summary>
    [Column("currency")]
    [MaxLength(3)]
    public string? Currency { get; set; }

    /// <summary>
    /// Date/time of payment (from Pesaflow)
    /// </summary>
    [Column("payment_date")]
    public DateTime? PaymentDate { get; set; }

    /// <summary>
    /// Full JSON payload from callback/webhook
    /// </summary>
    [Column("raw_payload")]
    public string? RawPayload { get; set; }

    /// <summary>
    /// For webhooks: whether HMAC signature was verified
    /// </summary>
    [Column("signature_verified")]
    public bool? SignatureVerified { get; set; }

    /// <summary>
    /// When the callback was processed/logged
    /// </summary>
    [Column("processed_at")]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Audit: record creation timestamp
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata (failure reason, timeout reason, etc.)
    /// </summary>
    [Column("metadata")]
    public string? Metadata { get; set; }

    // Navigation property
    public virtual Invoice? Invoice { get; set; }
}
