using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Auth;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("purchasing/operations/test-mode")]
public sealed class Es7OperationalTestModeController(
    IAuthenticatedUserContext userContext,
    Es7OperationalTestMode testMode) : ControllerBase
{
    [HttpPost("activate")]
    [ProducesResponseType<OperationalTestGrantDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OperationalTestGrantDto>> Activate(CancellationToken cancellationToken)
    {
        if (!testMode.Enabled) return Forbid();
        var user = await userContext.GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        return Ok(testMode.Activate(user, BearerToken));
    }

    private string BearerToken => Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? Request.Headers.Authorization.ToString()[7..].Trim()
        : string.Empty;
}