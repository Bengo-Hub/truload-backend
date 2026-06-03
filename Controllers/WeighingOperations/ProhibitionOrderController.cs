using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Data;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Controllers.WeighingOperations;

/// <summary>
/// Serves prohibition-order documents. The order itself is auto-created during the weighing
/// compliance flow for beyond-tolerance overloads; this exposes its PDF for preview/download
/// and links it on the case register.
/// </summary>
[ApiController]
[Route("api/v1/weighing/prohibition-orders")]
[Authorize]
public class ProhibitionOrderController : ControllerBase
{
    private readonly TruLoadDbContext _context;
    private readonly IPdfService _pdfService;
    private readonly ILogger<ProhibitionOrderController> _logger;

    public ProhibitionOrderController(
        TruLoadDbContext context,
        IPdfService pdfService,
        ILogger<ProhibitionOrderController> logger)
    {
        _context = context;
        _pdfService = pdfService;
        _logger = logger;
    }

    /// <summary>Downloads the prohibition order PDF.</summary>
    /// <remarks>
    /// Viewable by users who can read weighing transactions OR case-register entries — the
    /// prohibition is a weighing-domain document but is surfaced/linked from the case register,
    /// so case officers (case.read) can open it without also needing weighing.read.
    /// </remarks>
    [HttpGet("{id}/pdf")]
    [HasAnyPermission("weighing.read", "case.read")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPdf(Guid id)
    {
        try
        {
            var order = await _context.ProhibitionOrders
                .Include(o => o.Weighing)
                .Include(o => o.IssuedBy)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound($"Prohibition order {id} not found");

            var pdfBytes = await _pdfService.GenerateProhibitionOrderAsync(order);
            return File(pdfBytes, "application/pdf", $"ProhibitionOrder_{order.ProhibitionNo}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating prohibition order PDF for {ProhibitionId}", id);
            return StatusCode(500, "An error occurred while generating the prohibition order PDF.");
        }
    }
}
