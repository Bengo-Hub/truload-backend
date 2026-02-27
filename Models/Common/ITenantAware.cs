using System;

namespace TruLoad.Backend.Models.Common;

/// <summary>
/// Interface for entities that support multi-tenancy (Organization) and multi-outlet (Station) data isolation.
/// Provides the core properties required by global entity queries.
/// </summary>
public interface ITenantAware
{
    /// <summary>
    /// Organization/Tenant ID - provides multi-tenant data isolation.
    /// </summary>
    Guid OrganizationId { get; set; }

    /// <summary>
    /// Station/Outlet ID - provides multi-outlet data isolation within a tenant.
    /// Optional: Some entities are org-wide and don't need station isolation.
    /// </summary>
    Guid? StationId { get; set; }
}
