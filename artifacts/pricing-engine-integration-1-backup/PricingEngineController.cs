using LUMAR_ERP_API_V2.DTOs.Pricing;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("pricing-engine")]
public sealed class PricingEngineController(IPricingEngineService service) : ControllerBase
{
    [HttpPost("calculate")]
    [ProducesResponseType<PricingEngineResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PricingEngineResponseDto>> Calculate(
        [FromBody] PricingEngineRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.ProductTypeId <= 0)
        {
            return BadRequest(new { message = "معرف نوع القطعة غير صالح." });
        }

        if (request.Quantity <= 0)
        {
            return BadRequest(new { message = "عدد القطع يجب أن يكون أكبر من صفر." });
        }

        if (request.Consumption < 0)
        {
            return BadRequest(new { message = "الاستهلاك لا يمكن أن يكون سالبًا." });
        }

        if (request.PieceProfitPercentage < 0 || request.GlobalProfitPercentage < 0)
        {
            return BadRequest(new { message = "نسبة الربح لا يمكن أن تكون سالبة." });
        }

        return Ok(await service.CalculateAsync(request, cancellationToken));
    }
}
