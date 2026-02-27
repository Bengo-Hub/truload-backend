using Microsoft.AspNetCore.Mvc;

namespace TruLoad.Backend.Controllers.System;

[ApiController]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
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
            version = "v1.0.0"
        });
    }
}
