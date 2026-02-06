using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Infrastructure;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/cargo-types")]
[Authorize]
public class CargoTypesController : ControllerBase
{
    private readonly ICargoTypesRepository _repository;

    public CargoTypesController(ICargoTypesRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<CargoTypes>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var cargoTypes = await _repository.GetAllAsync(includeInactive);
        return Ok(cargoTypes);
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(List<CargoTypes>), 200)]
    public async Task<IActionResult> GetAllActive()
    {
        var cargoTypes = await _repository.GetAllActiveAsync();
        return Ok(cargoTypes);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CargoTypes), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var cargoType = await _repository.GetByIdAsync(id);
        if (cargoType == null)
            return NotFound(new { Message = $"Cargo type with ID {id} not found" });

        return Ok(cargoType);
    }

    [HttpGet("code/{code}")]
    [ProducesResponseType(typeof(CargoTypes), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var cargoType = await _repository.GetByCodeAsync(code);
        if (cargoType == null)
            return NotFound(new { Message = $"Cargo type with code {code} not found" });

        return Ok(cargoType);
    }

    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(List<CargoTypes>), 200)]
    public async Task<IActionResult> GetByCategory(string category)
    {
        var cargoTypes = await _repository.GetByCategoryAsync(category);
        return Ok(cargoTypes);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:config.manage_taxonomy")]
    [ProducesResponseType(typeof(CargoTypes), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] CargoTypes cargoType)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByCodeAsync(cargoType.Code);
        if (existing != null)
            return Conflict(new { Message = $"Cargo type with code {cargoType.Code} already exists" });

        var created = await _repository.CreateAsync(cargoType);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:config.manage_taxonomy")]
    [ProducesResponseType(typeof(CargoTypes), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CargoTypes cargoType)
    {
        if (id != cargoType.Id)
            return BadRequest(new { Message = "ID mismatch" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Cargo type with ID {id} not found" });

        var duplicate = await _repository.GetByCodeAsync(cargoType.Code);
        if (duplicate != null && duplicate.Id != id)
            return Conflict(new { Message = $"Cargo type with code {cargoType.Code} already exists" });

        var updated = await _repository.UpdateAsync(cargoType);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Permission:config.manage_taxonomy")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _repository.SoftDeleteAsync(id);
        if (!success)
            return NotFound(new { Message = $"Cargo type with ID {id} not found" });

        return NoContent();
    }
}
