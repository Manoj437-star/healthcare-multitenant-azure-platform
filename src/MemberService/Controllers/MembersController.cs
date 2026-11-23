using Microsoft.AspNetCore.Mvc;
using MemberService.DTOs;
using MemberService.Services;

namespace MemberService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MembersController : ControllerBase
{
    private readonly IMemberService _memberService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MembersController> _logger;

    public MembersController(IMemberService memberService, ITenantContext tenantContext, ILogger<MembersController> logger)
    {
        _memberService = memberService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Enroll a new member in the health plan.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnrollMember([FromBody] EnrollMemberRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _memberService.EnrollMemberAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetMemberById), new { id = result.MemberId }, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Conflict during member enrollment: {Message}", ex.Message);
            return Conflict(new { error = "Conflict", message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieve a member by member identifier within the current tenant.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMemberById(string id, CancellationToken cancellationToken)
    {
        var member = await _memberService.GetMemberByIdAsync(id, cancellationToken);
        if (member == null)
        {
            return NotFound(new { error = "NotFound", message = $"Member '{id}' was not found for tenant '{_tenantContext.TenantId}'." });
        }

        return Ok(member);
    }

    /// <summary>
    /// Retrieve a list of members enrolled under the current tenant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MemberResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers(CancellationToken cancellationToken)
    {
        var members = await _memberService.GetMembersAsync(cancellationToken);
        return Ok(members);
    }
}
