using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.Infrastructure;
using TruLoad.Backend.Repositories.Infrastructure;

namespace TruLoad.Backend.Controllers.WeighingOperations;

/// <summary>
/// Controller for vehicle makes master data.
/// Provides CRUD operations for vehicle manufacturers (Mercedes-Benz, Volvo, etc.)
/// </summary>
[ApiController]
[Route("api/v1/vehicle-makes")]
[Authorize]
public class VehicleMakesController : ControllerBase
{
    private readonly IVehicleMakesRepository _repository;

    public VehicleMakesController(IVehicleMakesRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Get all vehicle makes, optionally including inactive ones.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<VehicleMake>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var makes = await _repository.GetAllAsync(includeInactive);
        return Ok(makes);
    }

    /// <summary>
    /// Get all active vehicle makes.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(List<VehicleMake>), 200)]
    public async Task<IActionResult> GetAllActive()
    {
        var makes = await _repository.GetAllActiveAsync();
        return Ok(makes);
    }

    /// <summary>
    /// Get a vehicle make by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(VehicleMake), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var make = await _repository.GetByIdAsync(id);
        if (make == null)
            return NotFound(new { Message = $"Vehicle make with ID {id} not found" });

        return Ok(make);
    }

    /// <summary>
    /// Get a vehicle make by code.
    /// </summary>
    [HttpGet("code/{code}")]
    [ProducesResponseType(typeof(VehicleMake), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var make = await _repository.GetByCodeAsync(code);
        if (make == null)
            return NotFound(new { Message = $"Vehicle make with code {code} not found" });

        return Ok(make);
    }

    /// <summary>
    /// Get vehicle makes by country of origin.
    /// </summary>
    [HttpGet("country/{country}")]
    [ProducesResponseType(typeof(List<VehicleMake>), 200)]
    public async Task<IActionResult> GetByCountry(string country)
    {
        var makes = await _repository.GetByCountryAsync(country);
        return Ok(makes);
    }

    /// <summary>
    /// Create a new vehicle make.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(VehicleMake), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] CreateVehicleMakeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByCodeAsync(request.Code);
        if (existing != null)
            return Conflict(new { Message = $"Vehicle make with code {request.Code} already exists" });

        var make = new VehicleMake
        {
            Code = request.Code,
            Name = request.Name,
            Country = request.Country,
            Description = request.Description,
            IsActive = true
        };

        try
        {
            var created = await _repository.CreateAsync(make);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("idx_vehicle_makes_code") == true
                                         || ex.InnerException?.Message.Contains("duplicate key") == true)
        {
            return Conflict(new { Message = $"Vehicle make with code {request.Code} already exists" });
        }
    }

    /// <summary>
    /// Update an existing vehicle make.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(VehicleMake), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVehicleMakeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Vehicle make with ID {id} not found" });

        // Check for duplicate code if code is being changed
        if (!string.IsNullOrEmpty(request.Code) && request.Code != existing.Code)
        {
            var duplicate = await _repository.GetByCodeAsync(request.Code);
            if (duplicate != null)
                return Conflict(new { Message = $"Vehicle make with code {request.Code} already exists" });
            existing.Code = request.Code;
        }

        if (!string.IsNullOrEmpty(request.Name))
            existing.Name = request.Name;

        if (request.Country != null)
            existing.Country = request.Country;

        if (request.Description != null)
            existing.Description = request.Description;

        if (request.IsActive.HasValue)
            existing.IsActive = request.IsActive.Value;

        var updated = await _repository.UpdateAsync(existing);
        return Ok(updated);
    }

    /// <summary>
    /// Soft delete a vehicle make.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _repository.SoftDeleteAsync(id);
        if (!success)
            return NotFound(new { Message = $"Vehicle make with ID {id} not found" });

        return NoContent();
    }
}

/// <summary>
/// Request DTO for creating a vehicle make.
/// </summary>
public class CreateVehicleMakeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Request DTO for updating a vehicle make.
/// </summary>
public class UpdateVehicleMakeRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Country { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}
