using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Data.Repositories.Weighing;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[EnableRateLimiting("weighing")]
public class VehicleController : ControllerBase
{
    private readonly IVehicleRepository _vehicleRepository;

    public VehicleController(IVehicleRepository vehicleRepository)
    {
        _vehicleRepository = vehicleRepository;
    }

    [HttpGet("{id}")]
    [HasPermission("vehicle.read")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id);
        if (vehicle == null) return NotFound();
        return Ok(vehicle);
    }

    [HttpGet("search")]
    [HasPermission("vehicle.read")]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        var vehicles = await _vehicleRepository.SearchAsync(query);
        return Ok(vehicles);
    }

    [HttpGet("reg/{regNo}")]
    [HasPermission("vehicle.read")]
    public async Task<IActionResult> GetByRegNo(string regNo)
    {
        var vehicle = await _vehicleRepository.GetByRegNoAsync(regNo);
        if (vehicle == null) return NotFound();
        return Ok(vehicle);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:vehicle.create")]
    public async Task<IActionResult> Create([FromBody] Vehicle vehicle)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        
        var existing = await _vehicleRepository.GetByRegNoAsync(vehicle.RegNo);
        if (existing != null)
            return Conflict($"Vehicle with RegNo {vehicle.RegNo} already exists.");

        var created = await _vehicleRepository.CreateAsync(vehicle);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:vehicle.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Vehicle vehicle)
    {
        if (id != vehicle.Id) return BadRequest();
        
        await _vehicleRepository.UpdateAsync(vehicle);
        return NoContent();
    }
}
