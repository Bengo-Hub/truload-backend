using TruLoad.Backend.Models.Common;
using System.Text.Json;

namespace TruLoad.Backend.Models.Offline;

/// <summary>
/// Queue for offline submissions and synchronization tracking.
/// Handles offline-first operations with idempotency and retry logic.
/// </summary>
public class DeviceSyncEvent : BaseEntity
{
    /// <summary>
    /// Device identifier (unique per device)
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Entity type being synced: weighing, case_register, yard_entry, etc.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Target entity ID (after successful sync, nullable before sync)
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Client-generated correlation ID for idempotency
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Operation type: create, update, delete
    /// </summary>
    public string Operation { get; set; } = "create";

    /// <summary>
    /// Full entity payload (JSON)
    /// </summary>
    public JsonDocument Payload { get; set; } = null!;

    /// <summary>
    /// Sync status: queued, processing, synced, failed
    /// </summary>
    public string SyncStatus { get; set; } = "queued";

    /// <summary>
    /// Number of sync attempts made
    /// </summary>
    public int SyncAttempts { get; set; } = 0;

    /// <summary>
    /// Last sync attempt timestamp
    /// </summary>
    public DateTime? LastSyncAttemptAt { get; set; }

    /// <summary>
    /// Error message if sync failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Successful sync timestamp
    /// </summary>
    public DateTime? SyncedAt { get; set; }
}
