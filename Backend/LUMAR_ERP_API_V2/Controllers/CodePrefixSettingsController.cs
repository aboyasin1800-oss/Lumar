using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("settings")]
public sealed class CodePrefixSettingsController(ICodePrefixSettingsService service) : ControllerBase
{
    [HttpGet("code-prefixes")]
    public async Task<ActionResult<IReadOnlyList<CodePrefixSettingDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await service.GetAllAsync(cancellationToken));
    }

    [HttpGet("code-prefixes/{key}")]
    public async Task<ActionResult<CodePrefixSettingDto>> GetByKey(string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest("اسم المفتاح مطلوب.");
        }

        var value = await service.GetByKeyAsync(key, cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpPut("code-prefixes")]
    public async Task<ActionResult<IReadOnlyList<CodePrefixSettingDto>>> Upsert([FromBody] CodePrefixSettingsBatchDto request, CancellationToken cancellationToken)
    {
        if (request.Settings is null || request.Settings.Count == 0)
        {
            return BadRequest("يجب إرسال قائمة المفاتيح المراد حفظها.");
        }

        try
        {
            var saved = await service.UpsertAsync(request.Settings, cancellationToken);
            return Ok(saved);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
