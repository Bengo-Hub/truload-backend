using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.System;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Controllers.System;

/// <summary>
/// Controller for act configuration data (Traffic Act, EAC Act).
/// Provides read access to fee schedules, tolerances, and demerit points per legal framework.
/// </summary>
[ApiController]
[Route("api/v1/acts")]
[Authorize]
public class ActConfigurationController : ControllerBase
{
    private readonly IActConfigurationService _actConfigService;
    private readonly ILogger<ActConfigurationController> _logger;

    public ActConfigurationController(
        IActConfigurationService actConfigService,
        ILogger<ActConfigurationController> logger)
    {
        _actConfigService = actConfigService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Get all act definitions.
    /// </summary>
    [HttpGet]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(List<ActDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ActDefinitionDto>>> GetAllActs(CancellationToken ct)
    {
        var acts = await _actConfigService.GetAllActsAsync(ct);
        return Ok(acts);
    }

    /// <summary>
    /// Get a single act definition by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(ActDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActDefinitionDto>> GetActById(Guid id, CancellationToken ct)
    {
        var act = await _actConfigService.GetActByIdAsync(id, ct);
        if (act == null)
            return NotFound(new { message = $"Act definition with ID '{id}' not found" });
        return Ok(act);
    }

    /// <summary>
    /// Get the full configuration for an act (fee schedules, tolerances, demerit points).
    /// </summary>
    [HttpGet("{id:guid}/configuration")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(ActConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActConfigurationDto>> GetActConfiguration(Guid id, CancellationToken ct)
    {
        var config = await _actConfigService.GetActConfigurationAsync(id, ct);
        if (config == null)
            return NotFound(new { message = $"Act configuration for ID '{id}' not found" });
        return Ok(config);
    }

    /// <summary>
    /// Get the current default act definition.
    /// </summary>
    [HttpGet("default")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(ActDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActDefinitionDto>> GetDefaultAct(CancellationToken ct)
    {
        var act = await _actConfigService.GetDefaultActAsync(ct);
        if (act == null)
            return NotFound(new { message = "No default act configured" });
        return Ok(act);
    }

    /// <summary>
    /// Set the default act (updates compliance.default_act_code setting).
    /// </summary>
    [HttpPut("{id:guid}/set-default")]
    [HasPermission("config.update")]
    [ProducesResponseType(typeof(ActDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActDefinitionDto>> SetDefaultAct(Guid id, CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            var act = await _actConfigService.SetDefaultActAsync(id, userId, ct);
            return Ok(act);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get fee schedules filtered by legal framework.
    /// </summary>
    [HttpGet("fee-schedules")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(List<AxleFeeScheduleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AxleFeeScheduleDto>>> GetFeeSchedules(
        [FromQuery] string legalFramework, CancellationToken ct)
    {
        var schedules = await _actConfigService.GetFeeSchedulesAsync(legalFramework, ct);
        return Ok(schedules);
    }

    /// <summary>
    /// Get axle-type-specific overload fee schedules filtered by legal framework.
    /// </summary>
    [HttpGet("axle-type-fees")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(List<AxleTypeOverloadFeeScheduleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AxleTypeOverloadFeeScheduleDto>>> GetAxleTypeFeeSchedules(
        [FromQuery] string legalFramework, CancellationToken ct)
    {
        var schedules = await _actConfigService.GetAxleTypeFeeSchedulesAsync(legalFramework, ct);
        return Ok(schedules);
    }

    /// <summary>
    /// Get tolerance settings filtered by legal framework.
    /// </summary>
    [HttpGet("tolerances")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(List<ToleranceSettingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ToleranceSettingDto>>> GetToleranceSettings(
        [FromQuery] string legalFramework, CancellationToken ct)
    {
        var tolerances = await _actConfigService.GetToleranceSettingsAsync(legalFramework, ct);
        return Ok(tolerances);
    }

    /// <summary>
    /// Get demerit point schedules filtered by legal framework.
    /// </summary>
    [HttpGet("demerit-points")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(List<DemeritPointScheduleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DemeritPointScheduleDto>>> GetDemeritPointSchedules(
        [FromQuery] string legalFramework, CancellationToken ct)
    {
        var schedules = await _actConfigService.GetDemeritPointSchedulesAsync(legalFramework, ct);
        return Ok(schedules);
    }

    /// <summary>
    /// Get a summary of the acts configuration for dashboard display.
    /// </summary>
    [HttpGet("summary")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(ActConfigurationSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ActConfigurationSummaryDto>> GetSummary(CancellationToken ct)
    {
        var summary = await _actConfigService.GetSummaryAsync(ct);
        return Ok(summary);
    }
}
