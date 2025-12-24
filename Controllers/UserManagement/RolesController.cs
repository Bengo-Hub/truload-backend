using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.User;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Controllers;

[ApiController]
[Route("api/v1/user-management/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<RolesController> _logger;

    public RolesController(RoleManager<ApplicationRole> roleManager, ILogger<RolesController> logger)
    {
        _roleManager = roleManager;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> GetById(Guid id)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        return Ok(MapToDto(role));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetAll(
        [FromQuery] bool includeInactive = false)
    {
        var query = _roleManager.Roles.AsQueryable();
        
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }
        
        var roles = await query.OrderBy(r => r.Name).ToListAsync();
        return Ok(roles.Select(MapToDto));
    }

    [HttpPost]
    [HasPermission("system.manage_roles")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleRequest request)
    {
        // Check if name already exists
        var existing = await _roleManager.FindByNameAsync(request.Name);
        if (existing != null)
        {
            return BadRequest(new { message = "Role name already exists" });
        }

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            NormalizedName = request.Name.ToUpperInvariant(),
            Code = request.Code ?? request.Name.ToLowerInvariant().Replace(" ", "_"),
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        _logger.LogInformation("Role created: {RoleId}, Name: {Name}", role.Id, role.Name);
        return CreatedAtAction(nameof(GetById), new { id = role.Id }, MapToDto(role));
    }

    [HttpPut("{id:guid}")]
    [HasPermission("system.manage_roles")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> Update(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        // Check if new name conflicts
        if (!string.IsNullOrWhiteSpace(request.Name) && request.Name != role.Name)
        {
            var existing = await _roleManager.FindByNameAsync(request.Name);
            if (existing != null && existing.Id != role.Id)
            {
                return BadRequest(new { message = "Role name already exists" });
            }
            
            role.Name = request.Name;
            role.NormalizedName = request.Name.ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            role.Code = request.Code;
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            role.Description = request.Description;
        }

        if (request.IsActive.HasValue)
        {
            role.IsActive = request.IsActive.Value;
        }

        role.UpdatedAt = DateTime.UtcNow;

        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        _logger.LogInformation("Role updated: {RoleId}", role.Id);
        return Ok(MapToDto(role));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("system.manage_roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        _logger.LogInformation("Role deleted: {RoleId}", id);
        return NoContent();
    }

    private static RoleDto MapToDto(ApplicationRole role)
    {
        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name!,
            Code = role.Code,
            Description = role.Description,
            IsActive = role.IsActive,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt
        };
    }
}




