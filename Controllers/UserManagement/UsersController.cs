using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.User;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Controllers;

[ApiController]
[Route("api/v1/user-management/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetById(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.DeletedAt != null)
        {
            return NotFound(new { message = "User not found" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(await MapToDto(user, roles));
    }

    /// <summary>
    /// Get user by email
    /// </summary>
    [HttpGet("by-email/{email}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetByEmail(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || user.DeletedAt != null)
        {
            return NotFound(new { message = "User not found" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(await MapToDto(user, roles));
    }

    /// <summary>
    /// Search users with filters and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Search(
        [FromQuery] string? search = null,
        [FromQuery] Guid? organizationId = null,
        [FromQuery] Guid? stationId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        if (take > 100) take = 100;

        var query = _userManager.Users
            .Where(u => u.DeletedAt == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => 
                u.FullName.Contains(search) || 
                u.Email!.Contains(search) ||
                u.PhoneNumber!.Contains(search));
        }

        if (organizationId.HasValue)
        {
            query = query.Where(u => u.OrganizationId == organizationId.Value);
        }

        if (stationId.HasValue)
        {
            query = query.Where(u => u.StationId == stationId.Value);
        }

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.FullName)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        var userDtos = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(await MapToDto(user, roles));
        }

        return Ok(new
        {
            total,
            skip,
            take,
            data = userDtos
        });
    }

    /// <summary>
    /// Update user profile (admin only)
    /// </summary>
    [HttpPut("{id:guid}")]
    [HasPermission("user.update")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.DeletedAt != null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Update allowed fields
        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName;
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            user.PhoneNumber = request.PhoneNumber;
        }

        if (request.OrganizationId.HasValue)
        {
            user.OrganizationId = request.OrganizationId.Value;
        }

        if (request.StationId.HasValue)
        {
            user.StationId = request.StationId.Value;
        }

        if (request.DepartmentId.HasValue)
        {
            user.DepartmentId = request.DepartmentId.Value;
        }

        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        _logger.LogInformation("User updated: {UserId}", user.Id);

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(await MapToDto(user, roles));
    }

    /// <summary>
    /// Soft delete user
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission("user.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.DeletedAt != null)
        {
            return NotFound(new { message = "User not found" });
        }

        user.DeletedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User soft deleted: {UserId}", id);
        return NoContent();
    }

    /// <summary>
    /// Assign roles to user
    /// </summary>
    [HttpPost("{id:guid}/roles")]
    [HasPermission("user.update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.DeletedAt != null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Remove existing roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        // Add new roles
        if (request.RoleNames != null && request.RoleNames.Any())
        {
            var result = await _userManager.AddToRolesAsync(user, request.RoleNames);
            if (!result.Succeeded)
            {
                return BadRequest(new { errors = result.Errors });
            }
        }

        _logger.LogInformation("Roles assigned to user: {UserId}", id);
        return Ok(new { message = "Roles assigned successfully" });
    }

    private async Task<UserDto> MapToDto(ApplicationUser user, IList<string> roles)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email!,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            OrganizationId = user.OrganizationId,
            StationId = user.StationId,
            DepartmentId = user.DepartmentId,
            Roles = roles.ToList(),
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}




