using LUMAR_ERP_API_V2.DTOs.Referral;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("api/referrals")]
public sealed class ReferralController(IReferralService service) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<ReferralDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        return Ok(await service.GetDashboardAsync(cancellationToken));
    }

    [HttpGet("dashboard/search")]
    public async Task<ActionResult<IReadOnlyList<ReferralDashboardSearchResultDto>>> SearchDashboard([FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query)) return Ok(Array.Empty<ReferralDashboardSearchResultDto>());
        return Ok(await service.SearchDashboardAsync(query, cancellationToken));
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<ReferralAnalyticsDto>> GetAnalytics(
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        return Ok(await service.GetAnalyticsAsync(search, from, to, cancellationToken));
    }

    [HttpGet("rewards-screen")]
    public async Task<ActionResult<ReferralRewardsScreenDto>> GetRewardsScreen(
        [FromQuery] string? transactionType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? customerId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        if (transactionType is not null && transactionType is not ("RewardGranted" or "RewardReversal"))
        {
            return BadRequest("Unsupported referral reward transaction type.");
        }

        return Ok(await service.GetRewardsScreenAsync(transactionType, from, to, customerId, search, cancellationToken));
    }

    [HttpPost("codes/ensure")]
    public async Task<ActionResult<ReferralCodeDto>> EnsureCode([FromBody] EnsureReferralCodeRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerId <= 0) return BadRequest("CustomerId must be positive.");

        var code = await service.EnsureCodeAsync(request.CustomerId, request.PreferredCode, cancellationToken);
        return code is null ? NotFound() : Ok(code);
    }

    [HttpGet("customers/{customerId:int}/codes")]
    public async Task<ActionResult<IReadOnlyList<ReferralCodeDto>>> GetCodes(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        return Ok(await service.GetCodesByCustomerAsync(customerId, cancellationToken));
    }

    [HttpGet("customers/{customerId:int}/account")]
    public async Task<ActionResult<ReferralAccountSummaryDto>> GetAccount(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        var account = await service.GetAccountByCustomerAsync(customerId, cancellationToken);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpGet("customers/{customerId:int}/transactions")]
    public async Task<ActionResult<IReadOnlyList<ReferralTransactionDto>>> GetTransactions(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        return Ok(await service.GetTransactionsByCustomerAsync(customerId, cancellationToken));
    }

    [HttpGet("roots")]
    public async Task<ActionResult<IReadOnlyList<ReferralRootDto>>> GetRoots(CancellationToken cancellationToken)
    {
        return Ok(await service.GetRootsAsync(cancellationToken));
    }

    [HttpGet("customers/{customerId:int}/tree")]
    public async Task<ActionResult<ReferralTreeDto>> GetTree(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        var tree = await service.GetTreeAsync(customerId, cancellationToken);
        return tree is null ? NotFound() : Ok(tree);
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterReferralResponse>> Register([FromBody] RegisterReferralRequest request, CancellationToken cancellationToken)
    {
        if (request.ReferredCustomerId <= 0) return BadRequest("ReferredCustomerId must be positive.");
        if (string.IsNullOrWhiteSpace(request.ReferralCode)) return BadRequest("ReferralCode is required.");

        try
        {
            var response = await service.RegisterAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
