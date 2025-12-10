using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.User;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;

namespace TruLoad.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleRepository _roleRepository;
    private readonly ILogger<RolesController> _logger;

    public RolesController(IRoleRepository roleRepository, ILogger<RolesController> logger)
    {
        _roleRepository = roleRepository;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(id, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        return Ok(MapToDto(role));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepository.GetAllAsync(includeInactive, cancellationToken);
        return Ok(roles.Select(MapToDto));
    }

    [HttpPost]
    [HasPermission("system.manage_roles")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        // Check if name already exists
        if (await _roleRepository.NameExistsAsync(request.Name, cancellationToken: cancellationToken))
        {
            return BadRequest(new { message = "Role name already exists" });
        }

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _roleRepository.CreateAsync(role, cancellationToken);
        _logger.LogInformation("Role created: {RoleId}, Name: {Name}", created.Id, created.Name);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    [HttpPut("{id:guid}")]
    [HasPermission("system.manage_roles")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RoleDto>> Update(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(id, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        // Check if new name conflicts
        if (request.Name != null && request.Name != role.Name)
        {
            if (await _roleRepository.NameExistsAsync(request.Name, id, cancellationToken))
            {
                return BadRequest(new { message = "Role name already exists" });
            }
            role.Name = request.Name;
        }

        if (request.Description != null) role.Description = request.Description;
        if (request.IsActive.HasValue) role.IsActive = request.IsActive.Value;

        var updated = await _roleRepository.UpdateAsync(role, cancellationToken);
        _logger.LogInformation("Role updated: {RoleId}", updated.Id);

        return Ok(MapToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("system.manage_roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(id, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        await _roleRepository.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Role deleted: {RoleId}", id);

        return NoContent();
    }

    private static RoleDto MapToDto(Role role)
    {
        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsActive = role.IsActive,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt
        };
    }
}




