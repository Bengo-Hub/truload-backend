using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Infrastructure;
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

    [HttpGet("subcounty/{subcountyId}")]
    [ProducesResponseType(typeof(List<Roads>), 200)]
    public async Task<IActionResult> GetBySubcounty(Guid subcountyId)
    {
        var roads = await _repository.GetBySubcountyAsync(subcountyId);
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
    public async Task<IActionResult> Create([FromBody] CreateRoadRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { Message = "Code and Name are required" });

        var existing = await _repository.GetByCodeAsync(request.Code);
        if (existing != null)
            return Conflict(new { Message = $"Road with code {request.Code} already exists" });

        var road = new Roads
        {
            Code = request.Code,
            Name = request.Name,
            RoadClass = request.RoadClass ?? "C",
            TotalLengthKm = request.TotalLengthKm,
        };

        // Create junction records for county and subcounty links
        if (request.RoadCounties?.Count > 0)
        {
            foreach (var rc in request.RoadCounties)
            {
                road.RoadCounties.Add(new RoadCounty { CountyId = Guid.Parse(rc.CountyId) });
            }
        }
        if (request.RoadDistricts?.Count > 0)
        {
            foreach (var rs in request.RoadDistricts)
            {
                road.RoadSubcounties.Add(new Models.Infrastructure.RoadSubcounty { SubcountyId = Guid.Parse(rs.DistrictId) });
            }
        }

        var created = await _repository.CreateAsync(road);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// DTO for creating a road with county/subcounty links.
    /// </summary>
    public class CreateRoadRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? RoadClass { get; set; }
        public decimal? TotalLengthKm { get; set; }
        public List<RoadCountyLink>? RoadCounties { get; set; }
        public List<RoadDistrictLink>? RoadDistricts { get; set; }
    }

    public class RoadCountyLink
    {
        public string CountyId { get; set; } = string.Empty;
    }

    public class RoadDistrictLink
    {
        public string DistrictId { get; set; } = string.Empty;
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
