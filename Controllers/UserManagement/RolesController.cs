using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.User;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Services.Interfaces;

namespace TruLoad.Controllers;

[ApiController]
[Route("api/v1/user-management/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RolesController> _logger;
    private readonly IPermissionService _permissionService;

    public RolesController(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger<RolesController> logger,
        IPermissionService permissionService)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _logger = logger;
        _permissionService = permissionService;
    }

    [HttpGet("{id:guid}")]
    [HasPermission("system.manage_roles")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> GetById(Guid id)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }
        if (!User.IsInRole("Superuser") && role.IsSystemRole)
            return NotFound(new { message = "Role not found" });

        return Ok(MapToDto(role));
    }

    [HttpGet]
    [HasPermission("system.manage_roles")]
    [ProducesResponseType(typeof(IEnumerable<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetAll(
        [FromQuery] bool includeInactive = false)
    {
        var query = _roleManager.Roles.AsQueryable();

        if (!User.IsInRole("Superuser"))
            query = query.Where(r => !r.IsSystemRole);
        
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

    [HttpGet("{id:guid}/permissions")]
    [HasPermission("user.manage_permissions")]
    [ProducesResponseType(typeof(RolePermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RolePermissionsDto>> GetRolePermissions(Guid id)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        var permissions = await _permissionService.GetPermissionsForRoleAsync(id);
        return Ok(new RolePermissionsDto
        {
            RoleId = id,
            RoleName = role.Name!,
            Permissions = permissions.Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Category = p.Category,
                IsActive = p.IsActive
            }).ToList()
        });
    }

    [HttpPost("{id:guid}/permissions")]
    [HasPermission("user.manage_permissions")]
    [ProducesResponseType(typeof(RolePermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RolePermissionsDto>> AssignPermissionsToRole(Guid id, [FromBody] AssignPermissionsRequest request)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        if (request.PermissionIds == null || !request.PermissionIds.Any())
        {
            return BadRequest(new { message = "At least one permission ID must be provided" });
        }

        if (!User.IsInRole("Superuser"))
        {
            var allPerms = await _permissionService.GetAllPermissionsAsync();
            var systemSensitiveIds = allPerms.Where(p => p.IsSystemSensitive).Select(p => p.Id).ToHashSet();
            var attempted = request.PermissionIds.Where(pid => systemSensitiveIds.Contains(pid)).ToList();
            if (attempted.Any())
                return BadRequest(new { message = "You cannot assign system-sensitive permissions." });
        }

        try
        {
            await _permissionService.AssignPermissionsToRoleAsync(id, request.PermissionIds);
            _logger.LogInformation("Permissions assigned to role {RoleId}: {PermissionIds}", id, string.Join(", ", request.PermissionIds));

            // Return updated permissions
            var permissions = await _permissionService.GetPermissionsForRoleAsync(id);
            return Ok(new RolePermissionsDto
            {
                RoleId = id,
                RoleName = role.Name!,
                Permissions = permissions.Select(p => new PermissionDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    Category = p.Category,
                    IsActive = p.IsActive
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign permissions to role {RoleId}", id);
            return BadRequest(new { message = "Failed to assign permissions to role" });
        }
    }

    [HttpDelete("{id:guid}/permissions/{permissionId:guid}")]
    [HasPermission("user.manage_permissions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePermissionFromRole(Guid id, Guid permissionId)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        try
        {
            await _permissionService.RemovePermissionsFromRoleAsync(id, new[] { permissionId });
            _logger.LogInformation("Permission {PermissionId} removed from role {RoleId}", permissionId, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove permission {PermissionId} from role {RoleId}", permissionId, id);
            return BadRequest(new { message = "Failed to remove permission from role" });
        }
    }

    [HttpGet("{id:guid}/users")]
    [HasPermission("user.manage_permissions")]
    [ProducesResponseType(typeof(RoleUsersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleUsersDto>> GetRoleUsers(Guid id)
    {
        var role = await _roleManager.FindByIdAsync(id.ToString());
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        // Get users assigned to this role
        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        var userSummaries = usersInRole.Select(u => new UserSummaryDto
        {
            Id = u.Id,
            Email = u.Email!,
            FullName = u.FullName
        }).ToList();

        return Ok(new RoleUsersDto
        {
            RoleId = id,
            RoleName = role.Name!,
            Users = userSummaries
        });
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
            IsSystemRole = role.IsSystemRole,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt
        };
    }
}




