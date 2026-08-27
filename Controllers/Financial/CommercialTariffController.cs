using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Services.Interfaces.Financial;

namespace TruLoad.Backend.Controllers.Financial;

/// <summary>
/// CRUD for commercial weighing tariff rules — the rate engine CreateCommercialInvoiceAsync
/// resolves against (transporter contract rate > vehicle/axle/weight bracket rule >
/// Organization.CommercialWeighingFeeKes fallback).
/// </summary>
[ApiController]
[Route("api/v1/commercial-weighing/tariffs")]
[Authorize]
public class CommercialTariffController : ControllerBase
{
    private readonly ICommercialTariffService _tariffService;
    private readonly ILogger<CommercialTariffController> _logger;

    public CommercialTariffController(
        ICommercialTariffService tariffService,
        ILogger<CommercialTariffController> logger)
    {
        _tariffService = tariffService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission("billing.tariffs.view")]
    [ProducesResponseType(typeof(List<CommercialTariffRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CommercialTariffRuleDto>>> GetAll(CancellationToken ct)
    {
        return Ok(await _tariffService.GetAllAsync(ct));
    }

    [HttpGet("{id:guid}")]
    [HasPermission("billing.tariffs.view")]
    [ProducesResponseType(typeof(CommercialTariffRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommercialTariffRuleDto>> GetById(Guid id, CancellationToken ct)
    {
        var rule = await _tariffService.GetByIdAsync(id, ct);
        if (rule == null)
            return NotFound(new { message = $"Tariff rule '{id}' not found" });
        return Ok(rule);
    }

    [HttpPost]
    [HasPermission("billing.tariffs.manage")]
    [ProducesResponseType(typeof(CommercialTariffRuleDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CommercialTariffRuleDto>> Create([FromBody] CreateCommercialTariffRuleRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var rule = await _tariffService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = rule.Id }, rule);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("billing.tariffs.manage")]
    [ProducesResponseType(typeof(CommercialTariffRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommercialTariffRuleDto>> Update(Guid id, [FromBody] UpdateCommercialTariffRuleRequest request, CancellationToken ct)
    {
        var rule = await _tariffService.UpdateAsync(id, request, ct);
        if (rule == null)
            return NotFound(new { message = $"Tariff rule '{id}' not found" });
        return Ok(rule);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("billing.tariffs.manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _tariffService.DeleteAsync(id, ct);
        if (!deleted)
            return NotFound(new { message = $"Tariff rule '{id}' not found" });
        return NoContent();
    }
}
