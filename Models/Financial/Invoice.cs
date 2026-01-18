using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Models.Prosecution;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Models.Financial;

/// <summary>
/// Generated invoices for violations or services.
/// Tracks payment obligations for case registers, prosecutions, and weighing transactions.
/// </summary>
public class Invoice : BaseEntity
{
    /// <summary>
    /// Unique invoice number
    /// </summary>
    public string InvoiceNo { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to related case register (optional)
    /// </summary>
    public Guid? CaseRegisterId { get; set; }

    /// <summary>
    /// Foreign key to related prosecution case (optional)
    /// </summary>
    public Guid? ProsecutionCaseId { get; set; }

    /// <summary>
    /// Foreign key to related weighing transaction (optional)
    /// </summary>
    public Guid? WeighingId { get; set; }

    /// <summary>
    /// Amount due on this invoice
    /// </summary>
    public decimal AmountDue { get; set; }

    /// <summary>
    /// Currency code (USD, KES, etc.)
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Invoice status: pending, paid, cancelled, void
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Timestamp when invoice was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Due date for payment
    /// </summary>
    public DateTime? DueDate { get; set; }

    // Navigation properties
    public CaseRegister? CaseRegister { get; set; }
    public ProsecutionCase? ProsecutionCase { get; set; }
    public WeighingTransaction? Weighing { get; set; }
    public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
}
