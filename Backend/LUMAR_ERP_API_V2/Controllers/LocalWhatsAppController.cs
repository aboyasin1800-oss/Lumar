using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.WhatsApp;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("local-whatsapp")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LocalWhatsAppController(LocalWhatsAppGatewayClient gateway, IAuthenticatedUserContext userContext) : ControllerBase
{
    [HttpGet("health")]
    [ProducesResponseType<LocalWhatsAppHealthResponse>(200)]
    public Task<ActionResult<LocalWhatsAppHealthResponse>> Health(CancellationToken cancellationToken)
        => Execute(() => gateway.HealthAsync(cancellationToken), cancellationToken);

    [HttpGet("session/status")]
    [ProducesResponseType<LocalWhatsAppSessionResponse>(200)]
    public Task<ActionResult<LocalWhatsAppSessionResponse>> Status(CancellationToken cancellationToken)
        => Execute(() => gateway.StatusAsync(cancellationToken), cancellationToken);

    [HttpGet("session/link-material")]
    [ProducesResponseType<LocalWhatsAppLinkResponse>(200)]
    public Task<ActionResult<LocalWhatsAppLinkResponse>> LinkMaterial(CancellationToken cancellationToken)
        => Execute(() => gateway.LinkMaterialAsync(cancellationToken), cancellationToken);

    [HttpPost("session/{operation}")]
    [ProducesResponseType<LocalWhatsAppSessionResponse>(200)]
    public Task<ActionResult<LocalWhatsAppSessionResponse>> SessionAction(string operation, CancellationToken cancellationToken)
        => Execute(() => gateway.CommandAsync(operation, cancellationToken), cancellationToken);

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var user = await userContext.GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        if (!user.IsActive || !(string.Equals(user.Role?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Role?.Trim(), "System Administrator", StringComparison.OrdinalIgnoreCase)))
            return StatusCode(StatusCodes.Status403Forbidden);
        try { return Ok(await operation()); }
        catch (ArgumentException) { return BadRequest(new { code = "INVALID_ACTION" }); }
        catch (LocalWhatsAppGatewayException exception)
        {
            return StatusCode(exception.StatusCode, new { code = "LOCAL_GATEWAY_UNAVAILABLE_OR_SESSION_CONFLICT", state = "Unavailable" });
        }
    }
}