using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Compliance certificates issued after successful reweigh.
/// Confirms vehicle has achieved compliance after load correction.
/// </summary>
public class ComplianceCertificate : TenantAwareEntity
{
    /// <summary>
    /// Unique certificate number
    /// </summary>
    public string CertificateNo { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to related case register
    /// </summary>
    public Guid CaseRegisterId { get; set; }

    /// <summary>
    /// Foreign key to compliant weighing transaction
    /// </summary>
    public Guid WeighingId { get; set; }

    /// <summary>
    /// Foreign key to related load correction memo
    /// </summary>
    public Guid? LoadCorrectionMemoId { get; set; }

    /// <summary>
    /// Officer who issued the certificate
    /// </summary>
    public Guid IssuedById { get; set; }

    /// <summary>
    /// Timestamp when certificate was issued
    /// </summary>
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public CaseRegister? CaseRegister { get; set; }
    public WeighingTransaction? Weighing { get; set; }
    public LoadCorrectionMemo? LoadCorrectionMemo { get; set; }
    public ApplicationUser? IssuedBy { get; set; }
}
