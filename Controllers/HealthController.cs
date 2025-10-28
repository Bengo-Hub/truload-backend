using Microsoft.AspNetCore.Mvc;

namespace TruloadBackend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    [Route("/health")]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            service = "TruLoad Backend",
            timestamp = DateTime.UtcNow,
            version = "v1.0.0"
        });
    }
}

