using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Data.Repositories.Weighing;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/drivers")]
[Authorize]
[EnableRateLimiting("weighing")]
public class DriverController : ControllerBase
{
    private readonly IDriverRepository _driverRepository;
    private readonly ILogger<DriverController> _logger;

    public DriverController(
        IDriverRepository driverRepository,
        ILogger<DriverController> logger)
    {
        _driverRepository = driverRepository;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var driver = await _driverRepository.GetByIdAsync(id);
        if (driver == null) return NotFound();
        return Ok(driver);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? query)
    {
        var drivers = await _driverRepository.SearchAsync(query ?? string.Empty);
        return Ok(drivers);
    }

    [HttpGet("id_number/{idNumber}")]
    public async Task<IActionResult> GetByIdNumber(string idNumber)
    {
        var driver = await _driverRepository.GetByIdNumberAsync(idNumber);
        if (driver == null) return NotFound();
        return Ok(driver);
    }

    [HttpGet("license/{licenseNo}")]
    public async Task<IActionResult> GetByLicense(string licenseNo)
    {
        var driver = await _driverRepository.GetByLicenseAsync(licenseNo);
        if (driver == null) return NotFound();
        return Ok(driver);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Driver driver)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Validate required name fields
        if (string.IsNullOrWhiteSpace(driver.FullNames))
            return BadRequest("Full names (first name) is required.");
        if (string.IsNullOrWhiteSpace(driver.Surname))
            return BadRequest("Surname (last name) is required.");

        // Only check for duplicates when the field has a value (skip empty strings)
        if (!string.IsNullOrWhiteSpace(driver.IdNumber))
        {
            var existing = await _driverRepository.GetByIdNumberAsync(driver.IdNumber);
            if (existing != null)
                return Conflict($"Driver with ID {driver.IdNumber} already exists.");
        }

        if (!string.IsNullOrWhiteSpace(driver.DrivingLicenseNo))
        {
            var existing = await _driverRepository.GetByLicenseAsync(driver.DrivingLicenseNo);
            if (existing != null)
                return Conflict($"Driver with License {driver.DrivingLicenseNo} already exists.");
        }

        var created = await _driverRepository.CreateAsync(driver);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Driver driver)
    {
        if (id != driver.Id) return BadRequest();
        
        await _driverRepository.UpdateAsync(driver);
        return NoContent();
    }

    /// <summary>
    /// Get top repeat offenders by demerit points
    /// </summary>
    [HttpGet("top-offenders")]
    public async Task<IActionResult> GetTopOffenders(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int limit = 10)
    {
        try
        {
            var drivers = await _driverRepository.SearchAsync(string.Empty);
            
            // Return drivers sorted by demerit points
            var topOffenders = drivers
                .OrderByDescending(d => d.CurrentDemeritPoints)
                .Take(limit)
                .Select(d => new
                {
                    name = $"{d.FullNames} {d.Surname}".Trim(),
                    points = d.CurrentDemeritPoints,
                    violations = d.DemeritRecords?.Count ?? 0
                })
                .ToList();

            return Ok(topOffenders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top offenders");
            return StatusCode(500, "An error occurred while getting top offenders.");
        }
    }
}
