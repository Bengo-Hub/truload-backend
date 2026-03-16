using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Services.Interfaces;

namespace TruLoad.Backend.Controllers.System;

[ApiController]
[Route("api/v1/[controller]")]
public class VersionController : ControllerBase
{
    private readonly IVersionService _versionService;

    public VersionController(IVersionService versionService)
    {
        _versionService = versionService;
    }

    /// <summary>
    /// Gets the current application version
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        var versionInfo = _versionService.GetVersionInfo();
        return Ok(versionInfo);
    }
}
