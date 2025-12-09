using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.Shift;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Interfaces;

namespace TruLoad.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class WorkShiftsController : ControllerBase
{
    private readonly IWorkShiftRepository _workShiftRepository;
    private readonly ILogger<WorkShiftsController> _logger;

    public WorkShiftsController(IWorkShiftRepository workShiftRepository, ILogger<WorkShiftsController> logger)
    {
        _workShiftRepository = workShiftRepository;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkShiftDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var shift = await _workShiftRepository.GetByIdWithSchedulesAsync(id, cancellationToken);
        if (shift == null)
        {
            return NotFound(new { message = "Work shift not found" });
        }

        return Ok(MapToDto(shift));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<WorkShiftDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<WorkShiftDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var shifts = await _workShiftRepository.GetAllWithSchedulesAsync(includeInactive, cancellationToken);
        return Ok(shifts.Select(MapToDto));
    }

    [HttpPost]
    [HasPermission("workshift.create")]
    [ProducesResponseType(typeof(WorkShiftDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WorkShiftDto>> Create([FromBody] CreateWorkShiftRequest request, CancellationToken cancellationToken)
    {
        // Check if name already exists
        if (await _workShiftRepository.NameExistsAsync(request.Name, cancellationToken: cancellationToken))
        {
            return BadRequest(new { message = "Work shift name already exists" });
        }

        var shift = new WorkShift
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            TotalHoursPerWeek = request.TotalHoursPerWeek,
            GraceMinutes = request.GraceMinutes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WorkShiftSchedules = request.Schedules.Select(s => new WorkShiftSchedule
            {
                Id = Guid.NewGuid(),
                Day = s.Day,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                BreakHours = s.BreakHours,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList()
        };

        var created = await _workShiftRepository.CreateAsync(shift, cancellationToken);
        _logger.LogInformation("Work shift created: {ShiftId}, Name: {Name}, Schedules: {Count}", 
            created.Id, created.Name, created.WorkShiftSchedules.Count);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    [HttpPut("{id:guid}")]
    [HasPermission("workshift.update")]
    [ProducesResponseType(typeof(WorkShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WorkShiftDto>> Update(Guid id, [FromBody] UpdateWorkShiftRequest request, CancellationToken cancellationToken)
    {
        var shift = await _workShiftRepository.GetByIdWithSchedulesAsync(id, cancellationToken);
        if (shift == null)
        {
            return NotFound(new { message = "Work shift not found" });
        }

        if (request.Name != null && request.Name != shift.Name)
        {
            if (await _workShiftRepository.NameExistsAsync(request.Name, id, cancellationToken))
            {
                return BadRequest(new { message = "Work shift name already exists" });
            }
            shift.Name = request.Name;
        }

        if (request.Description != null) shift.Description = request.Description;
        if (request.TotalHoursPerWeek.HasValue) shift.TotalHoursPerWeek = request.TotalHoursPerWeek.Value;
        if (request.GraceMinutes.HasValue) shift.GraceMinutes = request.GraceMinutes.Value;
        if (request.IsActive.HasValue) shift.IsActive = request.IsActive.Value;

        var updated = await _workShiftRepository.UpdateAsync(shift, cancellationToken);
        _logger.LogInformation("Work shift updated: {ShiftId}", updated.Id);

        return Ok(MapToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("workshift.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var shift = await _workShiftRepository.GetByIdAsync(id, cancellationToken);
        if (shift == null)
        {
            return NotFound(new { message = "Work shift not found" });
        }

        await _workShiftRepository.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Work shift deleted: {ShiftId}", id);

        return NoContent();
    }

    private static WorkShiftDto MapToDto(WorkShift shift)
    {
        return new WorkShiftDto
        {
            Id = shift.Id,
            Name = shift.Name,
            Description = shift.Description,
            TotalHoursPerWeek = shift.TotalHoursPerWeek,
            GraceMinutes = shift.GraceMinutes,
            IsActive = shift.IsActive,
            CreatedAt = shift.CreatedAt,
            UpdatedAt = shift.UpdatedAt,
            Schedules = shift.WorkShiftSchedules?.Select(s => new WorkShiftScheduleDto
            {
                Id = s.Id,
                Day = s.Day,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                BreakHours = s.BreakHours
            }).ToList() ?? new List<WorkShiftScheduleDto>()
        };
    }
}




