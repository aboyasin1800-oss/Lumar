namespace LUMAR_ERP_API_V2.DTOs.Loyalty;

public sealed record LoyaltyRedemptionPreviewDto(
    int CustomerId,
    int OrderId,
    decimal CurrentPoints,
    decimal RemainingOrderAmount,
    decimal PointsRedeemed,
    decimal PointMonetaryValue,
    decimal CreditAmount,
    bool IsValid,
    string? ValidationMessage);

public sealed record LoyaltyRedemptionDto(
    int LoyaltyRedemptionId,
    int CustomerId,
    int OrderId,
    decimal PointsRedeemed,
    decimal PointMonetaryValue,
    decimal CreditAmount,
    string Status,
    DateTime CreatedAt,
    DateTime? ReversedAtUtc,
    long? ReversalLoyaltyTransactionId);

public sealed record LoyaltyRedemptionHistoryItemDto(
    long LoyaltyTransactionId,
    string TransactionType,
    int CustomerId,
    string? CustomerCode,
    string? CustomerName,
    int? OrderId,
    string? OrderNumber,
    decimal Points,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string Source,
    string? Notes,
    DateTime CreatedAt,
    long? LoyaltyRedemptionId,
    decimal? PointsRedeemed,
    decimal? PointMonetaryValue,
    decimal? CreditAmount,
    string? ReferenceNumber,
    DateTime? ReversedAtUtc,
    long? ReversalLoyaltyTransactionId,
    long? CustomerLedgerEntryId,
    string? FinancialReference);

public sealed record LoyaltyRedemptionHistorySummaryDto(
    int RedemptionCount,
    decimal RedeemedPointsTotal,
    decimal CreditAmountTotal,
    int ReversalCount,
    int CustomerCount);

public sealed record LoyaltyRedemptionHistoryDto(
    LoyaltyRedemptionHistorySummaryDto Summary,
    IReadOnlyList<LoyaltyRedemptionHistoryItemDto> Items);

public sealed record LoyaltyCreditDto(
    int LoyaltyCreditId,
    int CustomerId,
    int OrderId,
    int LoyaltyRedemptionId,
    decimal CreditAmount,
    decimal PointsRedeemed,
    string Source,
    string? ReferenceNumber,
    DateTime CreatedAt,
    bool Reversed,
    DateTime? ReversedAtUtc);

public sealed record LoyaltyRedemptionResultDto(
    LoyaltyRedemptionDto Redemption,
    LoyaltyTransactionDto RedeemTransaction,
    decimal CreditAmount,
    decimal NewBalanceAfterRedeem,
    decimal RemainingOrderAmountBefore = 0m,
    decimal RemainingOrderAmountAfter = 0m);

public sealed record RewardReversalResultDto(
    int Level,
    int ReferrerCustomerId,
    int ReferredCustomerId,
    decimal Points,
    string TransactionType,
    string Notes);

public sealed class RedeemLoyaltyRequest
{
    public int CustomerId { get; init; }
    public int OrderId { get; init; }
    public decimal PointsRedeemed { get; init; }
    public decimal PointMonetaryValue { get; init; } = 1m;
}

public sealed class ReverseLoyaltyRedemptionRequest
{
    public int RedemptionId { get; init; }
}
