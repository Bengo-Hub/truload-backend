using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Load correction/redistribution memos issued for overloaded vehicles.
/// Tracks the correction process and scheduled reweighs.
/// </summary>
public class LoadCorrectionMemo : BaseEntity
{
    /// <summary>
    /// Unique memo number
    /// </summary>
    public string MemoNo { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to related case register
    /// </summary>
    public Guid CaseRegisterId { get; set; }

    /// <summary>
    /// Foreign key to original weighing transaction
    /// </summary>
    public Guid WeighingId { get; set; }

    /// <summary>
    /// Overload amount that needs correction (in kg)
    /// </summary>
    public int OverloadKg { get; set; }

    /// <summary>
    /// Redistribution type: offload, redistribute
    /// </summary>
    public string RedistributionType { get; set; } = string.Empty;

    /// <summary>
    /// Scheduled time for reweigh after correction
    /// </summary>
    public DateTime? ReweighScheduledAt { get; set; }

    /// <summary>
    /// Foreign key to reweigh transaction (after correction)
    /// </summary>
    public Guid? ReweighWeighingId { get; set; }

    /// <summary>
    /// Whether compliance was achieved after reweigh
    /// </summary>
    public bool ComplianceAchieved { get; set; } = false;

    /// <summary>
    /// Relief truck registration number (if offload method used)
    /// </summary>
    public string? ReliefTruckRegNumber { get; set; }

    /// <summary>
    /// Relief truck empty weight in kg (before loading offloaded cargo)
    /// </summary>
    public int? ReliefTruckEmptyWeightKg { get; set; }

    /// <summary>
    /// Officer who issued the memo
    /// </summary>
    public Guid IssuedById { get; set; }

    /// <summary>
    /// Timestamp when memo was issued
    /// </summary>
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public CaseRegister? CaseRegister { get; set; }
    public WeighingTransaction? Weighing { get; set; }
    public WeighingTransaction? ReweighWeighing { get; set; }
    public ApplicationUser? IssuedBy { get; set; }
    public ICollection<ComplianceCertificate> ComplianceCertificates { get; set; } = new List<ComplianceCertificate>();
}
