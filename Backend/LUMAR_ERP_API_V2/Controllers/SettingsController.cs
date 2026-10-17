using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("settings")]
public sealed class SettingsController(ISettingsService service, IAuthenticatedUserContext userContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SystemSettingListDto>>> All(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<SettingCategoryDto>>> Categories(CancellationToken cancellationToken) =>
        Ok(await service.GetCategoriesAsync(cancellationToken));

    [HttpGet("categories/{category}")]
    public async Task<ActionResult<IReadOnlyList<SystemSettingListDto>>> Category(string category, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(category)) return BadRequest("Category must not be empty.");
        return Ok(await service.GetByCategoryAsync(category, cancellationToken));
    }

    [HttpGet("by-key/{key}")]
    public async Task<ActionResult<SystemSettingDetailsDto>> ByKey(string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key)) return BadRequest("Setting key must not be empty.");
        var setting = await service.GetByKeyAsync(key, cancellationToken);
        return setting is null ? NotFound() : Ok(setting);
    }

    [HttpGet("production-routes")]
    public async Task<ActionResult<ProductionRoutesConfigDto>> ProductionRoutes(CancellationToken cancellationToken) => Ok(await service.GetProductionRoutesConfigAsync(cancellationToken));

    [HttpGet("production-routes/options")]
    public async Task<ActionResult<IReadOnlyList<string>>> ProductionRouteOptions(CancellationToken cancellationToken) => Ok(await service.GetProductionRoutePieceTypesAsync(cancellationToken));

    [HttpPut("production-routes")]
    public async Task<ActionResult<ProductionRoutesConfigDto>> UpdateProductionRoutes([FromBody] ProductionRoutesConfigDto request, CancellationToken cancellationToken)
    {
        if (request is null || request.Routes is null || request.Routes.Count == 0)
        {
            return BadRequest("يجب إرسال مسارات الإنتاج المطلوبة.");
        }

        try
        {
            return Ok(await service.UpsertProductionRoutesConfigAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("message")]
    public async Task<ActionResult<MessageSettingsDto>> MessageSettings(CancellationToken cancellationToken) =>
        Ok(await service.GetMessageSettingsAsync(cancellationToken));

    [HttpPut("message")]
    public async Task<ActionResult<MessageSettingsDto>> UpdateMessageSettings(
        [FromBody] MessageSettingsDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("يجب إرسال إعدادات الرسائل المطلوبة.");
        }

        try
        {
            return Ok(await service.UpdateMessageSettingsAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("whatsapp-connection")]
    public async Task<ActionResult<WhatsAppConnectionSettingsResponseDto>> WhatsAppConnection(CancellationToken cancellationToken)
    {
        var user = await userContext.GetCurrentUserAsync(cancellationToken);
        if (user is null || !string.Equals(user.Role, "System Administrator", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        return Ok(await service.GetWhatsAppConnectionSettingsAsync(cancellationToken));
    }

    [HttpPut("whatsapp-connection")]
    public async Task<ActionResult<WhatsAppConnectionSettingsResponseDto>> UpdateWhatsAppConnection(
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
            return BadRequest("يجب إرسال إعدادات واتساب المطلوبة.");
        }

        try
        {
            return Ok(await service.UpdateWhatsAppConnectionSettingsAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("currency")]
    public async Task<IActionResult> UpdateCurrency(
        [FromBody] UpdateCurrencySettingsDto request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.CurrencyName) || string.IsNullOrWhiteSpace(request.PreferredCurrency))
        {
            return BadRequest("اسم العملة واختصارها مطلوبان.");
        }

        try
        {
            var updated = await service.UpdateCurrencySettingsAsync(request.CurrencyName, request.PreferredCurrency, cancellationToken);
            return updated ? Ok(new { message = "تم تحديث إعدادات العملة." }) : NotFound("إعدادات العملة غير موجودة.");
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("print-config")]
    public async Task<IActionResult> UpdatePrintSettings(
        [FromBody] Dictionary<string, string> request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.Count == 0)
        {
            return BadRequest("يجب إرسال إعدادات الطباعة المطلوبة.");
        }

        try
        {
            var updated = await service.UpdatePrintSettingsAsync(request, cancellationToken);
            return updated ? Ok(new { message = "تم تحديث إعدادات الطباعة." }) : NotFound("إعدادات الطباعة غير موجودة." );
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("general-cash-account")]
    public async Task<ActionResult<GeneralCashAccountSettingDto>> GeneralCashAccount(CancellationToken cancellationToken)
    {
        var setting = await service.GetGeneralCashAccountSettingAsync(cancellationToken);
        return Ok(setting);
    }

    [HttpPut("general-cash-account")]
    public async Task<ActionResult<GeneralCashAccountSettingDto>> UpdateGeneralCashAccount([FromBody] UpdateGeneralCashAccountSettingDto request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("يجب إرسال القيمة المطلوبة للحساب النقدي العام.");
        }

        if (request.CashAccountId is not null && request.CashAccountId <= 0)
        {
            return BadRequest("معرف الحساب النقدي العام يجب أن يكون أكبر من صفر.");
        }

        try
        {
            var result = await service.UpsertGeneralCashAccountSettingAsync(request.CashAccountId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SystemSettingDetailsDto>> One(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest("Setting id must be positive.");
        var setting = await service.GetByIdAsync(id, cancellationToken);
        return setting is null ? NotFound() : Ok(setting);
    }

    [HttpPost]
    [HttpPut("{id:int}")]
    [HttpDelete("{id:int}")]
    public IActionResult Disabled() =>
        StatusCode(405, "Setting writes are restricted to the approved currency settings.");
}
