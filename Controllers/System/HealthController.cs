using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Services.Interfaces;

namespace TruLoad.Backend.Controllers.System;

[ApiController]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly IVersionService _versionService;

    public HealthController(ILogger<HealthController> logger, IVersionService versionService)
    {
        _logger = logger;
        _versionService = versionService;
    }

    /// <summary>
    /// Health check endpoint — accessible at both /health and /api/v1/health.
    /// </summary>
    [HttpGet]
    [Route("/health")]
    [Route("/api/v1/health")]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            service = "TruLoad Backend API",
            timestamp = DateTime.UtcNow,
            version = _versionService.GetVersion()
        });
    }
}
