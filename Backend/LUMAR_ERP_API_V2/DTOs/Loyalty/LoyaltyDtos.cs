namespace LUMAR_ERP_API_V2.DTOs.Loyalty;

public sealed record LoyaltyAccountDto(
    int LoyaltyAccountId,
    int CustomerId,
    decimal CurrentPoints,
    decimal LifetimeEarnedPoints,
    decimal LifetimeRedeemedPoints,
    decimal PendingExpirePoints,
    int? VipLevelId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastActivityAt,
    string LoyaltyAccountStatus = "Active",
    DateTime? WarningStartedAtUtc = null,
    DateTime? FrozenAtUtc = null,
    DateTime? ReactivatedAtUtc = null,
    string? FreezeReason = null,
    DateTime? LastQualifyingActivityAtUtc = null);

public sealed record LoyaltyTransactionDto(
    long LoyaltyTransactionId,
    int LoyaltyAccountId,
    int CustomerId,
    int? OrderId,
    int? RewardId,
    string TransactionType,
    decimal Points,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string Source,
    string? Notes,
    DateTime CreatedAt);

public sealed record LoyaltyBalanceDto(
    int LoyaltyAccountId,
    int CustomerId,
    decimal CurrentPoints,
    decimal LifetimeEarnedPoints,
    decimal LifetimeRedeemedPoints,
    decimal PendingExpirePoints,
    decimal BalanceBefore,
    decimal BalanceAfter,
    int? VipLevelId,
    DateTime UpdatedAt,
    DateTime? LastActivityAt,
    string LoyaltyAccountStatus = "Active",
    DateTime? WarningStartedAtUtc = null,
    DateTime? FrozenAtUtc = null,
    DateTime? ReactivatedAtUtc = null,
    string? FreezeReason = null,
    DateTime? LastQualifyingActivityAtUtc = null);

public sealed record LoyaltyTransactionResultDto(
    LoyaltyTransactionDto Transaction,
    LoyaltyAccountDto Account,
    decimal BalanceBefore,
    decimal BalanceAfter);

public sealed record LoyaltyDashboardDto(
    int AccountCount,
    decimal CurrentPointsTotal,
    decimal EarnedPointsTotal,
    decimal RedeemedPointsTotal,
    decimal ReversedPointsTotal,
    decimal AdjustedPointsTotal,
    int ActiveCustomerCount,
    IReadOnlyList<LoyaltyTopCustomerDto> TopCustomersByBalance,
    IReadOnlyList<LoyaltyTopCustomerDto> TopCustomersByEarnedPoints,
    IReadOnlyList<LoyaltyVipSummaryDto> VipLevels,
    IReadOnlyList<LoyaltyActivityDto> RecentActivities,
    LoyaltyProgramSummaryDto ProgramSummary);

public sealed record LoyaltyTopCustomerDto(
    int CustomerId,
    string? CustomerCode,
    string? CustomerName,
    decimal Points,
    int TransactionCount);

public sealed record LoyaltyVipSummaryDto(
    int VipLevelId,
    string? DisplayName,
    int CustomerCount,
    decimal MinimumPoints);

public sealed record LoyaltyActivityDto(
    long LoyaltyTransactionId,
    int CustomerId,
    string? CustomerCode,
    string? CustomerName,
    string TransactionType,
    decimal Points,
    int? OrderId,
    string? Notes,
    DateTime CreatedAt);

public sealed record LoyaltyProgramSummaryDto(
    int AccountCount,
    int TransactionCount,
    int RedemptionCount,
    decimal CurrentPointsTotal,
    int EarnCount,
    int RedeemCount,
    int ReversalCount,
    int AdjustCount);

public sealed record LoyaltyTransactionsScreenDto(
    LoyaltyTransactionsSummaryDto Summary,
    IReadOnlyList<LoyaltyTransactionListItemDto> Transactions);

public sealed record LoyaltyRewardsScreenDto(
    LoyaltyRewardsSummaryDto Summary,
    IReadOnlyList<LoyaltyRewardCustomerDto> TopCustomers,
    IReadOnlyList<LoyaltyRewardItemDto> LargestRewards,
    IReadOnlyList<LoyaltyRewardItemDto> Rewards);

public sealed record LoyaltyRewardsSummaryDto(
    int RewardCount,
    decimal GrantedPointsTotal,
    int BeneficiaryCustomerCount,
    decimal AverageRewardPoints,
    decimal LargestRewardPoints);

public sealed record LoyaltyRewardCustomerDto(
    int CustomerId,
    string? CustomerCode,
    string? CustomerName,
    decimal GrantedPoints,
    int RewardCount);

public sealed record LoyaltyRewardItemDto(
    long LoyaltyTransactionId,
    int LoyaltyAccountId,
    int CustomerId,
    string? CustomerCode,
    string? CustomerName,
    int? OrderId,
    int? RewardId,
    decimal Points,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string Source,
    string? Notes,
    DateTime CreatedAt,
    bool IsReversed);

public sealed record LoyaltyTransactionsSummaryDto(
    int TransactionCount,
    decimal EarnedPointsTotal,
    decimal RedeemedPointsTotal,
    decimal ReversedPointsTotal,
    int AdjustCount,
    int ActiveCustomerCount,
    IReadOnlyList<LoyaltyTransactionTypeSummaryDto> Types);

public sealed record LoyaltyTransactionTypeSummaryDto(
    string TransactionType,
    int TransactionCount,
    decimal PointsTotal);

public sealed record LoyaltyTransactionListItemDto(
    long LoyaltyTransactionId,
    int LoyaltyAccountId,
    int CustomerId,
    string? CustomerCode,
    string? CustomerName,
    int? OrderId,
    int? RewardId,
    string TransactionType,
    decimal Points,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string Source,
    string? Notes,
    DateTime CreatedAt);

public sealed class EnsureLoyaltyAccountRequest
{
    public int CustomerId { get; init; }
}

public sealed class CreateLoyaltyTransactionRequest
{
    public int CustomerId { get; init; }
    public int? OrderId { get; init; }
    public int? RewardId { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public decimal Points { get; init; }
    public string Source { get; init; } = string.Empty;
    public string? Notes { get; init; }
}
