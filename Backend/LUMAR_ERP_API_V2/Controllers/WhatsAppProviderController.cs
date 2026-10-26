using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.WhatsApp;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("whatsapp-provider")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WhatsAppProviderController(WhatsAppProviderService service, IAuthenticatedUserContext userContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<WhatsAppProviderResponse>(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(503)]
    public Task<ActionResult<WhatsAppProviderResponse>> Get(CancellationToken cancellationToken)
        => Execute(() => service.GetAsync(cancellationToken), cancellationToken);

    [HttpPut]
    [ProducesResponseType<WhatsAppProviderResponse>(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(409)]
    [ProducesResponseType(503)]
    public Task<ActionResult<WhatsAppProviderResponse>> Set(SetWhatsAppProviderRequest request, CancellationToken cancellationToken)
        => Execute(() => service.SetAsync(request.Provider, cancellationToken), cancellationToken);

    private async Task<ActionResult<WhatsAppProviderResponse>> Execute(Func<Task<WhatsAppProviderResponse>> operation, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var user = await userContext.GetCurrentUserAsync(cancellationToken);
        if (user is null) return Unauthorized();
        if (!user.IsActive || !(string.Equals(user.Role?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Role?.Trim(), "System Administrator", StringComparison.OrdinalIgnoreCase)))
            return StatusCode(403, new { message = "إدارة مزود واتساب متاحة لمسؤول النظام فقط." });
        try { return Ok(await operation()); }
        catch (WhatsAppProviderConflictException exception) { return Conflict(new { message = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (SqlException) { return StatusCode(503, new { message = "تعذر الوصول إلى إعداد مزود واتساب. لم يتم اعتماد تغيير." }); }
    }
}