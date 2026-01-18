using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Models.Financial;

namespace TruLoad.Backend.Models.Prosecution;

/// <summary>
/// Detailed prosecution workflow tracking with automated charge computation.
/// Calculates fees based on GVW vs Axle overload and applies best charge basis.
/// </summary>
public class ProsecutionCase : BaseEntity
{
    /// <summary>
    /// Foreign key to related case register (one-to-one)
    /// </summary>
    public Guid CaseRegisterId { get; set; }

    /// <summary>
    /// Foreign key to related weighing transaction
    /// </summary>
    public Guid? WeighingId { get; set; }

    /// <summary>
    /// Prosecuting officer assigned to this case
    /// </summary>
    public Guid ProsecutionOfficerId { get; set; }

    /// <summary>
    /// Applicable Act (EAC Vehicle Load Control Act or Kenya Traffic Act)
    /// </summary>
    public Guid ActId { get; set; }

    /// <summary>
    /// GVW overload amount in kg
    /// </summary>
    public int GvwOverloadKg { get; set; }

    /// <summary>
    /// GVW overload fee in USD
    /// </summary>
    public decimal GvwFeeUsd { get; set; }

    /// <summary>
    /// GVW overload fee in KES
    /// </summary>
    public decimal GvwFeeKes { get; set; }

    /// <summary>
    /// Maximum axle overload in kg (from all axles)
    /// </summary>
    public int MaxAxleOverloadKg { get; set; }

    /// <summary>
    /// Maximum axle overload fee in USD
    /// </summary>
    public decimal MaxAxleFeeUsd { get; set; }

    /// <summary>
    /// Maximum axle overload fee in KES
    /// </summary>
    public decimal MaxAxleFeeKes { get; set; }

    /// <summary>
    /// Best charge basis: gvw or axle (whichever is higher)
    /// </summary>
    public string BestChargeBasis { get; set; } = "gvw";

    /// <summary>
    /// Penalty multiplier (1x for first offense, 5x for repeat offenses within 12 months)
    /// </summary>
    public decimal PenaltyMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// Total charge in USD (best basis * multiplier)
    /// </summary>
    public decimal TotalFeeUsd { get; set; }

    /// <summary>
    /// Total charge in KES
    /// </summary>
    public decimal TotalFeeKes { get; set; }

    /// <summary>
    /// USD to KES forex rate at time of charge calculation
    /// </summary>
    public decimal ForexRate { get; set; }

    /// <summary>
    /// Unique certificate number for prosecution
    /// </summary>
    public string? CertificateNo { get; set; }

    /// <summary>
    /// Additional prosecution notes
    /// </summary>
    public string? CaseNotes { get; set; }

    /// <summary>
    /// Vector embedding for case notes (semantic search).
    /// 384 dimensions for all-MiniLM-L12-v2 model.
    /// NotMapped by default - explicitly configured for PostgreSQL only.
    /// </summary>
    [NotMapped]
    public Pgvector.Vector? CaseNotesEmbedding { get; set; }

    /// <summary>
    /// Prosecution case status: pending, invoiced, paid, court
    /// </summary>
    public string Status { get; set; } = "pending";

    // Navigation properties
    public CaseRegister? CaseRegister { get; set; }
    public WeighingTransaction? Weighing { get; set; }
    public ApplicationUser? ProsecutionOfficer { get; set; }
    public ActDefinition? Act { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
