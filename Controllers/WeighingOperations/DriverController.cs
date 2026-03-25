using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Data.Repositories.Weighing;

namespace TruLoad.Backend.Controllers.WeighingOperations;

/// <summary>
/// Drivers are shared across the system (not tenant/station-scoped). Search returns all drivers; create returns 400 for validation, 409 for duplicates.
/// </summary>
[ApiController]
[Route("api/v1/drivers")]
[Authorize]
[EnableRateLimiting("weighing")]
public class DriverController : ControllerBase
{
    private readonly IDriverRepository _driverRepository;
    private readonly ILogger<DriverController> _logger;
    private readonly TruLoadDbContext _context;

    public DriverController(
        IDriverRepository driverRepository,
        ILogger<DriverController> logger,
        TruLoadDbContext context)
    {
        _driverRepository = driverRepository;
        _logger = logger;
        _context = context;
    }

    [HttpGet("{id}")]
    [HasPermission("driver.read")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var driver = await _driverRepository.GetByIdAsync(id);
        if (driver == null) return NotFound();
        return Ok(driver);
    }

    [HttpGet("search")]
    [HasPermission("driver.read")]
    public async Task<IActionResult> Search([FromQuery] string? query)
    {
        var drivers = await _driverRepository.SearchAsync(query ?? string.Empty);
        return Ok(drivers);
    }

    [HttpGet("id_number/{idNumber}")]
    [HasPermission("driver.read")]
    public async Task<IActionResult> GetByIdNumber(string idNumber)
    {
        var driver = await _driverRepository.GetByIdNumberAsync(idNumber);
        if (driver == null) return NotFound();
        return Ok(driver);
    }

    [HttpGet("license/{licenseNo}")]
    [HasPermission("driver.read")]
    public async Task<IActionResult> GetByLicense(string licenseNo)
    {
        var driver = await _driverRepository.GetByLicenseAsync(licenseNo);
        if (driver == null) return NotFound();
        return Ok(driver);
    }

    [HttpPost]
    [HasPermission("driver.create")]
    public async Task<IActionResult> Create([FromBody] Driver driver)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Validate required name fields
        if (string.IsNullOrWhiteSpace(driver.FullNames))
            return BadRequest("Full names (first name) is required.");
        if (string.IsNullOrWhiteSpace(driver.Surname))
            return BadRequest("Surname (last name) is required.");

        // Normalize empty/whitespace optional strings to null so they don't violate
        // the UNIQUE indexes on id_number and driving_license_no (multiple NULLs are
        // allowed by PostgreSQL, but multiple empty strings are not).
        driver.NtsaId = string.IsNullOrWhiteSpace(driver.NtsaId) ? null : driver.NtsaId.Trim();
        driver.IdNumber = string.IsNullOrWhiteSpace(driver.IdNumber) ? null : driver.IdNumber.Trim();
        driver.DrivingLicenseNo = string.IsNullOrWhiteSpace(driver.DrivingLicenseNo) ? null : driver.DrivingLicenseNo.Trim();
        driver.FullNames = driver.FullNames.Trim();
        driver.Surname = driver.Surname.Trim();

        try
        {
            // Only check for duplicates when the field has a value
            if (driver.IdNumber != null)
            {
                var existing = await _driverRepository.GetByIdNumberAsync(driver.IdNumber);
                if (existing != null)
                    return Conflict($"Driver with ID {driver.IdNumber} already exists.");
            }

            if (driver.DrivingLicenseNo != null)
            {
                var existing = await _driverRepository.GetByLicenseAsync(driver.DrivingLicenseNo);
                if (existing != null)
                    return Conflict($"Driver with License {driver.DrivingLicenseNo} already exists.");
            }

            // Ensure new Id is generated
            if (driver.Id == Guid.Empty)
                driver.Id = Guid.NewGuid();

            var created = await _driverRepository.CreateAsync(driver);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating driver {FullNames} {Surname}", driver.FullNames, driver.Surname);
            return StatusCode(500, "An error occurred while creating the driver.");
        }
    }

    [HttpPut("{id}")]
    [HasPermission("driver.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Driver driver)
    {
        if (id != driver.Id) return BadRequest();

        // Normalize optional fields (same as Create)
        driver.NtsaId = string.IsNullOrWhiteSpace(driver.NtsaId) ? null : driver.NtsaId.Trim();
        driver.IdNumber = string.IsNullOrWhiteSpace(driver.IdNumber) ? null : driver.IdNumber.Trim();
        driver.DrivingLicenseNo = string.IsNullOrWhiteSpace(driver.DrivingLicenseNo) ? null : driver.DrivingLicenseNo.Trim();
        driver.FullNames = driver.FullNames?.Trim() ?? string.Empty;
        driver.Surname = driver.Surname?.Trim() ?? string.Empty;

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
            var topOffenders = await _context.MvDriverDemeritRankings
                .AsNoTracking()
                .OrderByDescending(d => d.TotalCases)
                .Take(limit)
                .Select(d => new
                {
                    name = d.FullName,
                    idNo = d.IdNoOrPassport,
                    totalCases = d.TotalCases,
                    openCases = d.OpenCases,
                    totalFeesCharged = d.TotalFeesCharged,
                    lastViolation = d.LastViolationDate,
                    isRepeatOffender = d.IsRepeatOffender,
                    activeWarrants = d.ActiveWarrants
                })
                .ToListAsync();

            return Ok(topOffenders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top offenders");
            return StatusCode(500, "An error occurred while getting top offenders.");
        }
    }
}
