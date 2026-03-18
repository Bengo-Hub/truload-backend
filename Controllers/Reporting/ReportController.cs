using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Interfaces.Reporting;
using TruLoad.Backend.Authorization.Attributes;
using System.Text.Json;
using TruLoad.Backend.Services.Interfaces;

namespace TruLoad.Backend.Controllers.Reporting;

/// <summary>
/// Controller for generating and downloading reports across all modules.
/// Supports PDF, CSV, and Excel (xlsx) output formats.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ICacheService _cache;
    private readonly ILogger<ReportController> _logger;

    public ReportController(
        IReportService reportService,
        ICacheService cache,
        ILogger<ReportController> logger)
    {
        _reportService = reportService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get the report catalog (available reports per module).
    /// Optionally filter by module name.
    /// </summary>
    [HttpGet("catalog")]
    [HasPermission("analytics.read")]
    [ProducesResponseType(typeof(ReportCatalogResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportCatalogResponse>> GetCatalog([FromQuery] string? module = null)
    {
        var cacheKey = $"report_catalog_{module ?? "all"}";
        
        try
        {
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                return Ok(JsonSerializer.Deserialize<ReportCatalogResponse>(cached));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get report catalog from cache");
        }

        var catalog = _reportService.GetCatalog(module);

        try
        {
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(catalog), TimeSpan.FromHours(4));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache report catalog");
        }

        return Ok(catalog);
    }

    /// <summary>
    /// Generate and download a report.
    /// </summary>
    /// <param name="module">Report module (weighing, prosecution, cases, financial, yard, security)</param>
    /// <param name="reportType">Report type ID (e.g. daily-summary, weighbridge-register)</param>
    /// <param name="dateFrom">Optional start date filter</param>
    /// <param name="dateTo">Optional end date filter</param>
    /// <param name="format">Output format: pdf or csv (default: pdf)</param>
    /// <param name="stationId">Optional station filter (GUID)</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="weighingType">Optional weighing type (e.g. multideck, mobile) for weighing reports</param>
    /// <param name="controlStatus">Optional control status (e.g. LEGAL, OVERLOAD) for weighing reports</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("{module}/{reportType}")]
    [HasPermission("analytics.read")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateReport(
        [FromRoute] string module,
        [FromRoute] string reportType,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string format = "pdf",
        [FromQuery] string? stationId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? weighingType = null,
        [FromQuery] string? controlStatus = null,
        CancellationToken ct = default)
    {
        try
        {
            var filters = new ReportFilterParams
            {
                DateFrom = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : null,
                DateTo = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : null,
                StationId = stationId,
                Status = status,
                WeighingType = weighingType,
                ControlStatus = controlStatus
            };

            var result = await _reportService.GenerateAsync(module, reportType, filters, format, ct);

            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid report request: {Module}/{ReportType}", module, reportType);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report: {Module}/{ReportType}", module, reportType);
            return StatusCode(500, new { message = "Failed to generate report" });
        }
    }
}
