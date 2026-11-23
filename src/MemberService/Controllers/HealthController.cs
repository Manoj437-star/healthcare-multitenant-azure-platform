using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MemberService.Data;

namespace MemberService.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    private readonly MemberDbContext _dbContext;
    private readonly ILogger<HealthController> _logger;

    public HealthController(MemberDbContext dbContext, ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Liveness probe: returns OK if the service process is up.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "MemberService",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Readiness probe: verifies database connection is active.
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        try
        {
            // Verify DB connectivity
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    status = "Unhealthy",
                    service = "MemberService",
                    details = "Database connection check failed",
                    timestamp = DateTime.UtcNow
                });
            }

            return Ok(new
            {
                status = "Ready",
                service = "MemberService",
                database = "Connected",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "Unhealthy",
                service = "MemberService",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
