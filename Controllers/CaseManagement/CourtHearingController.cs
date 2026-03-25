using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Services.Interfaces.CaseManagement;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Controllers.CaseManagement;

/// <summary>
/// API controller for court hearing management.
/// Supports scheduling, adjournment, and completion of hearings.
/// </summary>
[ApiController]
[Authorize]
public class CourtHearingController : ControllerBase
{
    private readonly ICourtHearingService _courtHearingService;
    private readonly IPdfService _pdfService;
    private readonly ITenantContext _tenantContext;

    public CourtHearingController(
        ICourtHearingService courtHearingService,
        IPdfService pdfService,
        ITenantContext tenantContext)
    {
        _courtHearingService = courtHearingService;
        _pdfService = pdfService;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Get all hearings for a case
    /// </summary>
    [HttpGet("api/v1/cases/{caseId}/hearings")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetByCaseId(Guid caseId, CancellationToken ct)
    {
        var hearings = await _courtHearingService.GetByCaseIdAsync(caseId, ct);
        return Ok(hearings);
    }

    /// <summary>
    /// Get hearing by ID
    /// </summary>
    [HttpGet("api/v1/hearings/{id}")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var hearing = await _courtHearingService.GetByIdAsync(id, ct);
        if (hearing == null) return NotFound();
        return Ok(hearing);
    }

    /// <summary>
    /// Get next scheduled hearing for a case
    /// </summary>
    [HttpGet("api/v1/cases/{caseId}/hearings/next")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetNextScheduled(Guid caseId, CancellationToken ct)
    {
        var hearing = await _courtHearingService.GetNextScheduledAsync(caseId, ct);
        if (hearing == null) return NotFound("No upcoming hearings scheduled");
        return Ok(hearing);
    }

    /// <summary>
    /// Get hearings by court within date range
    /// </summary>
    [HttpGet("api/v1/courts/{courtId}/hearings")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetByCourtId(
        Guid courtId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var from = fromDate ?? DateTime.UtcNow.Date;
        var to = toDate ?? DateTime.UtcNow.Date.AddMonths(1);

        var hearings = await _courtHearingService.GetByCourtAsync(courtId, from, to, ct);
        return Ok(hearings);
    }

    /// <summary>
    /// Search hearings with filters
    /// </summary>
    [HttpPost("api/v1/hearings/search")]
    [HasPermission("case.read")]
    public async Task<IActionResult> Search([FromBody] CourtHearingSearchCriteria criteria, CancellationToken ct)
    {
        var hearings = await _courtHearingService.SearchAsync(criteria, ct);
        return Ok(hearings);
    }

    /// <summary>
    /// Schedule a new court hearing for a case
    /// </summary>
    [HttpPost("api/v1/cases/{caseId}/hearings")]
    [HasPermission("case.court_hearing")]
    public async Task<IActionResult> ScheduleHearing(
        Guid caseId,
        [FromBody] CreateCourtHearingRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var hearing = await _courtHearingService.ScheduleHearingAsync(caseId, request, userId, ct);
            return CreatedAtAction(nameof(GetById), new { id = hearing.Id }, hearing);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update an existing hearing
    /// </summary>
    [HttpPut("api/v1/hearings/{id}")]
    [HasPermission("case.court_hearing")]
    public async Task<IActionResult> UpdateHearing(
        Guid id,
        [FromBody] UpdateCourtHearingRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var hearing = await _courtHearingService.UpdateHearingAsync(id, request, userId, ct);
            return Ok(hearing);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Adjourn a hearing with reason and next date
    /// </summary>
    [HttpPost("api/v1/hearings/{id}/adjourn")]
    [HasPermission("case.court_hearing")]
    public async Task<IActionResult> AdjournHearing(
        Guid id,
        [FromBody] AdjournHearingRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var hearing = await _courtHearingService.AdjournHearingAsync(id, request, userId, ct);
            return Ok(hearing);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Complete a hearing with outcome
    /// </summary>
    [HttpPost("api/v1/hearings/{id}/complete")]
    [HasPermission("case.court_hearing")]
    public async Task<IActionResult> CompleteHearing(
        Guid id,
        [FromBody] CompleteHearingRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var hearing = await _courtHearingService.CompleteHearingAsync(id, request, userId, ct);
            return Ok(hearing);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Delete a hearing (soft delete)
    /// </summary>
    [HttpDelete("api/v1/hearings/{id}")]
    [HasPermission("case.court_hearing")]
    public async Task<IActionResult> DeleteHearing(Guid id, CancellationToken ct)
    {
        var deleted = await _courtHearingService.DeleteHearingAsync(id, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Get hearing statistics for dashboard
    /// </summary>
    [HttpGet("api/v1/hearings/statistics")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
        var effectiveStationId = (stationId == null && isHqOrAdmin) ? null : (stationId ?? _tenantContext.StationId);
        var stats = await _courtHearingService.GetHearingStatisticsAsync(dateFrom, dateTo, effectiveStationId, ct);
        return Ok(stats);
    }

    /// <summary>
    /// Get hearing outcomes breakdown for charts
    /// </summary>
    [HttpGet("api/v1/hearings/outcomes")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetOutcomes(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
        var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

        var criteria = new CourtHearingSearchCriteria
        {
            HearingDateFrom = from,
            HearingDateTo = to,
            PageSize = 10000
        };
        var hearings = await _courtHearingService.SearchAsync(criteria, ct);
        
        var outcomes = hearings
            .Where(h => !string.IsNullOrEmpty(h.HearingOutcomeName))
            .GroupBy(h => h.HearingOutcomeName ?? "Unknown")
            .Select(g => new { Name = g.Key, Value = g.Count() })
            .ToList();
        
        return Ok(outcomes);
    }

    /// <summary>
    /// Download court minutes PDF for a hearing
    /// </summary>
    [HttpGet("api/v1/hearings/{id}/minutes")]
    [HasPermission("case.read")]
    public async Task<IActionResult> DownloadCourtMinutes(Guid id, CancellationToken ct)
    {
        try
        {
            var pdfBytes = await _pdfService.GenerateCourtMinutesAsync(id, ct);
            var hearing = await _courtHearingService.GetByIdAsync(id, ct);
            var fileName = $"CourtMinutes_{hearing?.CaseNo ?? id.ToString()}_{hearing?.HearingDate:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("User ID not found in claims");
        return Guid.Parse(userIdClaim);
    }
}
