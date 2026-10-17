using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("whatsapp-settings")]
public sealed class WhatsAppSettingsController(
    IWhatsAppConnectionSettingsStore store,
    IAuthenticatedUserContext userContext) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<WhatsAppConnectionStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var user = await userContext.GetCurrentUserAsync(cancellationToken);
        if (user is null || !string.Equals(user.Role, "System Administrator", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        return Ok(await store.GetStatusAsync(cancellationToken));
    }

    [HttpPut("status")]
    public async Task<ActionResult<WhatsAppConnectionStatusDto>> Update(
        [FromBody] WhatsAppConnectionSettingsUpdateDto request,
        CancellationToken cancellationToken)
    {
        var user = await userContext.GetCurrentUserAsync(cancellationToken);
        if (user is null || !string.Equals(user.Role, "System Administrator", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (request is null)
        {
            return BadRequest("يجب إرسال بيانات الاتصال المطلوبة.");
        }

        return Ok(await store.UpdateAsync(request, cancellationToken));
    }
}
