using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.User;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Interfaces;

namespace TruLoad.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserRepository userRepository, ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(MapToDto(user));
    }

    /// <summary>
    /// Get user by auth service user ID (for SSO sync)
    /// </summary>
    [HttpGet("auth-service/{authServiceUserId:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetByAuthServiceUserId(Guid authServiceUserId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByAuthServiceUserIdAsync(authServiceUserId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(MapToDto(user));
    }

    /// <summary>
    /// Search users with filters and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserDto>>> Search(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? stationId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (take > 100) take = 100; // Max 100 records

        var users = await _userRepository.SearchAsync(search, status, stationId, skip, take, cancellationToken);
        return Ok(users.Select(MapToDto));
    }

    /// <summary>
    /// Get user count (for pagination)
    /// </summary>
    [HttpGet("count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetCount(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? stationId = null,
        CancellationToken cancellationToken = default)
    {
        var count = await _userRepository.CountAsync(search, status, stationId, cancellationToken);
        return Ok(count);
    }

    /// <summary>
    /// Create new user (called by auth-service sync or admin)
    /// </summary>
    [HttpPost]
    [HasPermission("user.create")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        // Check if user already exists
        var existing = await _userRepository.GetByAuthServiceUserIdAsync(request.AuthServiceUserId, cancellationToken);
        if (existing != null)
        {
            return BadRequest(new { message = "User already exists" });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            AuthServiceUserId = request.AuthServiceUserId,
            Email = request.Email,
            FullName = request.FullName,
            Phone = request.Phone,
            StationId = request.StationId,
            OrganizationId = request.OrganizationId,
            DepartmentId = request.DepartmentId,
            Status = "active",
            SyncStatus = "synced",
            LastSyncAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _userRepository.CreateAsync(user, cancellationToken);
        _logger.LogInformation("User created: {UserId}, AuthServiceUserId: {AuthServiceUserId}", created.Id, created.AuthServiceUserId);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    /// <summary>
    /// Update user
    /// </summary>
    [HttpPut("{id:guid}")]
    [HasPermission("user.update")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Update fields
        if (request.FullName != null) user.FullName = request.FullName;
        if (request.Phone != null) user.Phone = request.Phone;
        if (request.Status != null) user.Status = request.Status;
        if (request.StationId.HasValue) user.StationId = request.StationId;
        if (request.OrganizationId.HasValue) user.OrganizationId = request.OrganizationId;
        if (request.DepartmentId.HasValue) user.DepartmentId = request.DepartmentId;

        var updated = await _userRepository.UpdateAsync(user, cancellationToken);
        _logger.LogInformation("User updated: {UserId}", updated.Id);

        return Ok(MapToDto(updated));
    }

    /// <summary>
    /// Delete user (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission("user.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var exists = await _userRepository.ExistsAsync(id, cancellationToken);
        if (!exists)
        {
            return NotFound(new { message = "User not found" });
        }

        await _userRepository.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("User deleted: {UserId}", id);

        return NoContent();
    }

    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            AuthServiceUserId = user.AuthServiceUserId,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Status = user.Status,
            StationId = user.StationId,
            OrganizationId = user.OrganizationId,
            OrganizationName = user.Organization?.Name,
            DepartmentId = user.DepartmentId,
            DepartmentName = user.Department?.Name,
            StationName = user.Station?.Name,
            SyncStatus = user.SyncStatus,
            LastSyncAt = user.LastSyncAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Roles = user.UserRoles?.Select(ur => new RoleDto
            {
                Id = ur.Role.Id,
                Name = ur.Role.Name,
                Description = ur.Role.Description,
                Permissions = ur.Role.Permissions,
                IsActive = ur.Role.IsActive,
                CreatedAt = ur.Role.CreatedAt
            }).ToList() ?? new List<RoleDto>()
        };
    }
}




