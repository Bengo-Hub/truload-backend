using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Interfaces.Reporting;
using TruLoad.Backend.Authorization.Attributes;

namespace TruLoad.Backend.Controllers.Reporting;

/// <summary>
/// Controller for generating and downloading reports across all modules.
/// Supports PDF and CSV output formats.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportController> _logger;

    public ReportController(
        IReportService reportService,
        ILogger<ReportController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Get the report catalog (available reports per module).
    /// Optionally filter by module name.
    /// </summary>
    [HttpGet("catalog")]
    [HasPermission("analytics.read")]
    [ProducesResponseType(typeof(ReportCatalogResponse), StatusCodes.Status200OK)]
    public ActionResult<ReportCatalogResponse> GetCatalog([FromQuery] string? module = null)
    {
        var catalog = _reportService.GetCatalog(module);
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
    /// <param name="stationId">Optional station filter</param>
    /// <param name="status">Optional status filter</param>
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
        CancellationToken ct = default)
    {
        try
        {
            var filters = new ReportFilterParams
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                StationId = stationId,
                Status = status
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
