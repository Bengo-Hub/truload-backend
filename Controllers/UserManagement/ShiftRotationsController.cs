using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Shift;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;

namespace TruLoad.Backend.Controllers.UserManagement;

[ApiController]
[Route("api/v1/shift-rotations")]
[Authorize]
public class ShiftRotationsController : ControllerBase
{
    private readonly IShiftRotationRepository _shiftRotationRepository;
    private readonly IRotationShiftRepository _rotationShiftRepository;
    private readonly ILogger<ShiftRotationsController> _logger;

    public ShiftRotationsController(
        IShiftRotationRepository shiftRotationRepository,
        IRotationShiftRepository rotationShiftRepository,
        ILogger<ShiftRotationsController> logger)
    {
        _shiftRotationRepository = shiftRotationRepository;
        _rotationShiftRepository = rotationShiftRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all shift rotations.
    /// </summary>
    /// <param name="includeInactive">Include inactive rotations</param>
    /// <returns>List of shift rotations</returns>
    [HttpGet]
    [Authorize(Policy = "Permission:users.read")]
    [ProducesResponseType(typeof(List<ShiftRotationDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        try
        {
            var rotations = await _shiftRotationRepository.GetAllWithShiftsAsync(includeInactive);
            var dtos = rotations.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shift rotations");
            return StatusCode(500, "An error occurred while retrieving shift rotations.");
        }
    }

    /// <summary>
    /// Gets shift rotation by ID.
    /// </summary>
    /// <param name="id">Shift rotation ID</param>
    /// <returns>Shift rotation details</returns>
    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:users.read")]
    [ProducesResponseType(typeof(ShiftRotationDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var rotation = await _shiftRotationRepository.GetByIdWithShiftsAsync(id);
            if (rotation == null)
            {
                return NotFound($"Shift rotation {id} not found");
            }

            var dto = MapToDto(rotation);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shift rotation {RotationId}", id);
            return StatusCode(500, "An error occurred while retrieving the shift rotation.");
        }
    }

    /// <summary>
    /// Creates a new shift rotation with rotation shifts.
    /// </summary>
    /// <param name="request">Shift rotation details</param>
    /// <returns>Created shift rotation</returns>
    [HttpPost]
    [Authorize(Policy = "Permission:users.create")]
    [ProducesResponseType(typeof(ShiftRotationDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateShiftRotationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate title uniqueness
        if (await _shiftRotationRepository.TitleExistsAsync(request.Title))
        {
            return BadRequest($"Shift rotation with title '{request.Title}' already exists.");
        }

        // Validate run_unit and break_unit
        var validUnits = new[] { "Days", "Weeks", "Months" };
        if (!validUnits.Contains(request.RunUnit))
        {
            return BadRequest($"Invalid RunUnit. Must be one of: {string.Join(", ", validUnits)}");
        }

        var validBreakUnits = new[] { "Day", "Week", "Month" };
        if (!validBreakUnits.Contains(request.BreakUnit))
        {
            return BadRequest($"Invalid BreakUnit. Must be one of: {string.Join(", ", validBreakUnits)}");
        }

        // Validate rotation shifts sequence order
        if (request.RotationShifts.Any())
        {
            var sequenceOrders = request.RotationShifts.Select(rs => rs.SequenceOrder).ToList();
            if (sequenceOrders.Distinct().Count() != sequenceOrders.Count)
            {
                return BadRequest("Sequence orders must be unique for all rotation shifts.");
            }
        }

        try
        {
            var shiftRotation = new ShiftRotation
            {
                Title = request.Title,
                RunDuration = request.RunDuration,
                RunUnit = request.RunUnit,
                BreakDuration = request.BreakDuration,
                BreakUnit = request.BreakUnit,
                IsActive = true
            };

            var created = await _shiftRotationRepository.CreateAsync(shiftRotation);

            // Add rotation shifts
            foreach (var rsRequest in request.RotationShifts)
            {
                var rotationShift = new RotationShift
                {
                    RotationId = created.Id,
                    WorkShiftId = rsRequest.WorkShiftId,
                    SequenceOrder = rsRequest.SequenceOrder
                };

                await _rotationShiftRepository.CreateAsync(rotationShift);
            }

            // Reload with navigation properties
            var reloaded = await _shiftRotationRepository.GetByIdWithShiftsAsync(created.Id);
            var dto = MapToDto(reloaded!);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating shift rotation");
            return StatusCode(500, "An error occurred while creating the shift rotation.");
        }
    }

    /// <summary>
    /// Updates an existing shift rotation.
    /// </summary>
    /// <param name="id">Shift rotation ID</param>
    /// <param name="request">Updated details</param>
    /// <returns>Updated shift rotation</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:users.update")]
    [ProducesResponseType(typeof(ShiftRotationDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateShiftRotationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var rotation = await _shiftRotationRepository.GetByIdAsync(id);
            if (rotation == null)
            {
                return NotFound($"Shift rotation {id} not found");
            }

            // Validate title uniqueness if changed
            if (!string.IsNullOrWhiteSpace(request.Title) && request.Title != rotation.Title)
            {
                if (await _shiftRotationRepository.TitleExistsAsync(request.Title, id))
                {
                    return BadRequest($"Shift rotation with title '{request.Title}' already exists.");
                }
            }

            // Validate run_unit if changed
            if (!string.IsNullOrWhiteSpace(request.RunUnit))
            {
                var validUnits = new[] { "Days", "Weeks", "Months" };
                if (!validUnits.Contains(request.RunUnit))
                {
                    return BadRequest($"Invalid RunUnit. Must be one of: {string.Join(", ", validUnits)}");
                }
            }

            // Validate break_unit if changed
            if (!string.IsNullOrWhiteSpace(request.BreakUnit))
            {
                var validBreakUnits = new[] { "Day", "Week", "Month" };
                if (!validBreakUnits.Contains(request.BreakUnit))
                {
                    return BadRequest($"Invalid BreakUnit. Must be one of: {string.Join(", ", validBreakUnits)}");
                }
            }

            // Update only provided fields
            if (!string.IsNullOrWhiteSpace(request.Title))
                rotation.Title = request.Title;

            if (request.CurrentActiveShiftId.HasValue)
                rotation.CurrentActiveShiftId = request.CurrentActiveShiftId;

            if (request.RunDuration.HasValue)
                rotation.RunDuration = request.RunDuration.Value;

            if (!string.IsNullOrWhiteSpace(request.RunUnit))
                rotation.RunUnit = request.RunUnit;

            if (request.BreakDuration.HasValue)
                rotation.BreakDuration = request.BreakDuration.Value;

            if (!string.IsNullOrWhiteSpace(request.BreakUnit))
                rotation.BreakUnit = request.BreakUnit;

            if (request.NextChangeDate.HasValue)
                rotation.NextChangeDate = request.NextChangeDate;

            if (request.IsActive.HasValue)
                rotation.IsActive = request.IsActive.Value;

            await _shiftRotationRepository.UpdateAsync(rotation);

            // Reload with navigation properties
            var reloaded = await _shiftRotationRepository.GetByIdWithShiftsAsync(id);
            var dto = MapToDto(reloaded!);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating shift rotation {RotationId}", id);
            return StatusCode(500, "An error occurred while updating the shift rotation.");
        }
    }

    /// <summary>
    /// Updates the rotation shifts for a shift rotation.
    /// </summary>
    /// <param name="id">Shift rotation ID</param>
    /// <param name="request">Updated rotation shifts</param>
    /// <returns>Updated shift rotation</returns>
    [HttpPut("{id}/rotation-shifts")]
    [Authorize(Policy = "Permission:users.update")]
    [ProducesResponseType(typeof(ShiftRotationDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateRotationShifts(Guid id, [FromBody] UpdateRotationShiftsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate sequence orders
        var sequenceOrders = request.RotationShifts.Select(rs => rs.SequenceOrder).ToList();
        if (sequenceOrders.Distinct().Count() != sequenceOrders.Count)
        {
            return BadRequest("Sequence orders must be unique for all rotation shifts.");
        }

        try
        {
            var rotation = await _shiftRotationRepository.GetByIdAsync(id);
            if (rotation == null)
            {
                return NotFound($"Shift rotation {id} not found");
            }

            // Delete existing rotation shifts
            await _rotationShiftRepository.DeleteAllByRotationIdAsync(id);

            // Add new rotation shifts
            foreach (var rsRequest in request.RotationShifts)
            {
                var rotationShift = new RotationShift
                {
                    RotationId = id,
                    WorkShiftId = rsRequest.WorkShiftId,
                    SequenceOrder = rsRequest.SequenceOrder
                };

                await _rotationShiftRepository.CreateAsync(rotationShift);
            }

            // Reload with navigation properties
            var reloaded = await _shiftRotationRepository.GetByIdWithShiftsAsync(id);
            var dto = MapToDto(reloaded!);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rotation shifts for {RotationId}", id);
            return StatusCode(500, "An error occurred while updating rotation shifts.");
        }
    }

    /// <summary>
    /// Deletes a shift rotation.
    /// </summary>
    /// <param name="id">Shift rotation ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "Permission:users.delete")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var rotation = await _shiftRotationRepository.GetByIdAsync(id);
            if (rotation == null)
            {
                return NotFound($"Shift rotation {id} not found");
            }

            await _shiftRotationRepository.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting shift rotation {RotationId}", id);
            return StatusCode(500, "An error occurred while deleting the shift rotation.");
        }
    }

    private ShiftRotationDto MapToDto(ShiftRotation rotation)
    {
        return new ShiftRotationDto
        {
            Id = rotation.Id,
            Title = rotation.Title,
            CurrentActiveShiftId = rotation.CurrentActiveShiftId,
            CurrentActiveShiftName = rotation.CurrentActiveShift?.Name,
            RunDuration = rotation.RunDuration,
            RunUnit = rotation.RunUnit,
            BreakDuration = rotation.BreakDuration,
            BreakUnit = rotation.BreakUnit,
            NextChangeDate = rotation.NextChangeDate,
            IsActive = rotation.IsActive,
            RotationShifts = rotation.RotationShifts?.Select(rs => new RotationShiftDto
            {
                RotationId = rs.RotationId,
                WorkShiftId = rs.WorkShiftId,
                WorkShiftName = rs.WorkShift?.Name ?? string.Empty,
                SequenceOrder = rs.SequenceOrder
            }).OrderBy(rs => rs.SequenceOrder).ToList() ?? new()
        };
    }
}
