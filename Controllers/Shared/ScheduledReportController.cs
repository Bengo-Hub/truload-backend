using System.Security.Claims;
using Cronos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Notifications;
using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Controllers.Shared;

/// <summary>
/// Manages automated scheduled reports — create, list, update, toggle, delete.
/// Reports are generated on a cron schedule and emailed to configured recipients.
/// </summary>
[ApiController]
[Route("api/v1/shared/scheduled-reports")]
[Authorize]
public class ScheduledReportController : ControllerBase
{
    private readonly TruLoadDbContext _db;
    private readonly ILogger<ScheduledReportController> _logger;

    // Available report types exposed to the UI for selection
    private static readonly List<ReportTypeMetaDto> _availableReportTypes = new()
    {
        new() { Module = "weighing",   ReportType = "daily-summary",          DisplayName = "Daily Weighing Summary",          SupportedFormats = ["PDF", "CSV", "XLSX"] },
        new() { Module = "weighing",   ReportType = "weighbridge-register",    DisplayName = "Weighbridge Register",            SupportedFormats = ["PDF", "CSV", "XLSX"] },
        new() { Module = "weighing",   ReportType = "overloads",               DisplayName = "Overload Incidents",              SupportedFormats = ["PDF", "CSV"] },
        new() { Module = "cases",      ReportType = "case-summary",            DisplayName = "Case Register Summary",           SupportedFormats = ["PDF", "CSV"] },
        new() { Module = "cases",      ReportType = "pending-cases",           DisplayName = "Pending Cases",                   SupportedFormats = ["PDF", "CSV"] },
        new() { Module = "financial",  ReportType = "invoice-aging",           DisplayName = "Invoice Aging Report",            SupportedFormats = ["PDF", "XLSX"] },
        new() { Module = "financial",  ReportType = "revenue-summary",         DisplayName = "Revenue Summary",                 SupportedFormats = ["PDF", "XLSX"] },
        new() { Module = "compliance", ReportType = "compliance-overview",     DisplayName = "Compliance Overview",             SupportedFormats = ["PDF"] },
        new() { Module = "commercial", ReportType = "commercial-transactions", DisplayName = "Commercial Transactions",         SupportedFormats = ["PDF", "CSV", "XLSX"] },
        new() { Module = "commercial", ReportType = "commercial-daily",        DisplayName = "Commercial Daily Summary",        SupportedFormats = ["PDF", "CSV"] },
    };

    public ScheduledReportController(TruLoadDbContext db, ILogger<ScheduledReportController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Lists all scheduled reports.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ScheduledReportDto>>> List(CancellationToken ct)
    {
        var items = await _db.ScheduledReports
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return Ok(items.Select(Map).ToList());
    }

    /// <summary>Returns the available report types for UI dropdowns.</summary>
    [HttpGet("report-types")]
    public ActionResult<List<ReportTypeMetaDto>> GetReportTypes() => Ok(_availableReportTypes);

    /// <summary>Gets a single scheduled report by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScheduledReportDto>> Get(Guid id, CancellationToken ct)
    {
        var item = await _db.ScheduledReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (item == null) return NotFound();
        return Ok(Map(item));
    }

    /// <summary>Creates a new scheduled report.</summary>
    [HttpPost]
    public async Task<ActionResult<ScheduledReportDto>> Create([FromBody] CreateScheduledReportRequest request, CancellationToken ct)
    {
        if (!TryParseCron(request.CronSchedule, out var nextRun))
            return BadRequest(new { error = "Invalid cron expression" });

        var entity = new ScheduledReport
        {
            Name = request.Name,
            Module = request.Module,
            ReportType = request.ReportType,
            Format = request.Format,
            CronSchedule = request.CronSchedule,
            ScheduleDescription = request.ScheduleDescription,
            Recipients = request.Recipients,
            ParametersJson = request.ParametersJson,
            NextRunAt = nextRun,
            IsActive = true,
        };

        _db.ScheduledReports.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, Map(entity));
    }

    /// <summary>Updates an existing scheduled report.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ScheduledReportDto>> Update(Guid id, [FromBody] UpdateScheduledReportRequest request, CancellationToken ct)
    {
        var entity = await _db.ScheduledReports.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return NotFound();

        if (!TryParseCron(request.CronSchedule, out var nextRun))
            return BadRequest(new { error = "Invalid cron expression" });

        entity.Name = request.Name;
        entity.Module = request.Module;
        entity.ReportType = request.ReportType;
        entity.Format = request.Format;
        entity.CronSchedule = request.CronSchedule;
        entity.ScheduleDescription = request.ScheduleDescription;
        entity.Recipients = request.Recipients;
        entity.ParametersJson = request.ParametersJson;
        entity.IsActive = request.IsActive;
        entity.NextRunAt = nextRun;

        await _db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    /// <summary>Toggles the active state of a scheduled report.</summary>
    [HttpPatch("{id:guid}/toggle")]
    public async Task<ActionResult> Toggle(Guid id, CancellationToken ct)
    {
        var entity = await _db.ScheduledReports.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return NotFound();

        entity.IsActive = !entity.IsActive;
        if (entity.IsActive && entity.NextRunAt == null)
        {
            TryParseCron(entity.CronSchedule, out var next);
            entity.NextRunAt = next;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { isActive = entity.IsActive });
    }

    /// <summary>Deletes a scheduled report.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await _db.ScheduledReports.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return NotFound();

        _db.ScheduledReports.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static bool TryParseCron(string expression, out DateTime? next)
    {
        next = null;
        try
        {
            var cron = CronExpression.Parse(expression, CronFormat.Standard);
            next = cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ScheduledReportDto Map(ScheduledReport r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Module = r.Module,
        ReportType = r.ReportType,
        Format = r.Format,
        CronSchedule = r.CronSchedule,
        ScheduleDescription = r.ScheduleDescription,
        Recipients = r.Recipients,
        ParametersJson = r.ParametersJson,
        NextRunAt = r.NextRunAt,
        LastRunAt = r.LastRunAt,
        LastRunStatus = r.LastRunStatus,
        LastRunError = r.LastRunError,
        IsActive = r.IsActive,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };
}
