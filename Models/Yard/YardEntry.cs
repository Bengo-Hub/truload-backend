using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Models.Yard;

/// <summary>
/// Tracks vehicles sent to holding yard for redistribution, offloading, or permit verification.
/// Part of the prohibition order workflow.
/// </summary>
public class YardEntry : TenantAwareEntity
{
    /// <summary>
    /// Foreign key to the related weighing transaction
    /// </summary>
    public Guid WeighingId { get; set; }


    /// <summary>
    /// Reason for yard entry: redistribution, gvw_overload, permit_check, offload
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Entry status: pending, processing, released, escalated
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Timestamp when vehicle entered the yard
    /// </summary>
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when vehicle was released from yard (nullable)
    /// </summary>
    public DateTime? ReleasedAt { get; set; }

    // Navigation properties
    public WeighingTransaction? Weighing { get; set; }
}
