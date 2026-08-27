using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Infrastructure;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.DTOs.Infrastructure;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/origins-destinations")]
[Authorize]
public class OriginsDestinationsController : ControllerBase
{
    private readonly IOriginsDestinationsRepository _repository;
    private readonly ITenantContext _tenantContext;

    public OriginsDestinationsController(IOriginsDestinationsRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
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
    public async Task<IActionResult> Create([FromBody] CreateOriginDestinationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Auto-stamp the caller's org unless a Superuser explicitly asked for a shared/global entry
        // (MakeGlobal), or the caller is a Superuser with no resolvable tenant context at all (e.g.
        // cross-tenant mode - see TenantContext memory) - there is nothing to stamp in that case
        // either, so the row is created as shared/global by necessity.
        var isSuperuser = User.IsInRole("Superuser");
        Guid? organizationId = isSuperuser && request.MakeGlobal
            ? null
            : (_tenantContext.OrganizationId != Guid.Empty ? _tenantContext.OrganizationId : null);

        // Duplicate-code check is scoped: a code already used in the shared catalog or in this
        // org's own private catalog is a conflict, but a code that only exists in a DIFFERENT
        // org's private catalog is not - it mirrors the two filtered unique indexes on
        // (OrganizationId, Code).
        if (await _repository.CodeExistsInScopeAsync(request.Code, organizationId))
        {
            var scope = organizationId == null ? "the shared catalog" : "your organisation";
            return Conflict(new { Message = $"A location with code {request.Code} already exists in {scope}" });
        }

        var location = new OriginsDestinations
        {
            Code = request.Code,
            Name = request.Name,
            LocationType = request.LocationType,
            Country = request.Country,
            OrganizationId = organizationId
        };

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

        // Preserve the existing OrganizationId unless the caller is a Superuser explicitly changing
        // it - a regular tenant admin with config.manage_taxonomy should not be able to move/re-scope
        // a row to another org or make it shared/global just by round-tripping the entity on Update
        // (same superuser-only bypass rule Create enforces above).
        var effectiveOrganizationId = User.IsInRole("Superuser") ? location.OrganizationId : existing.OrganizationId;

        // Duplicate-code check is scoped to the row's effective (post-update) organization - same
        // rationale as Create, mirroring the two filtered unique indexes on (OrganizationId, Code).
        if (await _repository.CodeExistsInScopeAsync(location.Code, effectiveOrganizationId, excludeId: id))
        {
            var scope = effectiveOrganizationId == null ? "the shared catalog" : "your organisation";
            return Conflict(new { Message = $"A location with code {location.Code} already exists in {scope}" });
        }

        location.OrganizationId = effectiveOrganizationId;

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
