using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;

namespace TruLoad.Backend.Controllers.Shared;

/// <summary>
/// Public (unauthenticated) endpoints for login page branding: list organizations and stations.
/// </summary>
[ApiController]
[Route("api/v1/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStationRepository _stationRepository;
    private readonly ILogger<PublicController> _logger;

    public PublicController(
        IOrganizationRepository organizationRepository,
        IStationRepository stationRepository,
        ILogger<PublicController> logger)
    {
        _organizationRepository = organizationRepository;
        _stationRepository = stationRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get active organizations for login page dropdown/branding (id, code, name, logo, colors).
    /// </summary>
    [HttpGet("organizations")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrganizations(CancellationToken cancellationToken)
    {
        var orgs = await _organizationRepository.GetAllAsync(includeInactive: false, cancellationToken);
        var list = orgs
            .Where(o => o.IsActive && o.Code != "CODEVERTEX") // Platform org not visible on public pages
            .Select(o => new
            {
                id = o.Id,
                code = o.Code,
                name = o.Name,
                logoUrl = o.LogoUrl,
                platformLogoUrl = o.PlatformLogoUrl,
                loginPageImageUrl = o.LoginPageImageUrl,
                primaryColor = o.PrimaryColor,
                secondaryColor = o.SecondaryColor,
                tenantType = o.TenantType,
            })
            .ToList();
        return Ok(list);
    }

    /// <summary>
    /// Get a single organization by code (slug) for branding on org-scoped auth and public pages.
    /// Lookup is case-insensitive so "kura", "KURA", "Kura" all resolve.
    /// </summary>
    [HttpGet("organizations/by-code/{code}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrganizationByCode(string code, CancellationToken cancellationToken)
    {
        var trimmed = code?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return NotFound();

        var org = await _organizationRepository.GetByCodeAsync(trimmed, cancellationToken);

        if (org == null || !org.IsActive)
            return NotFound();
        return Ok(new
        {
            id = org.Id,
            code = org.Code,
            name = org.Name,
            logoUrl = org.LogoUrl,
            platformLogoUrl = org.PlatformLogoUrl,
            loginPageImageUrl = org.LoginPageImageUrl,
            primaryColor = org.PrimaryColor,
            secondaryColor = org.SecondaryColor,
            tenantType = org.TenantType,
        });
    }

    /// <summary>
    /// Get stations for an organization (for login page station selector). Returns id, code, name only.
    /// </summary>
    [HttpGet("stations")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStationsByOrganization([FromQuery] Guid organizationId, CancellationToken cancellationToken)
    {
        var stations = await _stationRepository.GetByOrganizationIdAsync(organizationId, cancellationToken);
        var list = stations
            .Where(s => s.IsActive && s.DeletedAt == null)
            .Select(s => new { id = s.Id, code = s.Code, name = s.Name, isHq = s.IsHq })
            .ToList();
        return Ok(list);
    }
}
