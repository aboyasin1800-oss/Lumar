using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class LoyaltyService(
    ILoyaltyRepository repository,
    ILoyaltyAccountLifecycleService? lifecycle = null,
    IVipLevelEvaluationService? vipLevelEvaluationService = null) : ILoyaltyService
{
    public Task<LoyaltyDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => repository.GetDashboardAsync(cancellationToken);
    public Task<LoyaltyTransactionsScreenDto> GetTransactionsScreenAsync(string? search, string? transactionType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken) => repository.GetTransactionsScreenAsync(search, transactionType, from, to, customerId, cancellationToken);
    public Task<LoyaltyRewardsScreenDto> GetRewardsScreenAsync(string? search, string? rewardType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken) => repository.GetRewardsScreenAsync(search, rewardType, from, to, customerId, cancellationToken);
    public async Task<LoyaltyAccountDto?> GetAccountByCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        if (vipLevelEvaluationService is not null)
        {
            await vipLevelEvaluationService.GetCustomerEvaluationAsync(customerId, cancellationToken);
        }

        return lifecycle is not null
            ? await lifecycle.EvaluateAsync(customerId, cancellationToken)
            : await repository.GetAccountByCustomerAsync(customerId, cancellationToken);
    }

    public Task<LoyaltyAccountDto?> ReactivateAccountAsync(int customerId, CancellationToken cancellationToken) =>
        lifecycle?.ReactivateManuallyAsync(customerId, cancellationToken)
        ?? throw new InvalidOperationException("Loyalty account lifecycle is not configured.");

    public Task<LoyaltyAccountDto> EnsureAccountAsync(int customerId, CancellationToken cancellationToken) => repository.EnsureAccountAsync(customerId, cancellationToken);

    public async Task<LoyaltyBalanceDto> GetBalanceAsync(int customerId, CancellationToken cancellationToken)
    {
        if (lifecycle is not null) await lifecycle.EvaluateAsync(customerId, cancellationToken);
        return await repository.GetBalanceAsync(customerId, cancellationToken);
    }

    public Task<IReadOnlyList<LoyaltyTransactionDto>> GetTransactionsByCustomerAsync(int customerId, CancellationToken cancellationToken) => repository.GetTransactionsByCustomerAsync(customerId, cancellationToken);

    public async Task<LoyaltyTransactionResultDto> CreateTransactionAsync(int customerId, string transactionType, decimal points, int? orderId, int? rewardId, string source, string? notes, CancellationToken cancellationToken)
    {
        var result = await repository.CreateTransactionAsync(customerId, transactionType, points, orderId, rewardId, source, notes, cancellationToken);
        if (vipLevelEvaluationService is not null)
        {
            await vipLevelEvaluationService.EvaluateNetworkAsync(customerId, cancellationToken);
        }

        return result;
    }
}
