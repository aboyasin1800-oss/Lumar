using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("api/loyalty-redemptions")]
public sealed class LoyaltyRedemptionsController(ILoyaltyRedemptionService service) : ControllerBase
{
    [HttpGet("history")]
    public async Task<ActionResult<LoyaltyRedemptionHistoryDto>> GetHistory(
        [FromQuery] string? search,
        [FromQuery] string? transactionType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? customerId,
        CancellationToken cancellationToken)
    {
        if (customerId is <= 0) return BadRequest("CustomerId must be positive.");
        if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date) return BadRequest("From must be earlier than To.");
        if (!string.IsNullOrWhiteSpace(transactionType) && transactionType is not ("Redeem" or "Reversal"))
            return BadRequest("TransactionType must be Redeem or Reversal.");

        return Ok(await service.GetHistoryAsync(search, transactionType, from, to, customerId, cancellationToken));
    }

    [HttpGet("customers/{customerId:int}/history")]
    public async Task<ActionResult<IReadOnlyList<LoyaltyRedemptionDto>>> GetHistory(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        return Ok(await service.GetHistoryByCustomerAsync(customerId, cancellationToken));
    }

    [HttpPost("preview")]
    public async Task<ActionResult<LoyaltyRedemptionPreviewDto>> Preview([FromBody] RedeemLoyaltyRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerId <= 0) return BadRequest("CustomerId must be positive.");
        if (request.OrderId <= 0) return BadRequest("OrderId must be positive.");
        if (request.PointsRedeemed <= 0m) return BadRequest("PointsRedeemed must be greater than zero.");

        return Ok(await service.PreviewAsync(request.CustomerId, request.OrderId, request.PointsRedeemed, request.PointMonetaryValue, cancellationToken));
    }

    [HttpPost("apply")]
    public async Task<ActionResult<LoyaltyRedemptionResultDto>> Apply([FromBody] RedeemLoyaltyRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerId <= 0) return BadRequest("CustomerId must be positive.");
        if (request.OrderId <= 0) return BadRequest("OrderId must be positive.");
        if (request.PointsRedeemed <= 0m) return BadRequest("PointsRedeemed must be greater than zero.");

        return Ok(await service.ApplyAsync(request.CustomerId, request.OrderId, request.PointsRedeemed, request.PointMonetaryValue, cancellationToken));
    }

    [HttpPost("reverse")]
    public async Task<ActionResult<LoyaltyRedemptionDto>> Reverse([FromBody] ReverseLoyaltyRedemptionRequest request, CancellationToken cancellationToken)
    {
        if (request.RedemptionId <= 0) return BadRequest("RedemptionId must be positive.");
        return Ok(await service.ReverseAsync(request.RedemptionId, cancellationToken));
    }
}
