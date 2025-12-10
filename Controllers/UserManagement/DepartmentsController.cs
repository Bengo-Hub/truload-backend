using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.User;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;

namespace TruLoad.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<DepartmentsController> _logger;

    public DepartmentsController(IDepartmentRepository departmentRepository, ILogger<DepartmentsController> logger)
    {
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dept = await _departmentRepository.GetByIdAsync(id, cancellationToken);
        if (dept == null)
        {
            return NotFound(new { message = "Department not found" });
        }

        return Ok(MapToDto(dept));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var depts = await _departmentRepository.GetAllAsync(includeInactive, cancellationToken);
        return Ok(depts.Select(MapToDto));
    }

    [HttpGet("organization/{organizationId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetByOrganization(Guid organizationId, CancellationToken cancellationToken)
    {
        var depts = await _departmentRepository.GetByOrganizationIdAsync(organizationId, cancellationToken);
        return Ok(depts.Select(MapToDto));
    }

    [HttpPost]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DepartmentDto>> Create([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        // Check if code already exists
        if (await _departmentRepository.CodeExistsAsync(request.Code, cancellationToken: cancellationToken))
        {
            return BadRequest(new { message = "Department code already exists" });
        }

        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            OrganizationId = request.OrganizationId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _departmentRepository.CreateAsync(dept, cancellationToken);
        _logger.LogInformation("Department created: {DeptId}, Code: {Code}", created.Id, created.Code);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> Update(Guid id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var dept = await _departmentRepository.GetByIdAsync(id, cancellationToken);
        if (dept == null)
        {
            return NotFound(new { message = "Department not found" });
        }

        if (request.Code != null && request.Code != dept.Code)
        {
            if (await _departmentRepository.CodeExistsAsync(request.Code, id, cancellationToken))
            {
                return BadRequest(new { message = "Department code already exists" });
            }
            dept.Code = request.Code;
        }

        if (request.Name != null) dept.Name = request.Name;
        if (request.Description != null) dept.Description = request.Description;
        if (request.IsActive.HasValue) dept.IsActive = request.IsActive.Value;

        var updated = await _departmentRepository.UpdateAsync(dept, cancellationToken);
        _logger.LogInformation("Department updated: {DeptId}", updated.Id);

        return Ok(MapToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var dept = await _departmentRepository.GetByIdAsync(id, cancellationToken);
        if (dept == null)
        {
            return NotFound(new { message = "Department not found" });
        }

        await _departmentRepository.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Department deleted: {DeptId}", id);

        return NoContent();
    }

    private static DepartmentDto MapToDto(Department dept)
    {
        return new DepartmentDto
        {
            Id = dept.Id,
            Code = dept.Code,
            Name = dept.Name,
            Description = dept.Description,
            OrganizationId = dept.OrganizationId,
            OrganizationName = dept.Organization?.Name,
            IsActive = dept.IsActive,
            CreatedAt = dept.CreatedAt,
            UpdatedAt = dept.UpdatedAt
        };
    }
}




