using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs;
using TruLoad.Backend.DTOs.Audit;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Repositories.Audit.Interfaces;

namespace TruLoad.Backend.Controllers.System;

/// <summary>
/// Controller for managing audit log operations.
/// Provides endpoints for querying system audit trails.
/// </summary>
[ApiController]
[Route("api/v1/audit-logs")]
[Authorize]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<AuditLogController> _logger;

    public AuditLogController(
        IAuditLogRepository auditLogRepository,
        ILogger<AuditLogController> logger)
    {
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get paginated audit logs with optional filters.
    /// </summary>
    [HttpGet]
    [HasPermission("system.audit_logs")]
    [ProducesResponseType(typeof(PagedResponse<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AuditLogDto>>> GetPaged([FromQuery] AuditLogQueryParams query)
    {
        var (items, totalCount) = await _auditLogRepository.GetPagedAsync(
            skip: query.Skip,
            take: query.PageSize,
            userId: query.UserId,
            resourceType: query.ResourceType,
            resourceId: query.ResourceId,
            action: query.Action,
            fromDate: query.FromDate,
            toDate: query.ToDate,
            organizationId: null, // Could be populated from JWT tenant claim
            orderBy: query.OrderBy ?? "CreatedAt");

        var dtos = items.Select(log => new AuditLogDto
        {
            Id = log.Id,
            UserId = log.UserId,
            UserName = log.User?.UserName,
            UserFullName = log.User?.FullName,
            Action = log.Action,
            ResourceType = log.ResourceType,
            ResourceId = log.ResourceId,
            ResourceName = log.ResourceName,
            Success = log.Success,
            HttpMethod = log.HttpMethod,
            Endpoint = log.Endpoint,
            StatusCode = log.StatusCode,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            DenialReason = log.DenialReason,
            RequiredPermission = log.RequiredPermission,
            OrganizationId = log.OrganizationId,
            CreatedAt = log.CreatedAt
        }).ToList();

        return Ok(PagedResponse<AuditLogDto>.Create(dtos, totalCount, query.PageNumber, query.PageSize));
    }

    /// <summary>
    /// Get audit log by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission("system.audit_logs")]
    [ProducesResponseType(typeof(AuditLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditLogDto>> GetById(Guid id)
    {
        var log = await _auditLogRepository.GetByIdAsync(id);
        if (log == null)
        {
            return NotFound(new { message = "Audit log not found" });
        }

        return Ok(new AuditLogDto
        {
            Id = log.Id,
            UserId = log.UserId,
            UserName = log.User?.UserName,
            UserFullName = log.User?.FullName,
            Action = log.Action,
            ResourceType = log.ResourceType,
            ResourceId = log.ResourceId,
            ResourceName = log.ResourceName,
            Success = log.Success,
            HttpMethod = log.HttpMethod,
            Endpoint = log.Endpoint,
            StatusCode = log.StatusCode,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            DenialReason = log.DenialReason,
            RequiredPermission = log.RequiredPermission,
            OrganizationId = log.OrganizationId,
            CreatedAt = log.CreatedAt
        });
    }

    /// <summary>
    /// Get audit logs for a specific resource.
    /// </summary>
    [HttpGet("resource/{resourceType}/{resourceId:guid}")]
    [HasPermission("system.audit_logs")]
    [ProducesResponseType(typeof(List<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AuditLogDto>>> GetByResource(string resourceType, Guid resourceId)
    {
        var logs = await _auditLogRepository.GetByResourceAsync(resourceType, resourceId);

        var dtos = logs.Select(log => new AuditLogDto
        {
            Id = log.Id,
            UserId = log.UserId,
            UserName = log.User?.UserName,
            UserFullName = log.User?.FullName,
            Action = log.Action,
            ResourceType = log.ResourceType,
            ResourceId = log.ResourceId,
            ResourceName = log.ResourceName,
            Success = log.Success,
            HttpMethod = log.HttpMethod,
            Endpoint = log.Endpoint,
            StatusCode = log.StatusCode,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            DenialReason = log.DenialReason,
            RequiredPermission = log.RequiredPermission,
            OrganizationId = log.OrganizationId,
            CreatedAt = log.CreatedAt
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Get audit logs for a specific user.
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    [HasPermission("system.audit_logs")]
    [ProducesResponseType(typeof(List<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AuditLogDto>>> GetByUser(Guid userId, [FromQuery] int limit = 100)
    {
        var logs = await _auditLogRepository.GetByUserAsync(userId, limit);

        var dtos = logs.Select(log => new AuditLogDto
        {
            Id = log.Id,
            UserId = log.UserId,
            UserName = log.User?.UserName,
            UserFullName = log.User?.FullName,
            Action = log.Action,
            ResourceType = log.ResourceType,
            ResourceId = log.ResourceId,
            ResourceName = log.ResourceName,
            Success = log.Success,
            HttpMethod = log.HttpMethod,
            Endpoint = log.Endpoint,
            StatusCode = log.StatusCode,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            DenialReason = log.DenialReason,
            RequiredPermission = log.RequiredPermission,
            OrganizationId = log.OrganizationId,
            CreatedAt = log.CreatedAt
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Get audit log summary statistics.
    /// </summary>
    [HttpGet("summary")]
    [HasPermission("system.audit_logs")]
    [ProducesResponseType(typeof(AuditLogSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogSummaryDto>> GetSummary(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var summary = await _auditLogRepository.GetSummaryAsync(fromDate, toDate);
        return Ok(summary);
    }

    /// <summary>
    /// Get failed audit entries (for security monitoring).
    /// </summary>
    [HttpGet("failed")]
    [HasPermission("system.audit_logs")]
    [ProducesResponseType(typeof(List<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AuditLogDto>>> GetFailedEntries(
        [FromQuery] Guid? userId = null,
        [FromQuery] int hours = 24)
    {
        var since = DateTime.UtcNow.AddHours(-hours);

        List<Models.AuditLog> logs;
        if (userId.HasValue)
        {
            logs = await _auditLogRepository.GetFailedEntriesAsync(userId.Value, since);
        }
        else
        {
            // Get all failed entries - use paged query with success filter
            var (items, _) = await _auditLogRepository.GetPagedAsync(
                skip: 0,
                take: 500,
                fromDate: since);
            logs = items.Where(l => !l.Success).ToList();
        }

        var dtos = logs.Select(log => new AuditLogDto
        {
            Id = log.Id,
            UserId = log.UserId,
            UserName = log.User?.UserName,
            UserFullName = log.User?.FullName,
            Action = log.Action,
            ResourceType = log.ResourceType,
            ResourceId = log.ResourceId,
            ResourceName = log.ResourceName,
            Success = log.Success,
            HttpMethod = log.HttpMethod,
            Endpoint = log.Endpoint,
            StatusCode = log.StatusCode,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            DenialReason = log.DenialReason,
            RequiredPermission = log.RequiredPermission,
            OrganizationId = log.OrganizationId,
            CreatedAt = log.CreatedAt
        }).ToList();

        return Ok(dtos);
    }
}
