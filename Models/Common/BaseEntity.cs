using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Models.Common;

/// <summary>
/// Base entity class with common audit fields for all models.
/// Provides consistent tracking of entity lifecycle: creation, updates, soft deletion, and active status.
/// Use this for shared configuration/metadata entities that don't need tenant isolation.
/// For tenant-aware entities, use TenantAwareEntity instead.
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

/// <summary>
/// Base entity with multi-tenant (Organization) and multi-outlet (Station) support.
/// Inherit from this for entities that should be isolated by tenant/station.
/// The TenantContext middleware automatically populates these from request context.
/// </summary>
public abstract class TenantAwareEntity : BaseEntity, ITenantAware
{
    /// <summary>
    /// Organization/Tenant ID - provides multi-tenant data isolation.
    /// All queries for tenant-aware entities should filter by this.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Navigation property to the Organization.
    /// </summary>
    public virtual Organization? Organization { get; set; }

    /// <summary>
    /// Station/Outlet ID - provides multi-outlet data isolation within a tenant.
    /// Optional: Some entities are org-wide and don't need station isolation.
    /// </summary>
    public Guid? StationId { get; set; }

    /// <summary>
    /// Navigation property to the Station.
    /// </summary>
    public virtual Station? Station { get; set; }
}
