using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("api/vip-levels")]
public sealed class VipLevelEvaluationController(IVipLevelEvaluationService service) : ControllerBase
{
    [HttpGet("criteria")]
    public async Task<ActionResult<IReadOnlyList<VipLevelEvaluationCriteriaDto>>> GetCriteria(CancellationToken cancellationToken)
        => Ok(await service.GetCriteriaAsync(cancellationToken));

    [HttpGet("customers")]
    public async Task<ActionResult<IReadOnlyList<VipCustomerListItemDto>>> GetClassifiedCustomers(CancellationToken cancellationToken)
        => Ok(await service.GetClassifiedCustomersAsync(cancellationToken));

    [HttpGet("customers/{customerId:int}/evaluation")]
    public async Task<ActionResult<VipLevelEvaluationDto>> EvaluateCustomer(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        var evaluation = await service.GetCustomerEvaluationAsync(customerId, cancellationToken);
        return evaluation is null ? NotFound() : Ok(evaluation);
    }

    [HttpPost("customers/{customerId:int}/evaluate")]
    public async Task<ActionResult<VipLevelEvaluationDto>> EvaluateCustomerNow(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        var evaluation = await service.EvaluateCustomerAsync(customerId, cancellationToken);
        return evaluation is null ? NotFound() : Ok(evaluation);
    }

    [HttpPost("customers/{customerId:int}/evaluate-network")]
    public async Task<ActionResult<IReadOnlyList<VipLevelEvaluationDto>>> EvaluateNetwork(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        return Ok(await service.EvaluateNetworkAsync(customerId, cancellationToken));
    }

    [HttpPost("evaluate-all")]
    public async Task<ActionResult<IReadOnlyList<VipLevelEvaluationDto>>> EvaluateAll(CancellationToken cancellationToken)
        => Ok(await service.EvaluateAllAsync(cancellationToken));
}