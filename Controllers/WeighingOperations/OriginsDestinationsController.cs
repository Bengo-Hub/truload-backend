using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Infrastructure;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/origins-destinations")]
[Authorize]
public class OriginsDestinationsController : ControllerBase
{
    private readonly IOriginsDestinationsRepository _repository;

    public OriginsDestinationsController(IOriginsDestinationsRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<OriginsDestinations>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var locations = await _repository.GetAllAsync(includeInactive);
        return Ok(locations);
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(List<OriginsDestinations>), 200)]
    public async Task<IActionResult> GetAllActive()
    {
        var locations = await _repository.GetAllActiveAsync();
        return Ok(locations);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OriginsDestinations), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var location = await _repository.GetByIdAsync(id);
        if (location == null)
            return NotFound(new { Message = $"Location with ID {id} not found" });

        return Ok(location);
    }

    [HttpGet("code/{code}")]
    [ProducesResponseType(typeof(OriginsDestinations), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var location = await _repository.GetByCodeAsync(code);
        if (location == null)
            return NotFound(new { Message = $"Location with code {code} not found" });

        return Ok(location);
    }

    [HttpGet("country/{country}")]
    [ProducesResponseType(typeof(List<OriginsDestinations>), 200)]
    public async Task<IActionResult> GetByCountry(string country)
    {
        var locations = await _repository.GetByCountryAsync(country);
        return Ok(locations);
    }

    [HttpGet("type/{locationType}")]
    [ProducesResponseType(typeof(List<OriginsDestinations>), 200)]
    public async Task<IActionResult> GetByLocationType(string locationType)
    {
        var locations = await _repository.GetByLocationTypeAsync(locationType);
        return Ok(locations);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:config.manage_taxonomy")]
    [ProducesResponseType(typeof(OriginsDestinations), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] OriginsDestinations location)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByCodeAsync(location.Code);
        if (existing != null)
            return Conflict(new { Message = $"Location with code {location.Code} already exists" });

        var created = await _repository.CreateAsync(location);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:config.manage_taxonomy")]
    [ProducesResponseType(typeof(OriginsDestinations), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] OriginsDestinations location)
    {
        if (id != location.Id)
            return BadRequest(new { Message = "ID mismatch" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Location with ID {id} not found" });

        var duplicate = await _repository.GetByCodeAsync(location.Code);
        if (duplicate != null && duplicate.Id != id)
            return Conflict(new { Message = $"Location with code {location.Code} already exists" });

        var updated = await _repository.UpdateAsync(location);
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
            return NotFound(new { Message = $"Location with ID {id} not found" });

        return NoContent();
    }
}
