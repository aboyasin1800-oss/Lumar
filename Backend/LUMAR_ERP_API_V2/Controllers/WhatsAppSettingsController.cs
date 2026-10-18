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
        if (user is null)
        {
            return Unauthorized();
        }

        if (!IsAdministrativeRole(user.Role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "هذه الشاشة متاحة لمسؤول النظام فقط.");
        }

        var runtime = await store.GetRuntimeSettingsAsync(cancellationToken);
        var connectionConfigured = runtime.Enabled
            && !string.IsNullOrWhiteSpace(runtime.PhoneNumberId)
            && !string.IsNullOrWhiteSpace(runtime.AccessToken);

        return Ok(new WhatsAppConnectionSettingsResponseDto(
            Enabled: runtime.Enabled,
            BaseUrl: string.IsNullOrWhiteSpace(runtime.BaseUrl) ? "https://graph.facebook.com" : runtime.BaseUrl,
            ApiVersion: string.IsNullOrWhiteSpace(runtime.ApiVersion) ? "v20.0" : runtime.ApiVersion,
            PhoneNumberId: runtime.PhoneNumberId ?? string.Empty,
            TimeoutSeconds: runtime.TimeoutSeconds <= 0 ? 30 : runtime.TimeoutSeconds,
            IsConfigured: connectionConfigured,
            TokenConfigured: !string.IsNullOrWhiteSpace(runtime.AccessToken),
            Message: connectionConfigured ? "تمت تهيئة اتصال واتساب بشكل كامل." : "اتصال واتساب غير مكتمل. يلزم رقم الهاتف ورمز الوصول."));
    }

    [HttpPut("status")]
    public async Task<ActionResult<WhatsAppConnectionSettingsResponseDto>> Update(
        [FromBody] WhatsAppConnectionSettingsUpdateDto request,
        CancellationToken cancellationToken)
    {
        var user = await userContext.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!IsAdministrativeRole(user.Role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "هذه الشاشة متاحة لمسؤول النظام فقط.");
        }

        if (request is null)
        {
            return BadRequest("يجب إرسال بيانات الاتصال المطلوبة.");
        }

        var status = await store.UpdateAsync(request, cancellationToken);
        var runtime = await store.GetRuntimeSettingsAsync(cancellationToken);
        var connectionConfigured = runtime.Enabled
            && !string.IsNullOrWhiteSpace(runtime.PhoneNumberId)
            && !string.IsNullOrWhiteSpace(runtime.AccessToken);

        return Ok(new WhatsAppConnectionSettingsResponseDto(
            Enabled: runtime.Enabled,
            BaseUrl: string.IsNullOrWhiteSpace(runtime.BaseUrl) ? "https://graph.facebook.com" : runtime.BaseUrl,
            ApiVersion: string.IsNullOrWhiteSpace(runtime.ApiVersion) ? "v20.0" : runtime.ApiVersion,
            PhoneNumberId: runtime.PhoneNumberId ?? string.Empty,
            TimeoutSeconds: runtime.TimeoutSeconds <= 0 ? 30 : runtime.TimeoutSeconds,
            IsConfigured: connectionConfigured,
            TokenConfigured: !string.IsNullOrWhiteSpace(runtime.AccessToken),
            Message: status.Message));
    }

    private static bool IsAdministrativeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        var normalized = role.Trim();
        return string.Equals(normalized, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "System Administrator", StringComparison.OrdinalIgnoreCase);
    }
}
