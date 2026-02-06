using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PermitsController : ControllerBase
{
    private readonly IPermitRepository _permitRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PermitsController> _logger;

    public PermitsController(
        IPermitRepository permitRepository,
        ITenantContext tenantContext,
        ILogger<PermitsController> logger)
    {
        _permitRepository = permitRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Get permit by ID with vehicle and permit type details
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PermitDto>> GetPermit(Guid id)
    {
        var permit = await _permitRepository.GetByIdAsync(id);

        if (permit == null)
        {
            return NotFound(new { message = "Permit not found" });
        }

        var permitDto = MapToDto(permit);
        return Ok(permitDto);
    }

    /// <summary>
    /// Get all permits for a specific vehicle
    /// </summary>
    [HttpGet("vehicle/{vehicleId}")]
    public async Task<ActionResult<IEnumerable<PermitDto>>> GetPermitsByVehicle(Guid vehicleId)
    {
        var permits = await _permitRepository.GetByVehicleIdAsync(vehicleId);
        var permitDtos = permits.Select(MapToDto);
        return Ok(permitDtos);
    }

    /// <summary>
    /// Get active permit for a specific vehicle
    /// </summary>
    [HttpGet("vehicle/{vehicleId}/active")]
    public async Task<ActionResult<PermitDto>> GetActivePermitForVehicle(Guid vehicleId)
    {
        var permit = await _permitRepository.GetActivePermitForVehicleAsync(vehicleId);

        if (permit == null)
        {
            return NotFound(new { message = "No active permit found for this vehicle" });
        }

        var permitDto = MapToDto(permit);
        return Ok(permitDto);
    }

    /// <summary>
    /// Create new permit
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PermitDto>> CreatePermit([FromBody] CreatePermitRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate date range
        if (request.ValidTo <= request.ValidFrom)
        {
            return BadRequest(new { message = "Valid to date must be after valid from date" });
        }

        var permit = new Permit
        {
            Id = Guid.NewGuid(),
            PermitNo = request.PermitNo,
            VehicleId = request.VehicleId,
            PermitTypeId = request.PermitTypeId,
            AxleExtensionKg = request.AxleExtensionKg,
            GvwExtensionKg = request.GvwExtensionKg,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            IssuingAuthority = request.IssuingAuthority,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            var createdPermit = await _permitRepository.CreateAsync(permit);
            var createdPermitDto = await _permitRepository.GetByIdAsync(createdPermit.Id);
            
            return CreatedAtAction(
                nameof(GetPermit),
                new { id = createdPermit.Id },
                MapToDto(createdPermitDto!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating permit");
            return StatusCode(500, new { message = "An error occurred while creating the permit" });
        }
    }

    /// <summary>
    /// Update existing permit
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<PermitDto>> UpdatePermit(Guid id, [FromBody] UpdatePermitRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var permit = await _permitRepository.GetByIdAsync(id);
        if (permit == null)
        {
            return NotFound(new { message = "Permit not found" });
        }

        // Update only provided fields
        if (!string.IsNullOrWhiteSpace(request.PermitNo))
            permit.PermitNo = request.PermitNo;

        if (request.AxleExtensionKg.HasValue)
            permit.AxleExtensionKg = request.AxleExtensionKg;

        if (request.GvwExtensionKg.HasValue)
            permit.GvwExtensionKg = request.GvwExtensionKg;

        if (request.ValidFrom.HasValue)
            permit.ValidFrom = request.ValidFrom.Value;

        if (request.ValidTo.HasValue)
            permit.ValidTo = request.ValidTo.Value;

        if (!string.IsNullOrWhiteSpace(request.IssuingAuthority))
            permit.IssuingAuthority = request.IssuingAuthority;

        if (!string.IsNullOrWhiteSpace(request.Status))
            permit.Status = request.Status;

        // Validate date range if both dates are present
        if (permit.ValidTo <= permit.ValidFrom)
        {
            return BadRequest(new { message = "Valid to date must be after valid from date" });
        }

        try
        {
            await _permitRepository.UpdateAsync(permit);
            var updatedPermit = await _permitRepository.GetByIdAsync(id);
            return Ok(MapToDto(updatedPermit!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permit {PermitId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the permit" });
        }
    }

    /// <summary>
    /// Revoke a permit
    /// </summary>
    [HttpPost("{id}/revoke")]
    public async Task<ActionResult<PermitDto>> RevokePermit(Guid id)
    {
        var permit = await _permitRepository.GetByIdAsync(id);
        if (permit == null)
        {
            return NotFound(new { message = "Permit not found" });
        }

        permit.Status = "revoked";

        try
        {
            await _permitRepository.UpdateAsync(permit);
            var updatedPermit = await _permitRepository.GetByIdAsync(id);
            return Ok(MapToDto(updatedPermit!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking permit {PermitId}", id);
            return StatusCode(500, new { message = "An error occurred while revoking the permit" });
        }
    }

    private static PermitDto MapToDto(Permit permit)
    {
        return new PermitDto
        {
            Id = permit.Id,
            PermitNo = permit.PermitNo,
            VehicleId = permit.VehicleId,
            VehicleRegNo = permit.Vehicle?.RegNo ?? string.Empty,
            PermitTypeId = permit.PermitTypeId,
            PermitTypeName = permit.PermitType?.Name ?? string.Empty,
            AxleExtensionKg = permit.AxleExtensionKg,
            GvwExtensionKg = permit.GvwExtensionKg,
            ValidFrom = permit.ValidFrom,
            ValidTo = permit.ValidTo,
            IssuingAuthority = permit.IssuingAuthority ?? string.Empty,
            Status = permit.Status,
            CreatedAt = permit.CreatedAt
        };
    }
}
