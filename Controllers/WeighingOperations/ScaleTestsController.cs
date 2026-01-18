using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Models.Infrastructure;
using TruLoad.Backend.Repositories.Infrastructure;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;
using System.Security.Claims;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/scale-tests")]
[Authorize]
public class ScaleTestsController : ControllerBase
{
    private readonly IScaleTestRepository _scaleTestRepository;
    private readonly IStationRepository _stationRepository;

    public ScaleTestsController(
        IScaleTestRepository scaleTestRepository,
        IStationRepository stationRepository)
    {
        _scaleTestRepository = scaleTestRepository;
        _stationRepository = stationRepository;
    }

    /// <summary>
    /// Get all scale tests for a station
    /// </summary>
    [HttpGet("station/{stationId}")]
    [ProducesResponseType(typeof(List<ScaleTest>), 200)]
    public async Task<IActionResult> GetByStation(Guid stationId)
    {
        var tests = await _scaleTestRepository.GetByStationAsync(stationId);
        return Ok(tests);
    }

    /// <summary>
    /// Get latest scale test for a station
    /// </summary>
    [HttpGet("station/{stationId}/latest")]
    [ProducesResponseType(typeof(ScaleTest), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetLatestByStation(Guid stationId)
    {
        var test = await _scaleTestRepository.GetLatestByStationAsync(stationId);
        if (test == null)
            return NotFound(new { Message = $"No scale tests found for station {stationId}" });

        return Ok(test);
    }

    /// <summary>
    /// Get scale test by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ScaleTest), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var test = await _scaleTestRepository.GetByIdAsync(id);
        if (test == null)
            return NotFound(new { Message = $"Scale test with ID {id} not found" });

        return Ok(test);
    }

    /// <summary>
    /// Get scale tests within date range for a station
    /// </summary>
    [HttpGet("station/{stationId}/range")]
    [ProducesResponseType(typeof(List<ScaleTest>), 200)]
    public async Task<IActionResult> GetByDateRange(
        Guid stationId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        if (fromDate > toDate)
            return BadRequest(new { Message = "From date cannot be after to date" });

        var tests = await _scaleTestRepository.GetByDateRangeAsync(stationId, fromDate, toDate);
        return Ok(tests);
    }

    /// <summary>
    /// Check if station has passed daily calibration (within last 24 hours)
    /// </summary>
    [HttpGet("station/{stationId}/daily-check")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> CheckDailyCalibration(Guid stationId)
    {
        var hasPassed = await _scaleTestRepository.HasPassedDailyCalibrationalAsync(stationId);
        var latestTest = await _scaleTestRepository.GetLatestByStationAsync(stationId);

        return Ok(new
        {
            StationId = stationId,
            HasPassedDailyCalibration = hasPassed,
            LatestTest = latestTest,
            Message = hasPassed
                ? "Station has passed daily calibration check"
                : "Station has NOT passed daily calibration check - weighing operations locked"
        });
    }

    /// <summary>
    /// Get all failed tests for a station
    /// </summary>
    [HttpGet("station/{stationId}/failed")]
    [ProducesResponseType(typeof(List<ScaleTest>), 200)]
    public async Task<IActionResult> GetFailedTests(Guid stationId)
    {
        var tests = await _scaleTestRepository.GetFailedTestsAsync(stationId);
        return Ok(tests);
    }

    /// <summary>
    /// Get scale tests carried out by current user
    /// </summary>
    [HttpGet("my-tests")]
    [ProducesResponseType(typeof(List<ScaleTest>), 200)]
    public async Task<IActionResult> GetMyTests()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { Message = "User ID not found in token" });

        var tests = await _scaleTestRepository.GetByUserAsync(userId);
        return Ok(tests);
    }

    /// <summary>
    /// Create new scale test record
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireCreateScaleTestPermission")]
    [ProducesResponseType(typeof(ScaleTest), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Create([FromBody] ScaleTest scaleTest)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Verify station exists
        var station = await _stationRepository.GetByIdAsync(scaleTest.StationId);
        if (station == null)
            return NotFound(new { Message = $"Station with ID {scaleTest.StationId} not found" });

        // Set carried by from current user if not provided
        if (scaleTest.CarriedById == Guid.Empty)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { Message = "User ID not found in token" });

            scaleTest.CarriedById = userId;
        }

        // Normalize result to lowercase
        scaleTest.Result = scaleTest.Result.ToLower();
        if (scaleTest.Result != "pass" && scaleTest.Result != "fail")
            return BadRequest(new { Message = "Result must be 'pass' or 'fail'" });

        var created = await _scaleTestRepository.CreateAsync(scaleTest);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Update existing scale test
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireUpdateScaleTestPermission")]
    [ProducesResponseType(typeof(ScaleTest), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ScaleTest scaleTest)
    {
        if (id != scaleTest.Id)
            return BadRequest(new { Message = "ID mismatch" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _scaleTestRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Scale test with ID {id} not found" });

        // Normalize result
        scaleTest.Result = scaleTest.Result.ToLower();
        if (scaleTest.Result != "pass" && scaleTest.Result != "fail")
            return BadRequest(new { Message = "Result must be 'pass' or 'fail'" });

        var updated = await _scaleTestRepository.UpdateAsync(scaleTest);
        return Ok(updated);
    }

    /// <summary>
    /// Delete scale test (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireDeleteScaleTestPermission")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _scaleTestRepository.SoftDeleteAsync(id);
        if (!success)
            return NotFound(new { Message = $"Scale test with ID {id} not found" });

        return NoContent();
    }
}
