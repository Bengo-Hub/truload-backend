using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Services.Interfaces.CaseManagement;
using TruLoad.Backend.Authorization.Attributes;

namespace TruLoad.Backend.Controllers.CaseManagement;

[ApiController]
[Route("api/v1/case/cases")]
[Authorize]
public class CaseRegisterController : ControllerBase
{
    private readonly ICaseRegisterService _caseRegisterService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CaseRegisterController> _logger;

    public CaseRegisterController(
        ICaseRegisterService caseRegisterService,
        ITenantContext tenantContext,
        ILogger<CaseRegisterController> logger)
    {
        _caseRegisterService = caseRegisterService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Get case by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var caseDto = await _caseRegisterService.GetByIdAsync(id);
        if (caseDto == null) return NotFound();
        return Ok(caseDto);
    }

    /// <summary>
    /// Get case by case number
    /// </summary>
    [HttpGet("by-case-no/{caseNo}")]
    public async Task<IActionResult> GetByCaseNo(string caseNo)
    {
        var caseDto = await _caseRegisterService.GetByCaseNoAsync(caseNo);
        if (caseDto == null) return NotFound();
        return Ok(caseDto);
    }

    /// <summary>
    /// Get case by weighing ID
    /// </summary>
    [HttpGet("by-weighing/{weighingId}")]
    public async Task<IActionResult> GetByWeighingId(Guid weighingId)
    {
        var caseDto = await _caseRegisterService.GetByWeighingIdAsync(weighingId);
        if (caseDto == null) return NotFound();
        return Ok(caseDto);
    }

    /// <summary>
    /// Search cases with filters
    /// </summary>
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] CaseSearchCriteria criteria)
    {
        var cases = await _caseRegisterService.SearchCasesAsync(criteria);
        return Ok(cases);
    }

    /// <summary>
    /// Create a new case (manual entry)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCaseRegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found"));

        var created = await _caseRegisterService.CreateCaseAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Create case from weighing violation
    /// </summary>
    [HttpPost("from-weighing/{weighingId}")]
    public async Task<IActionResult> CreateFromWeighing(Guid weighingId)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found"));

        try
        {
            var created = await _caseRegisterService.CreateCaseFromWeighingAsync(weighingId, userId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Create case from prohibition order
    /// </summary>
    [HttpPost("from-prohibition/{prohibitionOrderId}")]
    public async Task<IActionResult> CreateFromProhibition(Guid prohibitionOrderId)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found"));

        try
        {
            var created = await _caseRegisterService.CreateCaseFromProhibitionAsync(prohibitionOrderId, userId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update case details
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCaseRegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found"));

        try
        {
            var updated = await _caseRegisterService.UpdateCaseAsync(id, request, userId);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Close a case with disposition
    /// </summary>
    [HttpPost("{id}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseCaseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found"));

        try
        {
            var closed = await _caseRegisterService.CloseCaseAsync(id, request, userId);
            return Ok(closed);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Escalate case to case manager
    /// </summary>
    [HasPermission("case.escalate")]
    [HttpPost("{id}/escalate")]
    public async Task<IActionResult> EscalateToCaseManager(Guid id, [FromBody] Guid caseManagerId)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found"));

        try
        {
            var escalated = await _caseRegisterService.EscalateToCaseManagerAsync(id, caseManagerId, userId);
            return Ok(escalated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Assign investigating officer
    /// </summary>
    [HasPermission("case.assign")]
    [HttpPost("{id}/assign-io")]
    public async Task<IActionResult> AssignInvestigatingOfficer(Guid id, [FromBody] Guid officerId)
    {
        var assignedById = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found"));

        try
        {
            var assigned = await _caseRegisterService.AssignInvestigatingOfficerAsync(id, officerId, assignedById);
            return Ok(assigned);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Get case statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId)
    {
        var hasGlobalRead = User.HasClaim(c => c.Type == "Permission" && c.Value == "case.read");
        var effectiveStationId = (stationId == null && hasGlobalRead) ? null : (stationId ?? _tenantContext.StationId);
        var stats = await _caseRegisterService.GetCaseStatisticsAsync(dateFrom, dateTo, effectiveStationId);
        return Ok(stats);
    }

    /// <summary>
    /// Get case disposition breakdown for charts
    /// </summary>
    [HttpGet("disposition-breakdown")]
    public async Task<IActionResult> GetDispositionBreakdown(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId)
    {
        try
        {
            var hasGlobalRead = User.HasClaim(c => c.Type == "Permission" && c.Value == "case.read");
            var effectiveStationId = (stationId == null && hasGlobalRead) ? null : (stationId ?? _tenantContext.StationId);
            var criteria = new CaseSearchCriteria
            {
                CreatedFrom = dateFrom,
                CreatedTo = dateTo,
                StationId = effectiveStationId,
                PageSize = 10000
            };
            var result = await _caseRegisterService.SearchCasesAsync(criteria);

            var breakdown = result.Items
                .GroupBy(c => c.CaseStatus ?? "Unknown")
                .Select(g => new { Name = g.Key, Value = g.Count() })
                .ToList();

            return Ok(breakdown);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting disposition breakdown");
            return StatusCode(500, "An error occurred while retrieving disposition breakdown.");
        }
    }

    /// <summary>
    /// Get case trend over time
    /// </summary>
    [HttpGet("trend")]
    public async Task<IActionResult> GetCaseTrend(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId)
    {
        try
        {
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;
            var hasGlobalRead = User.HasClaim(c => c.Type == "Permission" && c.Value == "case.read");
            var effectiveStationId = (stationId == null && hasGlobalRead) ? null : (stationId ?? _tenantContext.StationId);
            var criteria = new CaseSearchCriteria
            {
                CreatedFrom = from,
                CreatedTo = to,
                StationId = effectiveStationId,
                PageSize = 10000
            };
            var result = await _caseRegisterService.SearchCasesAsync(criteria);

            var trend = result.Items
                .GroupBy(c => c.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    name = g.Key.ToString("MMM dd"),
                    opened = g.Count(),
                    closed = g.Count(c => string.Equals(c.CaseStatus, "CLOSED", StringComparison.OrdinalIgnoreCase))
                })
                .ToList();

            return Ok(trend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting case trend");
            return StatusCode(500, "An error occurred while retrieving case trend.");
        }
    }

    /// <summary>
    /// Delete a case
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _caseRegisterService.DeleteCaseAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
