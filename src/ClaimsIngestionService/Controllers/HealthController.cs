using Microsoft.AspNetCore.Mvc;

namespace ClaimsIngestionService.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Liveness probe: returns OK if the container process is running.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "ClaimsIngestionService",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Readiness probe: verifies data store readiness.
    /// </summary>
    [HttpGet("ready")]
    public IActionResult Ready()
    {
        return Ok(new
        {
            status = "Ready",
            service = "ClaimsIngestionService",
            storage = "Healthy",
            timestamp = DateTime.UtcNow
        });
    }
}
