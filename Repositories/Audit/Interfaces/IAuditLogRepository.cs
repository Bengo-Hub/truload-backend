using TruLoad.Backend.DTOs;
using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.Audit.Interfaces;

/// <summary>
/// Repository interface for audit log persistence and querying.
/// Provides abstraction for audit log data access operations.
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Save an audit log entry to the database.
    /// </summary>
    Task<AuditLog> SaveAsync(AuditLog auditLog);

    /// <summary>
    /// Get audit log by ID.
    /// </summary>
    Task<AuditLog?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get paginated audit logs with optional filters.
    /// </summary>
    Task<(List<AuditLog> Items, int TotalCount)> GetPagedAsync(
        int skip = 0,
        int take = 50,
        Guid? userId = null,
        string? resourceType = null,
        Guid? resourceId = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? organizationId = null,
        string? orderBy = "CreatedAt");

    /// <summary>
    /// Get audit logs for a specific resource.
    /// </summary>
    Task<List<AuditLog>> GetByResourceAsync(string resourceType, Guid resourceId);

    /// <summary>
    /// Get audit logs for a specific user.
    /// </summary>
    Task<List<AuditLog>> GetByUserAsync(Guid userId, int limit = 100);

    /// <summary>
    /// Get failed audit entries for a user in a time period.
    /// </summary>
    Task<List<AuditLog>> GetFailedEntriesAsync(Guid userId, DateTime since);

    /// <summary>
    /// Get audit logs for a specific organization.
    /// </summary>
    Task<(List<AuditLog> Items, int TotalCount)> GetByOrganizationAsync(Guid organizationId, int skip = 0, int take = 50);

    /// <summary>
    /// Get audit entries for a specific endpoint.
    /// </summary>
    Task<List<AuditLog>> GetByEndpointAsync(string endpoint, DateTime since);

    /// <summary>
    /// Delete all audit logs older than specified date.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffDate);

    /// <summary>
    /// Get summary statistics for audit logs.
    /// </summary>
    Task<AuditLogSummaryDto> GetSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null);
}
