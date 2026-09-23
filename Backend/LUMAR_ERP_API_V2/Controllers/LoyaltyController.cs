using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Services;
using Microsoft.AspNetCore.Mvc;

namespace LUMAR_ERP_API_V2.Controllers;

[ApiController]
[Route("api/loyalty")]
public sealed class LoyaltyController(ILoyaltyService service) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<LoyaltyDashboardDto>> GetDashboard(CancellationToken cancellationToken)
        => Ok(await service.GetDashboardAsync(cancellationToken));

    [HttpGet("transactions-screen")]
    public async Task<ActionResult<LoyaltyTransactionsScreenDto>> GetTransactionsScreen(
        [FromQuery] string? search,
        [FromQuery] string? transactionType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? customerId,
        CancellationToken cancellationToken)
    {
        if (customerId is <= 0) return BadRequest("CustomerId must be positive.");
        return Ok(await service.GetTransactionsScreenAsync(search, transactionType, from, to, customerId, cancellationToken));
    }

    [HttpGet("rewards-screen")]
    public async Task<ActionResult<LoyaltyRewardsScreenDto>> GetRewardsScreen(
        [FromQuery] string? search,
        [FromQuery] string? rewardType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? customerId,
        CancellationToken cancellationToken)
    {
        if (customerId is <= 0) return BadRequest("CustomerId must be positive.");
        return Ok(await service.GetRewardsScreenAsync(search, rewardType, from, to, customerId, cancellationToken));
    }

    [HttpGet("customers/{customerId:int}/account")]
    public async Task<ActionResult<LoyaltyAccountDto>> GetAccount(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        var account = await service.GetAccountByCustomerAsync(customerId, cancellationToken);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPost("customers/{customerId:int}/ensure")]
    public async Task<ActionResult<LoyaltyAccountDto>> EnsureAccount(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        return Ok(await service.EnsureAccountAsync(customerId, cancellationToken));
    }

    [HttpPost("customers/{customerId:int}/reactivate")]
    public async Task<ActionResult<LoyaltyAccountDto>> ReactivateAccount(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        var account = await service.ReactivateAccountAsync(customerId, cancellationToken);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpGet("customers/{customerId:int}/balance")]
    public async Task<ActionResult<LoyaltyBalanceDto>> GetBalance(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        return Ok(await service.GetBalanceAsync(customerId, cancellationToken));
    }

    [HttpGet("customers/{customerId:int}/transactions")]
    public async Task<ActionResult<IReadOnlyList<LoyaltyTransactionDto>>> GetTransactions(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest("CustomerId must be positive.");
        return Ok(await service.GetTransactionsByCustomerAsync(customerId, cancellationToken));
    }

    [HttpPost("transactions")]
    public async Task<ActionResult<LoyaltyTransactionResultDto>> CreateTransaction([FromBody] CreateLoyaltyTransactionRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerId <= 0) return BadRequest("CustomerId must be positive.");
        if (string.IsNullOrWhiteSpace(request.TransactionType)) return BadRequest("TransactionType is required.");
        if (string.IsNullOrWhiteSpace(request.Source)) return BadRequest("Source is required.");

        var result = await service.CreateTransactionAsync(request.CustomerId, request.TransactionType, request.Points, request.OrderId, request.RewardId, request.Source, request.Notes, cancellationToken);
        return Ok(result);
    }
}
