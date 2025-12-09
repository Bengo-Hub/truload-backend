using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs;
using TruLoad.Backend.Services.Interfaces;
using TruLoad.Backend.Authorization.Attributes;

namespace TruLoad.Backend.Controllers.UserManagement;

/// <summary>
/// API endpoints for managing permissions.
/// All endpoints require JWT authentication.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(IPermissionService permissionService, ILogger<PermissionsController> logger)
    {
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get all permissions (active and inactive).
    /// Cached for 1 hour.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all permissions</returns>
    /// <response code="200">List of permissions retrieved successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="500">Server error during retrieval</response>
    [HttpGet]
    [HasPermission("system.view_config")]
    [ProducesResponseType(typeof(IEnumerable<PermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PermissionDto>>> GetAllPermissions(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching all permissions");
            var permissions = await _permissionService.GetAllPermissionsAsync(cancellationToken);
            var dtos = permissions.ToDto().ToList();
            
            _logger.LogInformation("Retrieved {Count} permissions", dtos.Count);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Error retrieving permissions",
                Detail = ex.Message,
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get a permission by its unique identifier.
    /// </summary>
    /// <param name="id">The permission ID (GUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The requested permission</returns>
    /// <response code="200">Permission found and returned</response>
    /// <response code="404">Permission not found</response>
    [HttpGet("{id:guid}")]
    [HasPermission("system.view_config")]
    [ProducesResponseType(typeof(PermissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermissionDto>> GetPermissionById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (id == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid permission ID" });

            _logger.LogInformation("Fetching permission with ID: {PermissionId}", id);
            var permission = await _permissionService.GetPermissionByIdAsync(id, cancellationToken);

            if (permission == null)
                return NotFound(new ProblemDetails
                {
                    Title = "Permission not found",
                    Detail = $"No permission with ID {id} exists"
                });

            return Ok(permission.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permission {PermissionId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Error retrieving permission",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get permissions by category.
    /// Cached for 1 hour.
    /// </summary>
    /// <param name="category">The permission category (e.g., "Weighing", "Case", "User")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Permissions in the requested category</returns>
    /// <response code="200">Permissions retrieved successfully</response>
    /// <response code="400">Invalid category</response>
    [HttpGet("category/{category}")]
    [HasPermission("system.view_config")]
    [ProducesResponseType(typeof(IEnumerable<PermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PermissionDto>>> GetPermissionsByCategory(
        string category,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(category))
                return BadRequest(new ProblemDetails { Title = "Category cannot be empty" });

            _logger.LogInformation("Fetching permissions for category: {Category}", category);
            var permissions = await _permissionService.GetPermissionsByCategoryAsync(category, cancellationToken);
            var dtos = permissions.ToDto().ToList();

            _logger.LogInformation("Retrieved {Count} permissions for category {Category}", dtos.Count, category);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for category {Category}", category);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Error retrieving permissions",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get all permissions assigned to a specific role.
    /// Useful for role management and permission verification.
    /// </summary>
    /// <param name="roleId">The role ID (GUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Permissions assigned to the role</returns>
    /// <response code="200">Role permissions retrieved successfully</response>
    /// <response code="400">Invalid role ID</response>
    /// <response code="404">Role not found</response>
    [HttpGet("role/{roleId:guid}")]
    [HasPermission("system.manage_roles")]
    [ProducesResponseType(typeof(IEnumerable<PermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PermissionDto>>> GetRolePermissions(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (roleId == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid role ID" });

            _logger.LogInformation("Fetching permissions for role: {RoleId}", roleId);
            var permissions = await _permissionService.GetPermissionsForRoleAsync(roleId, cancellationToken);
            var dtos = permissions.ToDto().ToList();

            _logger.LogInformation("Retrieved {Count} permissions for role {RoleId}", dtos.Count, roleId);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for role {RoleId}", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Error retrieving role permissions",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Check if a user has a specific permission.
    /// Used for fine-grained authorization checks.
    /// </summary>
    /// <param name="userId">The user ID (GUID)</param>
    /// <param name="permissionCode">The permission code (e.g., "weighing.create")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Whether the user has the permission</returns>
    /// <response code="200">Permission check completed</response>
    /// <response code="400">Invalid parameters</response>
    [HttpGet("check/{userId:guid}/{permissionCode}")]
    [HasPermission("system.view_config")]
    [ProducesResponseType(typeof(PermissionCheckResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PermissionCheckResult>> CheckUserPermission(
        Guid userId,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (userId == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid user ID" });

            if (string.IsNullOrWhiteSpace(permissionCode))
                return BadRequest(new ProblemDetails { Title = "Permission code cannot be empty" });

            _logger.LogInformation("Checking permission {PermissionCode} for user {UserId}", permissionCode, userId);
            var hasPermission = await _permissionService.UserHasPermissionAsync(userId, permissionCode, cancellationToken);

            return Ok(new PermissionCheckResult
            {
                UserId = userId,
                PermissionCode = permissionCode,
                HasPermission = hasPermission
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {PermissionCode} for user {UserId}", permissionCode, userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Error checking permission",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Health check endpoint for permissions service.
    /// Returns basic statistics about permissions in the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Permission service health status</returns>
    /// <response code="200">Service is healthy</response>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PermissionServiceHealth), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionServiceHealth>> GetPermissionServiceHealth(CancellationToken cancellationToken = default)
    {
        try
        {
            var permissions = await _permissionService.GetAllPermissionsAsync(cancellationToken);
            var permissionList = permissions.ToList();
            var categoryStats = permissionList
                .GroupBy(p => p.Category)
                .Select(g => new PermissionCategoryStat { Category = g.Key, Count = g.Count() })
                .OrderBy(c => c.Category)
                .ToList();

            return Ok(new PermissionServiceHealth
            {
                IsHealthy = true,
                TotalPermissions = permissionList.Count,
                Categories = categoryStats,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in permission service health check");
            return StatusCode(StatusCodes.Status500InternalServerError, new PermissionServiceHealth
            {
                IsHealthy = false,
                Categories = Enumerable.Empty<PermissionCategoryStat>(),
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}

/// <summary>
/// Result of a permission check operation.
/// </summary>
public class PermissionCheckResult
{
    /// <summary>
    /// The user ID that was checked.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The permission code that was checked.
    /// </summary>
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>
    /// Whether the user has the permission.
    /// </summary>
    public bool HasPermission { get; set; }
}

/// <summary>
/// Health status of the permission service.
/// </summary>
public class PermissionServiceHealth
{
    /// <summary>
    /// Whether the service is operating normally.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Total number of permissions in the system.
    /// </summary>
    public int TotalPermissions { get; set; }

    /// <summary>
    /// Permission counts by category.
    /// </summary>
    public IEnumerable<PermissionCategoryStat> Categories { get; set; } = Enumerable.Empty<PermissionCategoryStat>();

    /// <summary>
    /// Error message if the service is not healthy.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When this health check was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Summary of permissions per category.
/// </summary>
public class PermissionCategoryStat
{
    /// <summary>
    /// Category name (e.g., Weighing, User, Configuration).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Number of permissions in the category.
    /// </summary>
    public int Count { get; set; }
}
