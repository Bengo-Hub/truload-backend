using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Data.Repositories.Weighing;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DriverController : ControllerBase
{
    private readonly IDriverRepository _driverRepository;

    public DriverController(IDriverRepository driverRepository)
    {
        _driverRepository = driverRepository;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var driver = await _driverRepository.GetByIdAsync(id);
        if (driver == null) return NotFound();
        return Ok(driver);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        var drivers = await _driverRepository.SearchAsync(query);
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
        
        var existing = await _driverRepository.GetByIdNumberAsync(driver.IdNumber);
        if (existing != null)
            return Conflict($"Driver with ID {driver.IdNumber} already exists.");
        
        existing = await _driverRepository.GetByLicenseAsync(driver.DrivingLicenseNo);
        if (existing != null)
            return Conflict($"Driver with License {driver.DrivingLicenseNo} already exists.");

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
}
