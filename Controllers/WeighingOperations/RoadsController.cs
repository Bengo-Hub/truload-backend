using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Infrastructure;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/roads")]
[Authorize]
public class RoadsController : ControllerBase
{
    private readonly IRoadsRepository _repository;

    public RoadsController(IRoadsRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Get roads with pagination. Default page size 50.
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResponse<Roads>), 200)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 1000);
        pageNumber = Math.Max(1, pageNumber);
        var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize, includeInactive, search, cancellationToken);
        return Ok(PagedResponse<Roads>.Create(items, totalCount, pageNumber, pageSize));
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Roads>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var roads = await _repository.GetAllAsync(includeInactive);
        return Ok(roads);
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(List<Roads>), 200)]
    public async Task<IActionResult> GetAllActive()
    {
        var roads = await _repository.GetAllActiveAsync();
        return Ok(roads);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Roads), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var road = await _repository.GetByIdAsync(id);
        if (road == null)
            return NotFound(new { Message = $"Road with ID {id} not found" });

        return Ok(road);
    }

    [HttpGet("code/{code}")]
    [ProducesResponseType(typeof(Roads), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var road = await _repository.GetByCodeAsync(code);
        if (road == null)
            return NotFound(new { Message = $"Road with code {code} not found" });

        return Ok(road);
    }

    [HttpGet("class/{roadClass}")]
    [ProducesResponseType(typeof(List<Roads>), 200)]
    public async Task<IActionResult> GetByRoadClass(string roadClass)
    {
        var roads = await _repository.GetByRoadClassAsync(roadClass);
        return Ok(roads);
    }

    [HttpGet("district/{districtId}")]
    [ProducesResponseType(typeof(List<Roads>), 200)]
    public async Task<IActionResult> GetByDistrict(Guid districtId)
    {
        var roads = await _repository.GetByDistrictAsync(districtId);
        return Ok(roads);
    }

    [HttpGet("county/{countyId}")]
    [ProducesResponseType(typeof(List<Roads>), 200)]
    public async Task<IActionResult> GetByCounty(Guid countyId)
    {
        var roads = await _repository.GetByCountyAsync(countyId);
        return Ok(roads);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:config.manage_taxonomy")]
    [ProducesResponseType(typeof(Roads), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] Roads road)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByCodeAsync(road.Code);
        if (existing != null)
            return Conflict(new { Message = $"Road with code {road.Code} already exists" });

        var created = await _repository.CreateAsync(road);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:config.manage_taxonomy")]
    [ProducesResponseType(typeof(Roads), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] Roads road)
    {
        if (id != road.Id)
            return BadRequest(new { Message = "ID mismatch" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Road with ID {id} not found" });

        var duplicate = await _repository.GetByCodeAsync(road.Code);
        if (duplicate != null && duplicate.Id != id)
            return Conflict(new { Message = $"Road with code {road.Code} already exists" });

        var updated = await _repository.UpdateAsync(road);
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
            return NotFound(new { Message = $"Road with ID {id} not found" });

        return NoContent();
    }
}
