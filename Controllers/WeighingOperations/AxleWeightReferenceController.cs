using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Weighing.Interfaces;

namespace TruLoad.Backend.Controllers.WeighingOperations;

/// <summary>
/// API endpoints for managing axle weight references (weight specs per axle position).
/// Each weight reference defines the permissible weight for one axle position within a configuration.
/// </summary>
[ApiController]
[Route("api/v1/AxleWeightReferences")]
[Authorize]
public class AxleWeightReferenceController : ControllerBase
{
    private readonly IAxleWeightReferenceRepository _repository;
    private readonly IAxleConfigurationRepository _configRepository;
    private readonly ILogger<AxleWeightReferenceController> _logger;

    public AxleWeightReferenceController(
        IAxleWeightReferenceRepository repository,
        IAxleConfigurationRepository configRepository,
        ILogger<AxleWeightReferenceController> logger)
    {
        _repository = repository;
        _configRepository = configRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get a weight reference by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AxleWeightReferenceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxleWeightReferenceResponseDto>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var reference = await _repository.GetByIdAsync(id, cancellationToken);
        if (reference == null)
        {
            return NotFound(new { message = "Weight reference not found" });
        }

        return Ok(MapToResponseDto(reference));
    }

    /// <summary>
    /// Get all weight references for a configuration
    /// Returns ordered by axle position
    /// </summary>
    [HttpGet("by-configuration/{configurationId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AxleWeightReferenceResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AxleWeightReferenceResponseDto>>> GetByConfiguration(
        Guid configurationId,
        CancellationToken cancellationToken = default)
    {
        var references = await _repository.GetByConfigurationIdAsync(
            configurationId,
            includeRelations: true,
            cancellationToken);

        return Ok(references.Select(MapToResponseDto).ToList());
    }

    /// <summary>
    /// Create a new weight reference for an axle position
    /// Validates position constraints and relationships
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Station Manager")]
    [ProducesResponseType(typeof(AxleWeightReferenceResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxleWeightReferenceResponseDto>> Create(
        [FromBody] CreateAxleWeightReferenceDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify parent configuration exists
            var config = await _configRepository.GetByIdAsync(
                request.AxleConfigurationId,
                cancellationToken: cancellationToken);

            if (config == null)
            {
                return NotFound(new { message = "Axle configuration not found" });
            }

            // Create reference
            var reference = new AxleWeightReference
            {
                Id = Guid.NewGuid(),
                AxleConfigurationId = request.AxleConfigurationId,
                AxlePosition = request.AxlePosition,
                AxleLegalWeightKg = request.AxleLegalWeightKg,
                AxleGrouping = request.AxleGrouping,
                AxleGroupId = request.AxleGroupId,
                TyreTypeId = request.TyreTypeId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Validate
            var (isValid, errors) = await _repository.ValidateAsync(reference, config, cancellationToken);
            if (!isValid)
            {
                return BadRequest(new { message = "Validation failed", errors });
            }

            // Save
            var created = await _repository.CreateAsync(reference, cancellationToken);

            _logger.LogInformation(
                "Created weight reference for configuration {ConfigId} at position {Position} by user {UserId}",
                created.AxleConfigurationId,
                created.AxlePosition,
                GetCurrentUserId());

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToResponseDto(created));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Error creating weight reference: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing weight reference
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Station Manager")]
    [ProducesResponseType(typeof(AxleWeightReferenceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxleWeightReferenceResponseDto>> Update(
        Guid id,
        [FromBody] UpdateAxleWeightReferenceDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id, cancellationToken);
            if (existing == null)
            {
                return NotFound(new { message = "Weight reference not found" });
            }

            // Get parent config for validation
            var config = await _configRepository.GetByIdAsync(
                existing.AxleConfigurationId,
                cancellationToken: cancellationToken);

            if (config == null)
            {
                return BadRequest(new { message = "Parent configuration not found" });
            }

            // Update fields
            existing.AxlePosition = request.AxlePosition;
            existing.AxleLegalWeightKg = request.AxleLegalWeightKg;
            existing.AxleGrouping = request.AxleGrouping;
            existing.AxleGroupId = request.AxleGroupId;
            existing.TyreTypeId = request.TyreTypeId;
            existing.IsActive = request.IsActive;

            // Validate
            var (isValid, errors) = await _repository.ValidateAsync(existing, config, cancellationToken);
            if (!isValid)
            {
                return BadRequest(new { message = "Validation failed", errors });
            }

            // Save
            var updated = await _repository.UpdateAsync(existing, cancellationToken);

            _logger.LogInformation(
                "Updated weight reference {RefId} for configuration {ConfigId} by user {UserId}",
                updated.Id,
                updated.AxleConfigurationId,
                GetCurrentUserId());

            return Ok(MapToResponseDto(updated));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Error updating weight reference: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a weight reference
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Station Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(new { message = "Weight reference not found" });
        }

        _logger.LogInformation("Deleted weight reference {RefId} by user {UserId}", id, GetCurrentUserId());
        return NoContent();
    }

    private AxleWeightReferenceResponseDto MapToResponseDto(AxleWeightReference reference)
    {
        return new AxleWeightReferenceResponseDto
        {
            Id = reference.Id,
            AxleConfigurationId = reference.AxleConfigurationId,
            AxlePosition = reference.AxlePosition,
            AxleLegalWeightKg = reference.AxleLegalWeightKg,
            AxleGrouping = reference.AxleGrouping,
            AxleGroupId = reference.AxleGroupId,
            AxleGroupCode = reference.AxleGroup?.Code,
            AxleGroupName = reference.AxleGroup?.Name,
            TyreTypeId = reference.TyreTypeId,
            TyreTypeCode = reference.TyreType?.Code,
            TyreTypeName = reference.TyreType?.Name,
            IsActive = reference.IsActive,
            CreatedAt = reference.CreatedAt
        };
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        return userIdClaim != null ? Guid.Parse(userIdClaim.Value) : Guid.Empty;
    }
}
