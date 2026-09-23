using LUMAR_ERP_API_V2.DTOs.Loyalty;

namespace LUMAR_ERP_API_V2.Services;

public interface ILoyaltyService
{
    Task<LoyaltyDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<LoyaltyTransactionsScreenDto> GetTransactionsScreenAsync(string? search, string? transactionType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken);
    Task<LoyaltyRewardsScreenDto> GetRewardsScreenAsync(string? search, string? rewardType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken);
    Task<LoyaltyAccountDto?> GetAccountByCustomerAsync(int customerId, CancellationToken cancellationToken);
    Task<LoyaltyAccountDto?> ReactivateAccountAsync(int customerId, CancellationToken cancellationToken);
    Task<LoyaltyAccountDto> EnsureAccountAsync(int customerId, CancellationToken cancellationToken);
    Task<LoyaltyBalanceDto> GetBalanceAsync(int customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoyaltyTransactionDto>> GetTransactionsByCustomerAsync(int customerId, CancellationToken cancellationToken);
    Task<LoyaltyTransactionResultDto> CreateTransactionAsync(int customerId, string transactionType, decimal points, int? orderId, int? rewardId, string source, string? notes, CancellationToken cancellationToken);
}
