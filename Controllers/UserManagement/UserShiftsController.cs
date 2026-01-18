using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Shift;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;

namespace TruLoad.Backend.Controllers.UserManagement;

[ApiController]
[Route("api/v1/user-shifts")]
[Authorize]
public class UserShiftsController : ControllerBase
{
    private readonly IUserShiftRepository _userShiftRepository;
    private readonly ILogger<UserShiftsController> _logger;

    public UserShiftsController(
        IUserShiftRepository userShiftRepository,
        ILogger<UserShiftsController> logger)
    {
        _userShiftRepository = userShiftRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets user shift by ID.
    /// </summary>
    /// <param name="id">User shift ID</param>
    /// <returns>User shift details</returns>
    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:users.read")]
    [ProducesResponseType(typeof(UserShiftDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var userShift = await _userShiftRepository.GetByIdAsync(id);
            if (userShift == null)
            {
                return NotFound($"User shift {id} not found");
            }

            var dto = MapToDto(userShift);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user shift {UserShiftId}", id);
            return StatusCode(500, "An error occurred while retrieving the user shift.");
        }
    }

    /// <summary>
    /// Gets all shifts for a specific user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="activeOnly">Filter to active shifts only</param>
    /// <returns>List of user shifts</returns>
    [HttpGet("user/{userId}")]
    [Authorize(Policy = "Permission:users.read")]
    [ProducesResponseType(typeof(List<UserShiftDto>), 200)]
    public async Task<IActionResult> GetByUserId(Guid userId, [FromQuery] bool activeOnly = true)
    {
        try
        {
            var userShifts = await _userShiftRepository.GetByUserIdAsync(userId, activeOnly);
            var dtos = userShifts.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shifts for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving user shifts.");
        }
    }

    /// <summary>
    /// Gets active shift for a specific user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Active user shift</returns>
    [HttpGet("user/{userId}/active")]
    [Authorize(Policy = "Permission:users.read")]
    [ProducesResponseType(typeof(UserShiftDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetActiveShiftForUser(Guid userId)
    {
        try
        {
            var userShift = await _userShiftRepository.GetActiveShiftForUserAsync(userId);
            if (userShift == null)
            {
                return NotFound($"No active shift found for user {userId}");
            }

            var dto = MapToDto(userShift);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active shift for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving the active shift.");
        }
    }

    /// <summary>
    /// Creates a new user shift assignment.
    /// </summary>
    /// <param name="request">User shift details</param>
    /// <returns>Created user shift</returns>
    [HttpPost]
    [Authorize(Policy = "Permission:users.update")]
    [ProducesResponseType(typeof(UserShiftDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateUserShiftRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate that either WorkShiftId or ShiftRotationId is provided
        if (!request.WorkShiftId.HasValue && !request.ShiftRotationId.HasValue)
        {
            return BadRequest("Either WorkShiftId or ShiftRotationId must be provided.");
        }

        // Validate that both are not provided
        if (request.WorkShiftId.HasValue && request.ShiftRotationId.HasValue)
        {
            return BadRequest("Cannot assign both WorkShift and ShiftRotation. Provide only one.");
        }

        // Validate date range
        if (request.EndsOn.HasValue && request.EndsOn < request.StartsOn)
        {
            return BadRequest("EndsOn date must be after StartsOn date.");
        }

        try
        {
            var userShift = new UserShift
            {
                UserId = request.UserId,
                WorkShiftId = request.WorkShiftId,
                ShiftRotationId = request.ShiftRotationId,
                StartsOn = request.StartsOn,
                EndsOn = request.EndsOn,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _userShiftRepository.CreateAsync(userShift);

            // Reload with navigation properties
            var reloaded = await _userShiftRepository.GetByIdAsync(created.Id);
            var dto = MapToDto(reloaded!);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user shift");
            return StatusCode(500, "An error occurred while creating the user shift.");
        }
    }

    /// <summary>
    /// Updates an existing user shift assignment.
    /// </summary>
    /// <param name="id">User shift ID</param>
    /// <param name="request">Updated details</param>
    /// <returns>Updated user shift</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:users.update")]
    [ProducesResponseType(typeof(UserShiftDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserShiftRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userShift = await _userShiftRepository.GetByIdAsync(id);
            if (userShift == null)
            {
                return NotFound($"User shift {id} not found");
            }

            // Validate that both are not provided
            if (request.WorkShiftId.HasValue && request.ShiftRotationId.HasValue)
            {
                return BadRequest("Cannot assign both WorkShift and ShiftRotation. Provide only one.");
            }

            // Update only provided fields
            if (request.WorkShiftId.HasValue)
            {
                userShift.WorkShiftId = request.WorkShiftId;
                userShift.ShiftRotationId = null;
            }

            if (request.ShiftRotationId.HasValue)
            {
                userShift.ShiftRotationId = request.ShiftRotationId;
                userShift.WorkShiftId = null;
            }

            if (request.StartsOn.HasValue)
                userShift.StartsOn = request.StartsOn.Value;

            if (request.EndsOn.HasValue)
                userShift.EndsOn = request.EndsOn.Value;

            // Validate date range
            if (userShift.EndsOn.HasValue && userShift.EndsOn < userShift.StartsOn)
            {
                return BadRequest("EndsOn date must be after StartsOn date.");
            }

            await _userShiftRepository.UpdateAsync(userShift);

            // Reload with navigation properties
            var reloaded = await _userShiftRepository.GetByIdAsync(id);
            var dto = MapToDto(reloaded!);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user shift {UserShiftId}", id);
            return StatusCode(500, "An error occurred while updating the user shift.");
        }
    }

    /// <summary>
    /// Deletes a user shift assignment.
    /// </summary>
    /// <param name="id">User shift ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "Permission:users.delete")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var userShift = await _userShiftRepository.GetByIdAsync(id);
            if (userShift == null)
            {
                return NotFound($"User shift {id} not found");
            }

            await _userShiftRepository.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user shift {UserShiftId}", id);
            return StatusCode(500, "An error occurred while deleting the user shift.");
        }
    }

    private UserShiftDto MapToDto(UserShift userShift)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isActive = userShift.StartsOn <= today && (userShift.EndsOn == null || userShift.EndsOn > today);

        return new UserShiftDto
        {
            Id = userShift.Id,
            UserId = userShift.UserId,
            UserFullName = userShift.User?.FullName ?? string.Empty,
            UserEmail = userShift.User?.Email ?? string.Empty,
            WorkShiftId = userShift.WorkShiftId,
            WorkShiftName = userShift.WorkShift?.Name,
            ShiftRotationId = userShift.ShiftRotationId,
            ShiftRotationTitle = userShift.ShiftRotation?.Title,
            StartsOn = userShift.StartsOn,
            EndsOn = userShift.EndsOn,
            CreatedAt = userShift.CreatedAt,
            IsActive = isActive
        };
    }
}
