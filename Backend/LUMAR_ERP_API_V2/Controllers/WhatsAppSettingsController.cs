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
    public async Task<ActionResult<WhatsAppConnectionSettingsResponseDto>> GetStatus(CancellationToken cancellationToken)
    {
        var user = await userContext.GetCurrentUserAsync(cancellationToken);
        if (user is null || !string.Equals(user.Role, "System Administrator", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var status = await store.GetStatusAsync(cancellationToken);
        return Ok(new WhatsAppConnectionSettingsResponseDto(
            Enabled: status.Enabled,
            PhoneNumberId: status.PhoneNumberId,
            TokenConfigured: status.TokenConfigured,
            ConnectionConfigured: status.ConnectionConfigured,
            Message: status.Message));
    }

    [HttpPut("status")]
    public async Task<ActionResult<WhatsAppConnectionSettingsResponseDto>> Update(
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

        var status = await store.UpdateAsync(request, cancellationToken);
        return Ok(new WhatsAppConnectionSettingsResponseDto(
            Enabled: status.Enabled,
            PhoneNumberId: status.PhoneNumberId,
            TokenConfigured: status.TokenConfigured,
            ConnectionConfigured: status.ConnectionConfigured,
            Message: status.Message));
    }
}
