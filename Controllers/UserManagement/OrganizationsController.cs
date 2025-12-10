using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.User;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;

namespace TruLoad.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(IOrganizationRepository organizationRepository, ILogger<OrganizationsController> logger)
    {
        _organizationRepository = organizationRepository;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        if (org == null)
        {
            return NotFound(new { message = "Organization not found" });
        }

        return Ok(MapToDto(org));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrganizationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrganizationDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var orgs = await _organizationRepository.GetAllAsync(includeInactive, cancellationToken);
        return Ok(orgs.Select(MapToDto));
    }

    [HttpGet("code/{code}")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> GetByCode(string code, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByCodeAsync(code, cancellationToken);
        if (org == null)
        {
            return NotFound(new { message = "Organization not found" });
        }

        return Ok(MapToDto(org));
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrganizationDto>> Create([FromBody] CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        // Check if code already exists
        if (await _organizationRepository.CodeExistsAsync(request.Code, cancellationToken: cancellationToken))
        {
            return BadRequest(new { message = "Organization code already exists" });
        }

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            OrgType = request.OrgType,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _organizationRepository.CreateAsync(org, cancellationToken);
        _logger.LogInformation("Organization created: {OrgId}, Code: {Code}", created.Id, created.Code);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> Update(Guid id, [FromBody] UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        if (org == null)
        {
            return NotFound(new { message = "Organization not found" });
        }

        // Check if new code conflicts
        if (request.Code != null && request.Code != org.Code)
        {
            if (await _organizationRepository.CodeExistsAsync(request.Code, id, cancellationToken))
            {
                return BadRequest(new { message = "Organization code already exists" });
            }
            org.Code = request.Code;
        }

        if (request.Name != null) org.Name = request.Name;
        if (request.OrgType != null) org.OrgType = request.OrgType;
        if (request.ContactEmail != null) org.ContactEmail = request.ContactEmail;
        if (request.ContactPhone != null) org.ContactPhone = request.ContactPhone;
        if (request.Address != null) org.Address = request.Address;
        if (request.IsActive.HasValue) org.IsActive = request.IsActive.Value;

        var updated = await _organizationRepository.UpdateAsync(org, cancellationToken);
        _logger.LogInformation("Organization updated: {OrgId}", updated.Id);

        return Ok(MapToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        if (org == null)
        {
            return NotFound(new { message = "Organization not found" });
        }

        await _organizationRepository.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Organization deleted: {OrgId}", id);

        return NoContent();
    }

    private static OrganizationDto MapToDto(Organization org)
    {
        return new OrganizationDto
        {
            Id = org.Id,
            Code = org.Code,
            Name = org.Name,
            OrgType = org.OrgType,
            ContactEmail = org.ContactEmail,
            ContactPhone = org.ContactPhone,
            Address = org.Address,
            IsActive = org.IsActive,
            CreatedAt = org.CreatedAt,
            UpdatedAt = org.UpdatedAt
        };
    }
}




