using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models.Reporting;

namespace TruLoad.Backend.Controllers.Reporting;

/// <summary>
/// CRUD for structured custom-report-builder saved configs (column selection + chart option +
/// filter overrides, scoped to the caller's tenant). Separate from
/// <see cref="Shared.ScheduledReportController"/>, which is schedule+email delivery, not "remember
/// my report layout".
/// </summary>
[ApiController]
[Route("api/v1/reports/configs")]
[Authorize]
public class ReportConfigController : ControllerBase
{
    private readonly TruLoadDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ReportConfigController(TruLoadDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    /// <summary>Lists saved configs for the caller's tenant, optionally filtered by module/reportType.</summary>
    [HttpGet]
    [HasPermission("analytics.read")]
    public async Task<ActionResult<List<SavedReportConfigDto>>> List(
        [FromQuery] string? module = null, [FromQuery] string? reportType = null, CancellationToken ct = default)
    {
        var query = _db.SavedReportConfigs.AsNoTracking().Where(c => c.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(module))
            query = query.Where(c => c.Module == module);
        if (!string.IsNullOrWhiteSpace(reportType))
            query = query.Where(c => c.ReportType == reportType);

        var items = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        return Ok(items.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    [HasPermission("analytics.read")]
    public async Task<ActionResult<SavedReportConfigDto>> Get(Guid id, CancellationToken ct)
    {
        var item = await _db.SavedReportConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);
        return item == null ? NotFound() : Ok(Map(item));
    }

    [HttpPost]
    [HasPermission("analytics.read")]
    public async Task<ActionResult<SavedReportConfigDto>> Create([FromBody] SaveReportConfigRequest request, CancellationToken ct)
    {
        var userId = ResolveUserId();

        var entity = new SavedReportConfig
        {
            OrganizationId = _tenantContext.OrganizationId,
            Name = request.Name,
            Module = request.Module,
            ReportType = request.ReportType,
            ColumnsJson = JsonSerializer.Serialize(request.Columns ?? []),
            ChartType = request.ChartType,
            FiltersJson = request.FiltersJson,
            IsDefault = request.IsDefault,
            CreatedByUserId = userId
        };

        // Only one default per (org, module, reportType) - demote any existing default rather
        // than allowing an ambiguous "which one is the default" state.
        if (entity.IsDefault)
            await ClearExistingDefaultAsync(entity.Module, entity.ReportType, ct);

        _db.SavedReportConfigs.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, Map(entity));
    }

    [HttpPut("{id:guid}")]
    [HasPermission("analytics.read")]
    public async Task<ActionResult<SavedReportConfigDto>> Update(Guid id, [FromBody] SaveReportConfigRequest request, CancellationToken ct)
    {
        var entity = await _db.SavedReportConfigs.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);
        if (entity == null) return NotFound();

        entity.Name = request.Name;
        entity.ColumnsJson = JsonSerializer.Serialize(request.Columns ?? []);
        entity.ChartType = request.ChartType;
        entity.FiltersJson = request.FiltersJson;

        if (request.IsDefault && !entity.IsDefault)
            await ClearExistingDefaultAsync(entity.Module, entity.ReportType, ct);
        entity.IsDefault = request.IsDefault;

        await _db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("analytics.read")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await _db.SavedReportConfigs.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);
        if (entity == null) return NotFound();

        entity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task ClearExistingDefaultAsync(string module, string reportType, CancellationToken ct)
    {
        var existingDefaults = await _db.SavedReportConfigs
            .Where(c => c.Module == module && c.ReportType == reportType && c.IsDefault && c.DeletedAt == null)
            .ToListAsync(ct);
        foreach (var d in existingDefaults)
            d.IsDefault = false;
    }

    private Guid ResolveUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    private static SavedReportConfigDto Map(SavedReportConfig c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Module = c.Module,
        ReportType = c.ReportType,
        Columns = string.IsNullOrWhiteSpace(c.ColumnsJson)
            ? []
            : JsonSerializer.Deserialize<string[]>(c.ColumnsJson) ?? [],
        ChartType = c.ChartType,
        FiltersJson = c.FiltersJson,
        IsDefault = c.IsDefault,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
