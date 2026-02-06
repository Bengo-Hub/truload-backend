using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruLoad.Backend.Models.Infrastructure;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Repositories.Infrastructure;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;
using TruLoad.Backend.DTOs.Weighing;
using System.Security.Claims;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/scale-tests")]
[Authorize]
[EnableRateLimiting("weighing")]
public class ScaleTestsController : ControllerBase
{
    private readonly IScaleTestRepository _scaleTestRepository;
    private readonly IStationRepository _stationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ScaleTestsController> _logger;

    public ScaleTestsController(
        IScaleTestRepository scaleTestRepository,
        IStationRepository stationRepository,
        ITenantContext tenantContext,
        ILogger<ScaleTestsController> logger)
    {
        _scaleTestRepository = scaleTestRepository;
        _stationRepository = stationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Check scale test status for current user's station and bound.
    /// Returns whether weighing is allowed.
    /// </summary>
    [HttpGet("my-station/status")]
    [ProducesResponseType(typeof(ScaleTestStatusDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetMyStationStatus([FromQuery] string? bound)
    {
        if (!_tenantContext.StationId.HasValue)
            return BadRequest(new { Message = "No station assigned to current user" });

        var stationId = _tenantContext.StationId.Value;
        var hasPassed = await _scaleTestRepository.HasPassedDailyCalibrationalAsync(stationId, bound);
        var latestTest = await _scaleTestRepository.GetLatestByStationAsync(stationId, bound);

        var status = new ScaleTestStatusDto
        {
            StationId = stationId,
            Bound = bound,
            HasValidTest = hasPassed,
            WeighingAllowed = hasPassed,
            LatestTest = latestTest != null ? MapToDto(latestTest) : null,
            Message = hasPassed
                ? "Scale test passed - weighing allowed"
                : "No valid scale test for today - perform scale test before weighing"
        };

        return Ok(status);
    }

    /// <summary>
    /// Get all scale tests for the current user's station
    /// </summary>
    [HttpGet("my-station")]
    [ProducesResponseType(typeof(List<ScaleTestDto>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetMyStationTests([FromQuery] string? bound)
    {
        if (!_tenantContext.StationId.HasValue)
            return BadRequest(new { Message = "No station assigned to current user" });

        var tests = await _scaleTestRepository.GetByStationAsync(_tenantContext.StationId.Value, bound);
        return Ok(tests.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Get latest scale test for the current user's station
    /// </summary>
    [HttpGet("my-station/latest")]
    [ProducesResponseType(typeof(ScaleTestDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMyLatestTest([FromQuery] string? bound)
    {
        if (!_tenantContext.StationId.HasValue)
            return BadRequest(new { Message = "No station assigned to current user" });

        var test = await _scaleTestRepository.GetLatestByStationAsync(_tenantContext.StationId.Value, bound);
        if (test == null)
            return NotFound(new { Message = "No scale tests found for your station" });

        return Ok(MapToDto(test));
    }

    /// <summary>
    /// Get all scale tests for a station
    /// </summary>
    [HttpGet("station/{stationId}")]
    [ProducesResponseType(typeof(List<ScaleTestDto>), 200)]
    public async Task<IActionResult> GetByStation(Guid stationId, [FromQuery] string? bound)
    {
        var tests = await _scaleTestRepository.GetByStationAsync(stationId, bound);
        return Ok(tests.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Get latest scale test for a station
    /// </summary>
    [HttpGet("station/{stationId}/latest")]
    [ProducesResponseType(typeof(ScaleTestDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetLatestByStation(Guid stationId, [FromQuery] string? bound)
    {
        var test = await _scaleTestRepository.GetLatestByStationAsync(stationId, bound);
        if (test == null)
            return NotFound(new { Message = $"No scale tests found for station {stationId}" });

        return Ok(MapToDto(test));
    }

    /// <summary>
    /// Get scale test by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ScaleTestDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var test = await _scaleTestRepository.GetByIdAsync(id);
        if (test == null)
            return NotFound(new { Message = $"Scale test with ID {id} not found" });

        return Ok(MapToDto(test));
    }

    /// <summary>
    /// Check scale test status for a station and bound.
    /// Returns whether weighing is allowed.
    /// </summary>
    [HttpGet("station/{stationId}/status")]
    [ProducesResponseType(typeof(ScaleTestStatusDto), 200)]
    public async Task<IActionResult> GetStationStatus(Guid stationId, [FromQuery] string? bound)
    {
        var hasPassed = await _scaleTestRepository.HasPassedDailyCalibrationalAsync(stationId, bound);
        var latestTest = await _scaleTestRepository.GetLatestByStationAsync(stationId, bound);

        var status = new ScaleTestStatusDto
        {
            StationId = stationId,
            Bound = bound,
            HasValidTest = hasPassed,
            WeighingAllowed = hasPassed,
            LatestTest = latestTest != null ? MapToDto(latestTest) : null,
            Message = hasPassed
                ? "Scale test passed - weighing allowed"
                : "No valid scale test for today - perform scale test before weighing"
        };

        return Ok(status);
    }

    /// <summary>
    /// Get scale tests within date range for a station
    /// </summary>
    [HttpGet("station/{stationId}/range")]
    [ProducesResponseType(typeof(List<ScaleTestDto>), 200)]
    public async Task<IActionResult> GetByDateRange(
        Guid stationId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] string? bound)
    {
        if (fromDate > toDate)
            return BadRequest(new { Message = "From date cannot be after to date" });

        var tests = await _scaleTestRepository.GetByDateRangeAsync(stationId, fromDate, toDate, bound);
        return Ok(tests.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Check if station has passed daily calibration (within last 24 hours)
    /// </summary>
    [HttpGet("station/{stationId}/daily-check")]
    [ProducesResponseType(typeof(ScaleTestStatusDto), 200)]
    public async Task<IActionResult> CheckDailyCalibration(Guid stationId, [FromQuery] string? bound)
    {
        var hasPassed = await _scaleTestRepository.HasPassedDailyCalibrationalAsync(stationId, bound);
        var latestTest = await _scaleTestRepository.GetLatestByStationAsync(stationId, bound);

        var status = new ScaleTestStatusDto
        {
            StationId = stationId,
            Bound = bound,
            HasValidTest = hasPassed,
            WeighingAllowed = hasPassed,
            LatestTest = latestTest != null ? MapToDto(latestTest) : null,
            Message = hasPassed
                ? "Station has passed daily calibration check"
                : "Station has NOT passed daily calibration check - weighing operations locked"
        };

        return Ok(status);
    }

    /// <summary>
    /// Get all failed tests for a station
    /// </summary>
    [HttpGet("station/{stationId}/failed")]
    [ProducesResponseType(typeof(List<ScaleTestDto>), 200)]
    public async Task<IActionResult> GetFailedTests(Guid stationId, [FromQuery] string? bound)
    {
        var tests = await _scaleTestRepository.GetFailedTestsAsync(stationId, bound);
        return Ok(tests.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Get scale tests carried out by current user
    /// </summary>
    [HttpGet("my-tests")]
    [ProducesResponseType(typeof(List<ScaleTestDto>), 200)]
    public async Task<IActionResult> GetMyTests()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { Message = "User ID not found in token" });

        var tests = await _scaleTestRepository.GetByUserAsync(userId);
        return Ok(tests.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Create new scale test record
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ScaleTestDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Create([FromBody] CreateScaleTestRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Verify station exists
        var station = await _stationRepository.GetByIdAsync(request.StationId);
        if (station == null)
            return NotFound(new { Message = $"Station with ID {request.StationId} not found" });

        // Get current user ID
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { Message = "User ID not found in token" });

        // Normalize result to lowercase
        var result = request.Result.ToLower();
        if (result != "pass" && result != "fail")
            return BadRequest(new { Message = "Result must be 'pass' or 'fail'" });

        var scaleTest = new ScaleTest
        {
            Id = Guid.NewGuid(),
            StationId = request.StationId,
            Bound = request.Bound,
            TestType = request.TestType ?? "calibration_weight",
            VehiclePlate = request.VehiclePlate,
            WeighingMode = request.WeighingMode,
            TestWeightKg = request.TestWeightKg,
            ActualWeightKg = request.ActualWeightKg,
            Result = result,
            DeviationKg = request.DeviationKg,
            Details = request.Details ?? string.Empty,
            CarriedAt = DateTime.UtcNow,
            CarriedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _scaleTestRepository.CreateAsync(scaleTest);
        _logger.LogInformation(
            "Scale test created: {TestId} for Station {StationId}, Bound {Bound}, Result: {Result}",
            created.Id, created.StationId, created.Bound, created.Result);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    /// <summary>
    /// Delete scale test (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "Permission:weighing.scale_test")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _scaleTestRepository.SoftDeleteAsync(id);
        if (!success)
            return NotFound(new { Message = $"Scale test with ID {id} not found" });

        return NoContent();
    }

    private ScaleTestDto MapToDto(ScaleTest test)
    {
        return new ScaleTestDto
        {
            Id = test.Id,
            StationId = test.StationId,
            StationName = test.Station?.Name,
            StationCode = test.Station?.Code,
            Bound = test.Bound,
            TestType = test.TestType,
            VehiclePlate = test.VehiclePlate,
            WeighingMode = test.WeighingMode,
            TestWeightKg = test.TestWeightKg,
            ActualWeightKg = test.ActualWeightKg,
            Result = test.Result,
            DeviationKg = test.DeviationKg,
            Details = test.Details,
            CarriedAt = test.CarriedAt,
            CarriedById = test.CarriedById,
            CarriedByName = test.CarriedBy?.FullName,
            CreatedAt = test.CreatedAt,
            UpdatedAt = test.UpdatedAt
        };
    }
}
