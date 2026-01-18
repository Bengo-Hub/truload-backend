using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Infrastructure;

/// <summary>
/// Tracks database seeding executions to prevent redundant seeding on every startup.
/// </summary>
public class DatabaseSeedingHistory : BaseEntity
{
    /// <summary>
    /// Name of the seeding operation (e.g., "InitialSeed", "PermissionsSeed")
    /// </summary>
    public required string SeedingName { get; set; }

    /// <summary>
    /// Version of the seeding operation (incremental, allows re-seeding when version changes)
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Indicates if the seeding completed successfully
    /// </summary>
    public bool IsCompleted { get; set; } = true;

    /// <summary>
    /// Additional notes or error messages
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Duration of the seeding operation in milliseconds
    /// </summary>
    public long DurationMs { get; set; }
}
