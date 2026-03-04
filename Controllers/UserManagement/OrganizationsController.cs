using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Constants;
using TruLoad.Backend.DTOs.User;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;

namespace TruLoad.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        IOrganizationRepository organizationRepository,
        ITenantContext tenantContext,
        ILogger<OrganizationsController> logger)
    {
        _organizationRepository = organizationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    [HasPermission("system.manage_organizations")]
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
    [HasPermission("system.manage_organizations")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrganizationDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var orgs = await _organizationRepository.GetAllAsync(includeInactive, cancellationToken);
        return Ok(orgs.Select(MapToDto));
    }

    [HttpGet("code/{code}")]
    [HasPermission("system.manage_organizations")]
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
    [HasPermission("system.manage_organizations")]
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
            OrgType = NormalizeOrgType(request.OrgType) ?? "Private",
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
    [HasPermission("system.manage_organizations")]
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
        if (request.OrgType != null) org.OrgType = NormalizeOrgType(request.OrgType) ?? org.OrgType;
        if (request.ContactEmail != null) org.ContactEmail = request.ContactEmail;
        if (request.ContactPhone != null) org.ContactPhone = request.ContactPhone;
        if (request.Address != null) org.Address = request.Address;
        if (request.IsActive.HasValue) org.IsActive = request.IsActive.Value;
        if (request.TenantType != null) org.TenantType = NormalizeTenantType(request.TenantType);
        if (request.EnabledModules != null) org.EnabledModulesJson = JsonSerializer.Serialize(request.EnabledModules);
        if (request.LogoUrl != null) org.LogoUrl = request.LogoUrl;
        if (request.PlatformLogoUrl != null) org.PlatformLogoUrl = request.PlatformLogoUrl;
        if (request.LoginPageImageUrl != null) org.LoginPageImageUrl = request.LoginPageImageUrl;
        if (request.PrimaryColor != null) org.PrimaryColor = request.PrimaryColor;
        if (request.SecondaryColor != null) org.SecondaryColor = request.SecondaryColor;

        var updated = await _organizationRepository.UpdateAsync(org, cancellationToken);
        _logger.LogInformation("Organization updated: {OrgId}", updated.Id);

        return Ok(MapToDto(updated));
    }

    /// <summary>
    /// Get the current tenant's organisation (for branding/edit in system config). Uses tenant context.
    /// </summary>
    [HttpGet("current")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> GetCurrent(CancellationToken cancellationToken)
    {
        var orgId = _tenantContext.OrganizationId;
        if (orgId == Guid.Empty)
            return NotFound(new { message = "No organisation in context" });
        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
        if (org == null)
            return NotFound(new { message = "Organisation not found" });
        return Ok(MapToDto(org));
    }

    /// <summary>
    /// Update branding (logo, colours) for the current tenant's organisation. Requires config.update.
    /// </summary>
    [HttpPatch("current/branding")]
    [HasPermission("config.update")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> UpdateCurrentBranding([FromBody] UpdateOrganizationBrandingRequest request, CancellationToken cancellationToken)
    {
        var orgId = _tenantContext.OrganizationId;
        if (orgId == Guid.Empty)
            return NotFound(new { message = "No organisation in context" });
        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
        if (org == null)
            return NotFound(new { message = "Organisation not found" });
        if (request.LogoUrl != null) org.LogoUrl = request.LogoUrl;
        if (request.PlatformLogoUrl != null) org.PlatformLogoUrl = request.PlatformLogoUrl;
        if (request.LoginPageImageUrl != null) org.LoginPageImageUrl = request.LoginPageImageUrl;
        if (request.PrimaryColor != null) org.PrimaryColor = request.PrimaryColor;
        if (request.SecondaryColor != null) org.SecondaryColor = request.SecondaryColor;
        var updated = await _organizationRepository.UpdateAsync(org, cancellationToken);
        _logger.LogInformation("Organisation branding updated: {OrgId}", orgId);
        return Ok(MapToDto(updated));
    }

    /// <summary>
    /// Update organization tenant type and enabled modules. Superuser only.
    /// </summary>
    [HttpPatch("{id:guid}/modules")]
    [Authorize(Roles = "Superuser")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> UpdateModules(Guid id, [FromBody] UpdateOrganizationModulesRequest request, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdAsync(id, cancellationToken);
        if (org == null)
            return NotFound(new { message = "Organization not found" });

        if (request.TenantType != null)
            org.TenantType = NormalizeTenantType(request.TenantType);
        if (request.EnabledModules != null)
            org.EnabledModulesJson = JsonSerializer.Serialize(request.EnabledModules);

        var updated = await _organizationRepository.UpdateAsync(org, cancellationToken);
        _logger.LogInformation("Organization modules updated: {OrgId}, TenantType: {TenantType}", id, updated.TenantType);
        return Ok(MapToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("system.manage_organizations")]
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
        var enabledModules = ResolveEnabledModules(org);
        return new OrganizationDto
        {
            Id = org.Id,
            Code = org.Code,
            Name = org.Name,
            OrgType = org.OrgType,
            TenantType = org.TenantType,
            EnabledModules = enabledModules,
            ContactEmail = org.ContactEmail,
            ContactPhone = org.ContactPhone,
            Address = org.Address,
            LogoUrl = org.LogoUrl,
            PlatformLogoUrl = org.PlatformLogoUrl,
            LoginPageImageUrl = org.LoginPageImageUrl,
            PrimaryColor = org.PrimaryColor,
            SecondaryColor = org.SecondaryColor,
            IsActive = org.IsActive,
            CreatedAt = org.CreatedAt,
            UpdatedAt = org.UpdatedAt
        };
    }

    private static List<string> ResolveEnabledModules(Organization org)
    {
        if (!string.IsNullOrWhiteSpace(org.EnabledModulesJson))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(org.EnabledModulesJson);
                if (list != null && list.Count > 0)
                    return list;
            }
            catch { /* use defaults */ }
        }
        if (string.Equals(org.TenantType, TenantModules.TenantTypeCommercialWeighing, StringComparison.OrdinalIgnoreCase))
            return TenantModules.DefaultCommercialWeighingModules.ToList();
        return TenantModules.AllModules.ToList();
    }

    private static string? NormalizeTenantType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim() switch
        {
            var v when v.Equals(TenantModules.TenantTypeCommercialWeighing, StringComparison.OrdinalIgnoreCase) => TenantModules.TenantTypeCommercialWeighing,
            var v when v.Equals(TenantModules.TenantTypeAxleLoadEnforcement, StringComparison.OrdinalIgnoreCase) => TenantModules.TenantTypeAxleLoadEnforcement,
            _ => value.Trim()
        };
    }

    private static string? NormalizeOrgType(string? orgType)
    {
        if (string.IsNullOrWhiteSpace(orgType)) return null;

        var value = orgType.Trim();
        return value.Equals("government", StringComparison.OrdinalIgnoreCase)
            ? "Government"
            : value.Equals("private", StringComparison.OrdinalIgnoreCase)
                ? "Private"
                : null;
    }
}




