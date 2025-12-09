using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Weighing.Interfaces;

namespace TruLoad.Backend.Controllers.WeighingOperations;

/// <summary>
/// API endpoints for axle configuration management.
/// Handles both standard EAC-defined and user-derived axle patterns.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AxleConfigurationController : ControllerBase
{
    private readonly IAxleConfigurationRepository _repository;
    private readonly ILogger<AxleConfigurationController> _logger;

    public AxleConfigurationController(
        IAxleConfigurationRepository repository,
        ILogger<AxleConfigurationController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all axle configurations with optional filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AxleConfigurationResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AxleConfigurationResponseDto>>> GetAll(
        [FromQuery] bool? isStandard = null,
        [FromQuery] string? legalFramework = null,
        [FromQuery] int? axleCount = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var configs = await _repository.GetAllAsync(
            isStandard,
            legalFramework,
            axleCount,
            includeInactive,
            cancellationToken);

        return Ok(configs.Select(MapToResponseDto).ToList());
    }

    /// <summary>
    /// Get axle configuration by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AxleConfigurationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxleConfigurationResponseDto>> GetById(
        Guid id,
        [FromQuery] bool includeWeightReferences = false,
        CancellationToken cancellationToken = default)
    {
        var config = await _repository.GetByIdAsync(id, includeWeightReferences, cancellationToken);
        if (config == null)
        {
            return NotFound(new { message = "Axle configuration not found" });
        }

        return Ok(MapToResponseDto(config));
    }

    /// <summary>
    /// Get axle configuration by code (e.g., "2A", "3B-CUSTOM")
    /// </summary>
    [HttpGet("by-code/{code}")]
    [ProducesResponseType(typeof(AxleConfigurationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxleConfigurationResponseDto>> GetByCode(
        string code,
        CancellationToken cancellationToken = default)
    {
        var config = await _repository.GetByCodeAsync(code, cancellationToken);
        if (config == null)
        {
            return NotFound(new { message = $"Axle configuration with code '{code}' not found" });
        }

        return Ok(MapToResponseDto(config));
    }

    /// <summary>
    /// Create a new derived (user-custom) axle configuration
    /// Standard configurations are immutable and seeded only
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Station Manager")]
    [ProducesResponseType(typeof(AxleConfigurationResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AxleConfigurationResponseDto>> Create(
        [FromBody] CreateAxleConfigurationDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if code already exists
            if (await _repository.CodeExistsAsync(request.AxleCode, cancellationToken: cancellationToken))
            {
                return Conflict(new { message = $"Axle code '{request.AxleCode}' already exists" });
            }

            var config = new AxleConfiguration
            {
                Id = Guid.NewGuid(),
                AxleCode = request.AxleCode,
                AxleName = request.AxleName,
                Description = request.Description,
                AxleNumber = request.AxleNumber,
                GvwPermissibleKg = request.GvwPermissibleKg,
                IsStandard = false, // User-created configs are derived
                LegalFramework = request.LegalFramework ?? "BOTH",
                VisualDiagramUrl = request.VisualDiagramUrl,
                Notes = request.Notes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = GetCurrentUserId()
            };

            var created = await _repository.CreateDerivedConfigAsync(config, cancellationToken);

            _logger.LogInformation(
                "Created derived axle configuration {AxleCode} by user {UserId}",
                created.AxleCode,
                GetCurrentUserId());

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponseDto(created));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Validation error creating axle configuration: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing derived axle configuration
    /// Standard (EAC-defined) configurations cannot be modified
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Station Manager")]
    [ProducesResponseType(typeof(AxleConfigurationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxleConfigurationResponseDto>> Update(
        Guid id,
        [FromBody] UpdateAxleConfigurationDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id, cancellationToken: cancellationToken);
            if (existing == null)
            {
                return NotFound(new { message = "Axle configuration not found" });
            }

            // Update fields
            existing.AxleName = request.AxleName;
            existing.Description = request.Description;
            existing.GvwPermissibleKg = request.GvwPermissibleKg;
            existing.LegalFramework = request.LegalFramework ?? existing.LegalFramework;
            existing.VisualDiagramUrl = request.VisualDiagramUrl;
            existing.Notes = request.Notes;
            existing.IsActive = request.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;

            var updated = await _repository.UpdateDerivedConfigAsync(existing, cancellationToken);

            _logger.LogInformation(
                "Updated derived axle configuration {AxleCode} by user {UserId}",
                updated.AxleCode,
                GetCurrentUserId());

            return Ok(MapToResponseDto(updated));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Error updating axle configuration: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Soft delete an axle configuration (marks as inactive)
    /// Standard configurations cannot be deleted
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Station Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await _repository.SoftDeleteAsync(id, cancellationToken);
            if (!deleted)
            {
                return NotFound(new { message = "Axle configuration not found" });
            }

            _logger.LogInformation("Deleted axle configuration {ConfigId} by user {UserId}", id, GetCurrentUserId());
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Error deleting axle configuration: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get list of available legal frameworks
    /// </summary>
    [HttpGet("lookup/legal-frameworks")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public ActionResult<IEnumerable<string>> GetLegalFrameworks()
    {
        return Ok(new[] { "EAC", "TRAFFIC_ACT", "BOTH" });
    }

    /// <summary>
    /// Get lookup data for creating/editing axle weight references (tyre types and axle groups)
    /// </summary>
    [HttpGet("{id:guid}/lookup")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> GetLookupDataForConfiguration(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var config = await _repository.GetByIdAsync(id, cancellationToken: cancellationToken);
        if (config == null)
        {
            return NotFound(new { message = "Configuration not found" });
        }

        // Get all active tyre types
        var tyreTypes = await Task.FromResult(
            HttpContext.RequestServices.GetService<ITyreTypeRepository>() as ITyreTypeRepository);

        // Get all active axle groups
        var axleGroups = await Task.FromResult(
            HttpContext.RequestServices.GetService<IAxleGroupRepository>() as IAxleGroupRepository);

        var tyreTypeDtos = new List<TyreTypeLookupDto>();
        var axleGroupDtos = new List<AxleGroupLookupDto>();

        // These will be fetched properly via DI in next step
        // For now, return structure

        return Ok(new
        {
            configuration = new
            {
                id = config.Id,
                axleCode = config.AxleCode,
                axleName = config.AxleName,
                axleNumber = config.AxleNumber,
                gvwPermissibleKg = config.GvwPermissibleKg,
                legalFramework = config.LegalFramework
            },
            tyreTypes = tyreTypeDtos,
            axleGroups = axleGroupDtos,
            axleGroupings = new[] { "A", "B", "C", "D" },
            axlePositions = Enumerable.Range(1, config.AxleNumber).ToList()
        });
    }

    private AxleConfigurationResponseDto MapToResponseDto(AxleConfiguration config)
    {
        return new AxleConfigurationResponseDto
        {
            Id = config.Id,
            AxleCode = config.AxleCode,
            AxleName = config.AxleName,
            Description = config.Description,
            AxleNumber = config.AxleNumber,
            GvwPermissibleKg = config.GvwPermissibleKg,
            IsStandard = config.IsStandard,
            LegalFramework = config.LegalFramework,
            VisualDiagramUrl = config.VisualDiagramUrl,
            Notes = config.Notes,
            IsActive = config.IsActive,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt,
            CreatedByUserId = config.CreatedByUserId,
            WeightReferenceCount = config.AxleWeightReferences?.Count ?? 0
        };
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
    }
}
