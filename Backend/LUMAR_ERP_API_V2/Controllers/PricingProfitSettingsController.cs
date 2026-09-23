using LUMAR_ERP_API_V2.DTOs.Pricing;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("pricing-engine/profit-settings")]
public sealed class PricingProfitSettingsController(IPricingProfitSettingsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PricingProfitSettingsDto>> Get(CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(cancellationToken));

    [HttpPut("global")]
    public async Task<ActionResult<PricingProfitSettingsDto>> SetGlobal(
        [FromBody] PricingProfitPercentageRequestDto request,
        CancellationToken cancellationToken) => await Execute(
            () => service.SetGlobalAsync(request.ProfitPercentage, cancellationToken));

    [HttpPut("product-type/{productTypeId:int}")]
    public async Task<ActionResult<PricingProfitSettingsDto>> SetProductType(
        int productTypeId,
        [FromBody] PricingProfitPercentageRequestDto request,
        CancellationToken cancellationToken) => await Execute(
            () => service.SetProductTypeAsync(productTypeId, request.ProfitPercentage, cancellationToken));

    private async Task<ActionResult<PricingProfitSettingsDto>> Execute(
        Func<Task<PricingProfitSettingsDto>> operation)
    {
        try
        {
            return Ok(await operation());
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }
}
