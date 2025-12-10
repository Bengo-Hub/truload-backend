using TruLoad.Backend.Repositories.Audit.Interfaces;
using TruLoad.Backend.Models;
using truload_backend.Data;
using Microsoft.EntityFrameworkCore;
namespace TruLoad.Backend.Repositories.Audit;

/// <summary>
/// Repository implementation for audit log persistence and querying.
/// Handles all database operations for AuditLog entity.
/// </summary>
/// <remarks>
/// This implementation uses Entity Framework Core for data access.
/// </remarks>
public class AuditLogRepository : IAuditLogRepository    
{
    private readonly TruLoadDbContext _context;
    public AuditLogRepository(TruLoadDbContext context)
    {
        _context = context;
    }
    public async Task<AuditLog> SaveAsync(AuditLog auditLog)
    {
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
        return auditLog;
    }
    public async Task<AuditLog?> GetByIdAsync(Guid id)
    {
        return await _context.AuditLogs.FindAsync(id);
    }
    public async Task<(List<AuditLog> Items, int TotalCount)> GetPagedAsync(
        int skip = 0,
        int take = 50,
        Guid? userId = null,
        string? resourceType = null,
        Guid? resourceId = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? organizationId = null,
        string? orderBy = "CreatedAt")
    {
        var query = _context.AuditLogs.AsQueryable();

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);
        if (!string.IsNullOrEmpty(resourceType))
            query = query.Where(a => a.ResourceType == resourceType);
        if (resourceId.HasValue)
            query = query.Where(a => a.ResourceId == resourceId.Value);
        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);
        if (fromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(a => a.CreatedAt <= toDate.Value);
        if (organizationId.HasValue)
            query = query.Where(a => a.OrganizationId == organizationId.Value);

        var totalCount = await query.CountAsync();

        // Apply ordering
        query = orderBy switch
        {
            "CreatedAt" => query.OrderByDescending(a => a.CreatedAt),
            "UserId" => query.OrderBy(a => a.UserId),
            _ => query.OrderByDescending(a => a.CreatedAt)
        };

        var items = await query.Skip(skip).Take(take).ToListAsync();
        return (items, totalCount);
    }
    public async Task<List<AuditLog>> GetByResourceAsync(string resourceType, Guid resourceId)
    {
        return await _context.AuditLogs
            .Where(a => a.ResourceType == resourceType && a.ResourceId == resourceId)
            .ToListAsync();
    }
    public async Task<List<AuditLog>> GetByUserAsync(Guid userId, int limit = 100)
    {
        return await _context.AuditLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }
    public async Task<List<AuditLog>> GetFailedEntriesAsync(Guid userId, DateTime since)
    {
        return await _context.AuditLogs
            .Where(a => a.UserId == userId && !a.Success && a.CreatedAt >= since)
            .ToListAsync();
    }
    public async Task<(List<AuditLog> Items, int TotalCount)> GetByOrganizationAsync(Guid organizationId, int skip = 0, int take = 50)
    {
        var query = _context.AuditLogs
            .Where(a => a.OrganizationId == organizationId);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, totalCount);
    }
    public async Task<List<AuditLog>> GetByEndpointAsync(string endpoint, DateTime since)
    {
        return await _context.AuditLogs
            .Where(a => a.Endpoint == endpoint && a.CreatedAt >= since)
            .ToListAsync();
    }
    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate)
    {
        var oldLogs = _context.AuditLogs.Where(a => a.CreatedAt < cutoffDate);
        _context.AuditLogs.RemoveRange(oldLogs);
        return await _context.SaveChangesAsync();
    }
    public async Task<int> GetSummaryStatisticsAsync(DateTime since)
    {
        return await _context.AuditLogs
            .Where(a => a.CreatedAt >= since)
            .CountAsync();
    }

    public async Task<DTOs.AuditLogSummaryDto> GetSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(a => a.CreatedAt <= toDate.Value);

        var logs = await query.ToListAsync();

        var summary = new DTOs.AuditLogSummaryDto
        {
            TotalEntries = logs.Count,
            SuccessfulEntries = logs.Count(a => a.Success),
            FailedEntries = logs.Count(a => !a.Success),
            UniqueUsers = logs.Select(a => a.UserId).Distinct().Count(),
            ActionCounts = logs.GroupBy(a => a.Action)
                               .ToDictionary(g => g.Key, g => g.Count()),
            ResourceTypeCounts = logs.GroupBy(a => a.ResourceType)
                                     .ToDictionary(g => g.Key, g => g.Count())
        };

        return summary;
    }
}
