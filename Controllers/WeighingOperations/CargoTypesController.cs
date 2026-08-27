using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Infrastructure;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.DTOs.Infrastructure;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/cargo-types")]
[Authorize]
public class CargoTypesController : ControllerBase
{
    private readonly ICargoTypesRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CargoTypesController(ICargoTypesRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
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
    public async Task<IActionResult> Create([FromBody] CreateCargoTypeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByCodeAsync(request.Code);
        if (existing != null)
            return Conflict(new { Message = $"Cargo type with code {request.Code} already exists" });

        // Auto-stamp the caller's org unless a Superuser explicitly asked for a shared/global entry
        // (MakeGlobal), or the caller is a Superuser with no resolvable tenant context at all (e.g.
        // cross-tenant mode - see TenantContext memory) - there is nothing to stamp in that case
        // either, so the row is created as shared/global by necessity.
        var isSuperuser = User.IsInRole("Superuser");
        Guid? organizationId = isSuperuser && request.MakeGlobal
            ? null
            : (_tenantContext.OrganizationId != Guid.Empty ? _tenantContext.OrganizationId : null);

        var cargoType = new CargoTypes
        {
            Code = request.Code,
            Name = request.Name,
            Category = request.Category,
            MoistureTargetPercent = request.MoistureTargetPercent,
            ForeignMatterLimitPercent = request.ForeignMatterLimitPercent,
            OrganizationId = organizationId
        };

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

        // Preserve the existing OrganizationId unless the caller is a Superuser explicitly changing
        // it - a regular tenant admin with config.manage_taxonomy should not be able to move/re-scope
        // a row to another org or make it shared/global just by round-tripping the entity on Update
        // (same superuser-only bypass rule Create enforces above).
        cargoType.OrganizationId = User.IsInRole("Superuser") ? cargoType.OrganizationId : existing.OrganizationId;

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
