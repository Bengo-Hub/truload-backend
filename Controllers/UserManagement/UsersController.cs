using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.User;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Controllers;

[ApiController]
[Route("api/v1/user-management/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly TruLoadDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        TruLoadDbContext context,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission("user.read")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetById(Guid id)
    {
        var user = await _context.Users
            .Include(u => u.Station)
            .Include(u => u.Organization)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null || user.DeletedAt != null)
        {
            return NotFound(new { message = "User not found" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (!User.IsInRole("Superuser"))
        {
            var systemRoleIds = await _roleManager.Roles.Where(r => r.IsSystemRole).Select(r => r.Id).ToListAsync();
            var userRoleIds = await _context.Set<IdentityUserRole<Guid>>()
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.RoleId)
                .ToListAsync();
            if (userRoleIds.Any(rid => systemRoleIds.Contains(rid)))
                return NotFound(new { message = "User not found" });
        }

        return Ok(await MapToDto(user, roles));
    }

    /// <summary>
    /// Create new user
    /// </summary>
    [HttpPost]
    [HasPermission("user.create")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request)
    {
        // 1. Validate required fields
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required" });

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Password is required" });

        // 2. Validate roles exist and non-superusers cannot assign system roles
        if (request.RoleNames != null && request.RoleNames.Any())
        {
            if (!User.IsInRole("Superuser"))
            {
                var systemRoles = await _roleManager.Roles.Where(r => r.IsSystemRole).Select(r => r.Name).ToListAsync();
                var attemptedSystem = request.RoleNames.Where(n => systemRoles.Contains(n!, StringComparer.OrdinalIgnoreCase)).ToList();
                if (attemptedSystem.Any())
                    return BadRequest(new { message = "You cannot assign system roles to users." });
            }
            foreach (var roleName in request.RoleNames)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    return BadRequest(new { message = $"Role '{roleName}' does not exist" });
                }
            }
        }

        // 3. Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return BadRequest(new { message = "User with this email already exists" });
        }

        // 4. Create user within a transaction to ensure role assignment success
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Fetch default organization and station ONLY if they were not provided in the request
                var finalOrgId = request.OrganizationId;
                if (!finalOrgId.HasValue || finalOrgId.Value == Guid.Empty)
                {
                    var defaultOrg = await _context.Organizations.FirstOrDefaultAsync(o => o.IsDefault);
                    finalOrgId = defaultOrg?.Id;
                }

                var finalStationId = request.StationId;
                if (!finalStationId.HasValue || finalStationId.Value == Guid.Empty)
                {
                    var defaultStation = await _context.Stations.FirstOrDefaultAsync(s => s.IsDefault);
                    finalStationId = defaultStation?.Id;
                }

                // Tenant users must have a station assigned (platform/superuser creating without org may omit)
                if (finalOrgId.HasValue && finalOrgId.Value != Guid.Empty && (!finalStationId.HasValue || finalStationId.Value == Guid.Empty))
                {
                    return BadRequest(new { message = "Station is required when creating a tenant user." });
                }

                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = request.Email,
                    Email = request.Email,
                    FullName = request.FullName ?? string.Empty,
                    PhoneNumber = request.PhoneNumber,
                    OrganizationId = finalOrgId ?? Guid.Empty, // Identity might require a valid guid or null depending on FK constraint
                    StationId = finalStationId,
                    DepartmentId = request.DepartmentId,
                    EmailConfirmed = true, // Auto-confirm for admin-created users
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { errors = result.Errors });
                }

                // 5. Assign roles
                if (request.RoleNames != null && request.RoleNames.Any())
                {
                    var roleResult = await _userManager.AddToRolesAsync(user, request.RoleNames);
                    if (!roleResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { errors = roleResult.Errors });
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation("User created: {UserId}, Email: {Email}", user.Id, user.Email);

                var roles = await _userManager.GetRolesAsync(user);
                var withIncludes = await _context.Users
                    .Include(u => u.Station)
                    .Include(u => u.Organization)
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.Id == user.Id);
                return CreatedAtAction(nameof(GetById), new { id = user.Id }, await MapToDto(withIncludes ?? user, roles));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating user {Email}", request.Email);
                return StatusCode(500, new { message = "An error occurred during user creation." });
            }
        });
    }

    /// <summary>
    /// Get user by email
    /// </summary>
    [HttpGet("by-email/{email}")]
    [HasPermission("user.read")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetByEmail(string email)
    {
        var user = await _context.Users
            .Include(u => u.Station)
            .Include(u => u.Organization)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Email == email);
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
    [HasPermission("user.read")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Search(
        [FromQuery] string? search = null,
        [FromQuery] Guid? organizationId = null,
        [FromQuery] Guid? stationId = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] string? roleName = null,
        [FromQuery] Guid? workShiftId = null,
        [FromQuery] bool? hasActiveShift = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        if (pageSize > 1000) pageSize = 1000;
        var skip = (pageNumber - 1) * pageSize;

        var query = _userManager.Users
            .Include(u => u.Station)
            .Include(u => u.Organization)
            .Include(u => u.Department)
            .Where(u => u.DeletedAt == null)
            .AsQueryable();

        // Non-superusers must not see users who have a system role (Superuser, Middleware Service)
        if (!User.IsInRole("Superuser"))
        {
            var systemRoleIds = await _roleManager.Roles.Where(r => r.IsSystemRole).Select(r => r.Id).ToListAsync();
            var userIdsWithSystemRole = await _context.Set<IdentityUserRole<Guid>>()
                .Where(ur => systemRoleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();
            query = query.Where(u => !userIdsWithSystemRole.Contains(u.Id));
        }

        // Text search across name, email, phone
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.FullName.Contains(search) ||
                u.Email!.Contains(search) ||
                u.PhoneNumber!.Contains(search));
        }

        // Organization filter
        if (organizationId.HasValue)
        {
            query = query.Where(u => u.OrganizationId == organizationId.Value);
        }

        // Station filter
        if (stationId.HasValue)
        {
            query = query.Where(u => u.StationId == stationId.Value);
        }

        // Department filter
        if (departmentId.HasValue)
        {
            query = query.Where(u => u.DepartmentId == departmentId.Value);
        }

        // Active status filter (based on LockoutEnd)
        if (isActive.HasValue)
        {
            if (isActive.Value)
            {
                // Active users: not locked out
                query = query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow);
            }
            else
            {
                // Inactive users: currently locked out
                query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
            }
        }

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.FullName)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        var userDtos = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            // Apply role filter (post-query since roles are in separate table)
            if (!string.IsNullOrWhiteSpace(roleName) && !roles.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            {
                total--; // Adjust total count
                continue;
            }

            userDtos.Add(await MapToDto(user, roles));
        }

        // Apply shift filters if needed (post-query since shifts are in separate table)
        if (workShiftId.HasValue || hasActiveShift.HasValue)
        {
            var filteredDtos = new List<UserDto>();

            foreach (var dto in userDtos)
            {
                // Query user shifts from database (UserShift doesn't have DeletedAt)
                var userShifts = await _userManager.Users
                    .Where(u => u.Id == dto.Id)
                    .SelectMany(u => u.UserShifts)
                    .ToListAsync();

                // Filter by work shift ID
                if (workShiftId.HasValue)
                {
                    var hasShift = userShifts.Any(us => us.WorkShiftId == workShiftId.Value);
                    if (!hasShift)
                    {
                        total--;
                        continue;
                    }
                }

                // Filter by active shift status
                if (hasActiveShift.HasValue)
                {
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    var hasActive = userShifts.Any(us =>
                        us.StartsOn <= today &&
                        (us.EndsOn == null || us.EndsOn > today));

                    if (hasActive != hasActiveShift.Value)
                    {
                        total--;
                        continue;
                    }
                }

                filteredDtos.Add(dto);
            }

            userDtos = filteredDtos;
        }

        return Ok(PagedResponse<UserDto>.Create(userDtos, total, pageNumber, pageSize));
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

        // Reload with includes so MapToDto has Station/Organization/Department names
        var reloaded = await _context.Users
            .Include(u => u.Station)
            .Include(u => u.Organization)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(await MapToDto(reloaded ?? user, roles));
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
    [HasPermission("user.assign_roles")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.DeletedAt != null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Resolve all role names (from names and IDs)
        var roleNamesToAssign = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (request.RoleNames != null)
        {
            foreach (var name in request.RoleNames) roleNamesToAssign.Add(name);
        }

        if (request.RoleIds != null)
        {
            foreach (var roleId in request.RoleIds)
            {
                var role = await _roleManager.FindByIdAsync(roleId.ToString());
                if (role?.Name != null) roleNamesToAssign.Add(role.Name);
            }
        }

        if (!roleNamesToAssign.Any())
        {
            return BadRequest(new { message = "No valid roles provided" });
        }

        // Non-superusers cannot assign system roles
        if (!User.IsInRole("Superuser"))
        {
            var systemRoles = await _roleManager.Roles.Where(r => r.IsSystemRole).Select(r => r.Name).ToListAsync();
            var attemptedSystem = roleNamesToAssign.Where(n => systemRoles.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
            if (attemptedSystem.Any())
                return BadRequest(new { message = "You cannot assign system roles." });
        }

        // Validate all roles exist
        foreach (var roleName in roleNamesToAssign)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                return BadRequest(new { message = $"Role '{roleName}' does not exist" });
            }
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Remove existing roles
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.Any())
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    if (!removeResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { errors = removeResult.Errors });
                    }
                }

                // Add new roles
                var addResult = await _userManager.AddToRolesAsync(user, roleNamesToAssign);
                if (!addResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { errors = addResult.Errors });
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Roles assigned to user: {UserId}", id);
                return Ok(new { message = "Roles assigned successfully" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error assigning roles to user {UserId}", id);
                return StatusCode(500, new { message = "An error occurred while assigning roles." });
            }
        });
    }

    /// <summary>
    /// Get user roles
    /// </summary>
    [HttpGet("{id:guid}/roles")]
    [HasPermission("user.read")]
    [ProducesResponseType(typeof(UserRolesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserRolesDto>> GetUserRoles(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.DeletedAt != null)
        {
            return NotFound(new { message = "User not found" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var roleObjects = new List<ApplicationRole>();

        foreach (var roleName in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                roleObjects.Add(role);
            }
        }

        return Ok(new UserRolesDto
        {
            UserId = id,
            UserEmail = user.Email!,
            UserFullName = user.FullName,
            Roles = roleObjects.Select(r => new RoleDto
            {
                Id = r.Id,
                Name = r.Name!,
                Code = r.Code,
                Description = r.Description,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList()
        });
    }

    /// <summary>
    /// Remove role from user
    /// </summary>
    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [HasPermission("user.assign_roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRoleFromUser(Guid id, Guid roleId)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.DeletedAt != null)
        {
            return NotFound(new { message = "User not found" });
        }

        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        var result = await _userManager.RemoveFromRoleAsync(user, role.Name!);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        _logger.LogInformation("Role {RoleId} removed from user {UserId}", roleId, id);
        return NoContent();
    }

    /// <summary>
    /// Admin reset user password (sets a new password directly)
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    [HasPermission("user.update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminResetPassword(Guid id, [FromBody] AdminResetPasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null || user.DeletedAt != null)
        {
            return NotFound(new { message = "User not found" });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        _logger.LogInformation("Admin reset password for user: {UserId}", id);
        return Ok(new { message = "Password reset successfully" });
    }

    /// <summary>
    /// Gets users grouped by station.
    /// </summary>
    [HttpGet("by-station")]
    [HasPermission("user.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<UsersByStationDto>), 200)]
    public async Task<IActionResult> GetUsersByStation(CancellationToken ct)
    {
        try
        {
            var users = await _userManager.Users
                .Where(u => u.StationId.HasValue)
                .Include(u => u.Station)
                .ToListAsync(ct);

            var grouped = users
                .GroupBy(u => new { u.StationId, StationName = u.Station?.Name ?? "Unknown" })
                .Select(g => new UsersByStationDto
                {
                    StationId = g.Key.StationId ?? Guid.Empty,
                    StationName = g.Key.StationName,
                    Count = g.Count()
                })
                .OrderBy(d => d.StationName)
                .ToList();

            return Ok(grouped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users by station");
            return StatusCode(500, "An error occurred while getting users by station.");
        }
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
            OrganizationName = user.Organization?.Name,
            StationId = user.StationId,
            StationName = user.Station?.Name,
            DepartmentId = user.DepartmentId,
            DepartmentName = user.Department?.Name,
            Roles = roles.ToList(),
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}




