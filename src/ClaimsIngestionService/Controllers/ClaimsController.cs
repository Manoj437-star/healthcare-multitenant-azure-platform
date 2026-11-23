using Microsoft.AspNetCore.Mvc;
using ClaimsIngestionService.DTOs;
using ClaimsIngestionService.Services;

namespace ClaimsIngestionService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ClaimsController : ControllerBase
{
    private readonly IClaimsService _claimsService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ClaimsController> _logger;

    public ClaimsController(IClaimsService claimsService, ITenantContext tenantContext, ILogger<ClaimsController> logger)
    {
        _claimsService = claimsService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Ingest and validate a new healthcare claim.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClaimResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitClaim([FromBody] SubmitClaimRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _claimsService.IngestClaimAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetClaimById), new { id = result.ClaimId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "ValidationError", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Forbidden", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "Conflict", message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieve claim details by claim ID within the current tenant scope.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ClaimResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClaimById(string id, CancellationToken cancellationToken)
    {
        var claim = await _claimsService.GetClaimByIdAsync(id, cancellationToken);
        if (claim == null)
        {
            return NotFound(new { error = "NotFound", message = $"Claim '{id}' was not found for tenant '{_tenantContext.TenantId}'." });
        }

        return Ok(claim);
    }
}
