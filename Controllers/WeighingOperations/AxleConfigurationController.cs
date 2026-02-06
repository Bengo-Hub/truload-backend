using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Middleware;
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
    private readonly IAxleWeightReferenceRepository _weightRefRepository;
    private readonly ITyreTypeRepository _tyreTypeRepository;
    private readonly IAxleGroupRepository _axleGroupRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AxleConfigurationController> _logger;

    public AxleConfigurationController(
        IAxleConfigurationRepository repository,
        IAxleWeightReferenceRepository weightRefRepository,
        ITyreTypeRepository tyreTypeRepository,
        IAxleGroupRepository axleGroupRepository,
        ITenantContext tenantContext,
        ILogger<AxleConfigurationController> logger)
    {
        _repository = repository;
        _weightRefRepository = weightRefRepository;
        _tyreTypeRepository = tyreTypeRepository;
        _axleGroupRepository = axleGroupRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all axle configurations with optional filtering
    /// Invalid configurations (empty axleCode or axleNumber = 0) are excluded
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

        // Filter out invalid configurations (empty axleCode or axleNumber = 0)
        var validConfigs = configs
            .Where(c => !string.IsNullOrWhiteSpace(c.AxleCode) && c.AxleNumber > 0)
            .ToList();

        if (validConfigs.Count < configs.Count)
        {
            _logger.LogWarning(
                "AxleConfigurationController.GetAll: Filtered out {FilteredCount} invalid configurations (empty axleCode or axleNumber = 0)",
                configs.Count - validConfigs.Count);
        }

        _logger.LogInformation("AxleConfigurationController.GetAll: Returning {Count} configurations for isStandard={IsStandard}, includeInactive={IncludeInactive}",
            validConfigs.Count, isStandard, includeInactive);

        return Ok(validConfigs.Select(MapToResponseDto).ToList());
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
    /// Create a new derived (user-custom) axle configuration.
    /// GVW is auto-calculated from the sum of weight reference legal weights.
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

            // Auto-calculate GVW from weight references
            var gvw = request.WeightReferences?.Sum(wr => wr.AxleLegalWeightKg) ?? 0;

            var config = new AxleConfiguration
            {
                Id = Guid.NewGuid(),
                AxleCode = request.AxleCode,
                AxleName = request.AxleName,
                Description = request.Description,
                AxleNumber = request.AxleNumber,
                GvwPermissibleKg = gvw,
                IsStandard = false,
                LegalFramework = request.LegalFramework ?? "BOTH",
                VisualDiagramUrl = request.VisualDiagramUrl,
                Notes = request.Notes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = GetCurrentUserId()
            };

            var created = await _repository.CreateDerivedConfigAsync(config, cancellationToken);

            // Create weight references if provided
            if (request.WeightReferences is { Count: > 0 })
            {
                foreach (var wrDto in request.WeightReferences)
                {
                    var weightRef = new AxleWeightReference
                    {
                        Id = Guid.NewGuid(),
                        AxleConfigurationId = created.Id,
                        AxlePosition = wrDto.AxlePosition,
                        AxleLegalWeightKg = wrDto.AxleLegalWeightKg,
                        AxleGrouping = wrDto.AxleGrouping,
                        AxleGroupId = wrDto.AxleGroupId,
                        TyreTypeId = wrDto.TyreTypeId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _weightRefRepository.CreateAsync(weightRef, cancellationToken);
                }
            }

            // Re-fetch with weight references for response
            var result = await _repository.GetByIdAsync(created.Id, includeWeightReferences: true, cancellationToken);

            _logger.LogInformation(
                "Created derived axle configuration {AxleCode} with {RefCount} weight refs (GVW={Gvw}kg) by user {UserId}",
                created.AxleCode,
                request.WeightReferences?.Count ?? 0,
                gvw,
                GetCurrentUserId());

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponseDto(result!));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Validation error creating axle configuration: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing derived axle configuration.
    /// When weight references are provided, they replace existing ones and GVW is recalculated.
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

            // Update basic fields
            existing.AxleName = request.AxleName;
            existing.Description = request.Description;
            existing.LegalFramework = request.LegalFramework ?? existing.LegalFramework;
            existing.VisualDiagramUrl = request.VisualDiagramUrl;
            existing.Notes = request.Notes;
            existing.IsActive = request.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;

            // Handle weight references: replace all and recalculate GVW
            if (request.WeightReferences != null)
            {
                // Delete all existing weight references
                var existingRefs = await _weightRefRepository.GetByConfigurationIdAsync(id, cancellationToken: cancellationToken);
                foreach (var existingRef in existingRefs)
                {
                    await _weightRefRepository.DeleteAsync(existingRef.Id, cancellationToken);
                }

                // Create new weight references
                foreach (var wrDto in request.WeightReferences)
                {
                    var weightRef = new AxleWeightReference
                    {
                        Id = Guid.NewGuid(),
                        AxleConfigurationId = id,
                        AxlePosition = wrDto.AxlePosition,
                        AxleLegalWeightKg = wrDto.AxleLegalWeightKg,
                        AxleGrouping = wrDto.AxleGrouping,
                        AxleGroupId = wrDto.AxleGroupId,
                        TyreTypeId = wrDto.TyreTypeId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _weightRefRepository.CreateAsync(weightRef, cancellationToken);
                }

                // Recalculate GVW from new weight references
                existing.GvwPermissibleKg = request.WeightReferences.Sum(wr => wr.AxleLegalWeightKg);
            }

            var updated = await _repository.UpdateDerivedConfigAsync(existing, cancellationToken);

            // Re-fetch with weight references for response
            var result = await _repository.GetByIdAsync(id, includeWeightReferences: true, cancellationToken);

            _logger.LogInformation(
                "Updated derived axle configuration {AxleCode} (GVW={Gvw}kg) by user {UserId}",
                updated.AxleCode,
                existing.GvwPermissibleKg,
                GetCurrentUserId());

            return Ok(MapToResponseDto(result!));
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
    /// Get lookup data for creating/editing axle weight references (tyre types and axle groups).
    /// Does not require a configuration ID - used for create form.
    /// </summary>
    [HttpGet("lookup/weight-ref-data")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetWeightReferenceLookupData(
        CancellationToken cancellationToken = default)
    {
        var tyreTypes = await _tyreTypeRepository.GetAllActiveAsync(cancellationToken);
        var axleGroups = await _axleGroupRepository.GetAllActiveAsync(cancellationToken);

        return Ok(new
        {
            tyreTypes = tyreTypes.Select(t => new TyreTypeLookupDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                Description = t.Description,
                TypicalMaxWeightKg = t.TypicalMaxWeightKg
            }).ToList(),
            axleGroups = axleGroups.Select(g => new AxleGroupLookupDto
            {
                Id = g.Id,
                Code = g.Code,
                Name = g.Name,
                Description = g.Description,
                TypicalWeightKg = g.TypicalWeightKg
            }).ToList(),
            axleGroupings = new[] { "A", "B", "C", "D" }
        });
    }

    /// <summary>
    /// Get lookup data for a specific configuration (includes config details)
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

        var tyreTypes = await _tyreTypeRepository.GetAllActiveAsync(cancellationToken);
        var axleGroups = await _axleGroupRepository.GetAllActiveAsync(cancellationToken);

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
            tyreTypes = tyreTypes.Select(t => new TyreTypeLookupDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                Description = t.Description,
                TypicalMaxWeightKg = t.TypicalMaxWeightKg
            }).ToList(),
            axleGroups = axleGroups.Select(g => new AxleGroupLookupDto
            {
                Id = g.Id,
                Code = g.Code,
                Name = g.Name,
                Description = g.Description,
                TypicalWeightKg = g.TypicalWeightKg
            }).ToList(),
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
            WeightReferenceCount = config.AxleWeightReferences?.Count ?? 0,
            WeightReferences = config.AxleWeightReferences?.Select(wr => new AxleWeightReferenceDto
            {
                Id = wr.Id,
                AxleConfigurationId = wr.AxleConfigurationId,
                AxlePosition = wr.AxlePosition,
                AxleLegalWeightKg = wr.AxleLegalWeightKg,
                AxleGroupId = wr.AxleGroupId,
                AxleGrouping = wr.AxleGrouping,
                TyreTypeId = wr.TyreTypeId,
                IsActive = wr.IsActive,
                CreatedAt = wr.CreatedAt
            }).ToList()
        };
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
    }
}
