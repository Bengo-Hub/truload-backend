using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.User;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;

namespace TruLoad.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class StationsController : ControllerBase
{
    private readonly IStationRepository _stationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<StationsController> _logger;

    public StationsController(
        IStationRepository stationRepository,
        ITenantContext tenantContext,
        ILogger<StationsController> logger)
    {
        _stationRepository = stationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Get the station linked to the current authenticated user.
    /// Returns the user's assigned station based on their stationId from JWT claims.
    /// </summary>
    [HttpGet("my-station")]
    [HasPermission("station.read")]
    [ProducesResponseType(typeof(StationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StationDto>> GetMyStation(CancellationToken cancellationToken)
    {
        // Get user's station ID from claims (added during JWT generation)
        var stationIdClaim = User.FindFirst("station_id")?.Value;

        if (string.IsNullOrEmpty(stationIdClaim) || !Guid.TryParse(stationIdClaim, out var stationId))
        {
            _logger.LogWarning("User {UserId} does not have a linked station", User.FindFirst("sub")?.Value);
            return NotFound(new { message = "No station linked to current user" });
        }

        var station = await _stationRepository.GetByIdAsync(stationId, cancellationToken);
        if (station == null)
        {
            _logger.LogWarning("Station {StationId} linked to user not found", stationId);
            return NotFound(new { message = "Linked station not found" });
        }

        _logger.LogDebug("Returning user's linked station: {StationCode}", station.Code);
        return Ok(MapToDto(station));
    }

    [HttpGet("{id:guid}")]
    [HasPermission("station.read")]
    [ProducesResponseType(typeof(StationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StationDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var station = await _stationRepository.GetByIdAsync(id, cancellationToken);
        if (station == null)
        {
            return NotFound(new { message = "Station not found" });
        }

        return Ok(MapToDto(station));
    }

    [HttpGet]
    [HasPermission("station.read")]
    [ProducesResponseType(typeof(IEnumerable<StationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StationDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var stations = await _stationRepository.GetAllAsync(includeInactive, cancellationToken);
        return Ok(stations.Select(MapToDto));
    }

    [HttpGet("type/{stationType}")]
    [HasPermission("station.read")]
    [ProducesResponseType(typeof(IEnumerable<StationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StationDto>>> GetByType(string stationType, CancellationToken cancellationToken)
    {
        var stations = await _stationRepository.GetByTypeAsync(stationType, cancellationToken);
        return Ok(stations.Select(MapToDto));
    }

    [HttpGet("organization/{organizationId:guid}")]
    [HasPermission("station.read")]
    [ProducesResponseType(typeof(IEnumerable<StationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StationDto>>> GetByOrganization(Guid organizationId, CancellationToken cancellationToken)
    {
        var stations = await _stationRepository.GetByOrganizationIdAsync(organizationId, cancellationToken);
        return Ok(stations.Select(MapToDto));
    }

    [HttpPost]
    [HasPermission("station.create")]
    [ProducesResponseType(typeof(StationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StationDto>> Create([FromBody] CreateStationRequest request, CancellationToken cancellationToken)
    {
        // Check if code already exists
        if (await _stationRepository.CodeExistsAsync(request.Code, cancellationToken: cancellationToken))
        {
            return BadRequest(new { message = "Station code already exists" });
        }

        var station = new Station
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            StationType = request.StationType,
            OrganizationId = request.OrganizationId,
            Location = request.Location,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            SupportsBidirectional = request.SupportsBidirectional,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _stationRepository.CreateAsync(station, cancellationToken);
        _logger.LogInformation("Station created: {StationId}, Code: {Code}, Type: {Type}", created.Id, created.Code, created.StationType);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    [HttpPut("{id:guid}")]
    [HasPermission("station.update")]
    [ProducesResponseType(typeof(StationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StationDto>> Update(Guid id, [FromBody] UpdateStationRequest request, CancellationToken cancellationToken)
    {
        var station = await _stationRepository.GetByIdAsync(id, cancellationToken);
        if (station == null)
        {
            return NotFound(new { message = "Station not found" });
        }

        if (request.Code != null && request.Code != station.Code)
        {
            if (await _stationRepository.CodeExistsAsync(request.Code, id, cancellationToken))
            {
                return BadRequest(new { message = "Station code already exists" });
            }
            station.Code = request.Code;
        }

        if (request.Name != null) station.Name = request.Name;
        if (request.StationType != null) station.StationType = request.StationType;
        if (request.Location != null) station.Location = request.Location;
        if (request.Latitude.HasValue) station.Latitude = request.Latitude;
        if (request.Longitude.HasValue) station.Longitude = request.Longitude;
        if (request.SupportsBidirectional.HasValue) station.SupportsBidirectional = request.SupportsBidirectional.Value;
        if (request.IsActive.HasValue) station.IsActive = request.IsActive.Value;

        var updated = await _stationRepository.UpdateAsync(station, cancellationToken);
        _logger.LogInformation("Station updated: {StationId}", updated.Id);

        return Ok(MapToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("station.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var station = await _stationRepository.GetByIdAsync(id, cancellationToken);
        if (station == null)
        {
            return NotFound(new { message = "Station not found" });
        }

        await _stationRepository.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Station deleted: {StationId}", id);

        return NoContent();
    }

    private static StationDto MapToDto(Station station)
    {
        return new StationDto
        {
            Id = station.Id,
            Code = station.Code,
            Name = station.Name,
            StationType = station.StationType,
            OrganizationId = station.OrganizationId,
            OrganizationName = station.Organization?.Name,
            Location = station.Location,
            Latitude = station.Latitude,
            Longitude = station.Longitude,
            SupportsBidirectional = station.SupportsBidirectional,
            IsActive = station.IsActive,
            CreatedAt = station.CreatedAt,
            UpdatedAt = station.UpdatedAt
        };
    }
}




