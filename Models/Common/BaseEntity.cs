namespace TruLoad.Backend.Models.Common;

/// <summary>
/// Base entity class with common audit fields for all models.
/// Provides consistent tracking of entity lifecycle: creation, updates, soft deletion, and active status.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Unique identifier for the entity (primary key).
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Indicates whether the entity is active in the system.
    /// Inactive entities are typically hidden from normal queries.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// UTC timestamp when the entity was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the entity was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the entity was soft-deleted.
    /// Null if the entity has not been deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
