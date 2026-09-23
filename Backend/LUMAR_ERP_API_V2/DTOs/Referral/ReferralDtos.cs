using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Referral;

public sealed record ReferralCodeDto(int ReferralCodeId, int CustomerId, string Code, bool IsActive, DateTime CreatedAt, DateTime? LastUsedAt);

public sealed record ReferralAccountDto(int ReferralAccountId, int CustomerId, int? ReferralCodeId, string? ReferralCode, bool? ReferralCodeIsActive, int TotalReferrals, int SuccessfulReferrals, decimal TotalRewardsAmount, decimal TotalRewardPoints, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ReferralCodeCreatedAt, DateTime? ReferralCodeLastUsedAt);

public sealed record ReferralTreeNodeDto(int CustomerId, string? CustomerCode, string? CustomerName, int? ParentCustomerId, int Level, IReadOnlyList<ReferralTreeNodeDto> Children, int DirectChildrenCount, int TotalDescendantsCount, int MaxDepth, string? ReferralCode, bool IsActive);

public sealed record ReferralTreeDto(int RootCustomerId, string? RootCustomerCode, string? RootCustomerName, int DirectReferralsCount, int TotalDescendantsCount, int MaxDepth, IReadOnlyList<ReferralTreeNodeDto> Children);

public sealed record ReferralRootDto(int CustomerId, string? CustomerCode, string? CustomerName, int DirectReferralsCount, int TotalDescendantsCount, int MaxDepth);

public sealed record ReferralTransactionDto(long ReferralTransactionId, int ReferrerCustomerId, int? ReferredCustomerId, int? ReferralCodeId, int? OrderId, int? ReferralRewardId, string TransactionType, decimal FixedRewardAmount, decimal LoyaltyPoints, string? Notes, DateTime CreatedAt);

public sealed record ReferralDashboardDto(
    int TotalRegistrations,
    int ParticipatingCustomers,
    int RewardsGranted,
    int RewardReversals,
    decimal TotalReferralPoints,
    IReadOnlyList<ReferralDashboardReferrerDto> TopReferrers,
    IReadOnlyList<ReferralDashboardCodeDto> TopCodes,
    IReadOnlyList<ReferralDashboardEventDto> RecentEvents,
    IReadOnlyList<ReferralDashboardReceiverDto> TopReceivers,
    ReferralDashboardTreeSummaryDto TreeSummary);

public sealed record ReferralDashboardReferrerDto(int CustomerId, string? CustomerCode, string? CustomerName, int ReferralCount);

public sealed record ReferralDashboardCodeDto(int ReferralCodeId, string Code, int CustomerId, string? CustomerName, int UsageCount);

public sealed record ReferralDashboardEventDto(long TransactionId, string TransactionType, int ReferrerCustomerId, string? ReferrerName, int? ReferredCustomerId, string? ReferredName, string? ReferralCode, decimal FixedRewardAmount, decimal LoyaltyPoints, DateTime CreatedAt);

public sealed record ReferralDashboardReceiverDto(int CustomerId, string? CustomerCode, string? CustomerName, int ReferralCount);

public sealed record ReferralDashboardTreeSummaryDto(int RootCount, int MaxDepth, int MaxDirectReferrals);

public sealed record ReferralDashboardSearchResultDto(int CustomerId, string? CustomerCode, string? CustomerName, string? ReferralCode, string ResultType);

public sealed record ReferralAnalyticsDto(
    int TotalRegistrations,
    int TotalReferredCustomers,
    int RewardsGranted,
    decimal TotalReferralPoints,
    decimal AverageReferralsPerCustomer,
    IReadOnlyList<ReferralAnalyticsReferrerDto> TopReferrers,
    IReadOnlyList<ReferralAnalyticsCodeDto> TopCodes,
    IReadOnlyList<ReferralAnalyticsRewardCustomerDto> TopRewardCustomers,
    ReferralAnalyticsQualityDto Quality,
    ReferralAnalyticsActivityDto Activity,
    ReferralAnalyticsTreeDto Tree);

public sealed record ReferralAnalyticsReferrerDto(int CustomerId, string? CustomerCode, string? CustomerName, int ReferralCount);

public sealed record ReferralAnalyticsCodeDto(int ReferralCodeId, string Code, int CustomerId, string? CustomerName, int UsageCount);

public sealed record ReferralAnalyticsRewardCustomerDto(int CustomerId, string? CustomerCode, string? CustomerName, decimal TotalPoints, int RewardCount);

public sealed record ReferralAnalyticsQualityDto(
    decimal AverageUsagePerCode,
    decimal ReferredCustomersRate,
    decimal RewardsToRegistrationsRate,
    decimal ReversalsToRewardsRate,
    int RewardReversals);

public sealed record ReferralAnalyticsActivityDto(
    int TodayRegistrations,
    int ThisWeekRegistrations,
    int ThisMonthRegistrations,
    IReadOnlyList<ReferralAnalyticsPeriodDto> DailyRegistrations);

public sealed record ReferralAnalyticsPeriodDto(DateTime Period, int RegistrationCount);

public sealed record ReferralAnalyticsTreeDto(
    int RootCount,
    int MaxDepth,
    int LargestNetworkSize,
    int? LargestNetworkCustomerId,
    string? LargestNetworkCustomerName);

public sealed record ReferralRewardsScreenDto(
    int GrantedCount,
    int ReversalCount,
    decimal TotalGrantedPoints,
    int BeneficiaryCount,
    IReadOnlyList<ReferralRewardBeneficiaryDto> TopBeneficiaries,
    IReadOnlyList<ReferralRewardEventDto> Events);

public sealed record ReferralRewardBeneficiaryDto(int CustomerId, string? CustomerCode, string? CustomerName, decimal TotalPoints, int EventCount);

public sealed record ReferralRewardEventDto(
    long TransactionId,
    int BeneficiaryCustomerId,
    string? BeneficiaryCode,
    string? BeneficiaryName,
    int? ReferredCustomerId,
    string? ReferredCustomerName,
    string TransactionType,
    decimal FixedRewardAmount,
    decimal LoyaltyPoints,
    int? OrderId,
    string? Notes,
    DateTime CreatedAt);

public sealed class EnsureReferralCodeRequest
{
    [Required] public int CustomerId { get; init; }
    [StringLength(50)] public string? PreferredCode { get; init; }
}

public sealed class RegisterReferralRequest
{
    [Required] public int ReferredCustomerId { get; init; }
    [Required] public string ReferralCode { get; init; } = string.Empty;
    [StringLength(200)] public string? Notes { get; init; }
}

public sealed record RegisterReferralResponse(long ReferralTransactionId, int ReferrerCustomerId, int ReferredCustomerId, int? ReferralCodeId, string TransactionType, DateTime CreatedAt, bool IsDuplicate, string Message);

public sealed class ReferralAccountSummaryDto
{
    public int CustomerId { get; init; }
    public int? ReferralCodeId { get; init; }
    public string? ReferralCode { get; init; }
    public bool ReferralCodeIsActive { get; init; }
    public int TotalReferrals { get; init; }
    public int SuccessfulReferrals { get; init; }
    public decimal TotalRewardsAmount { get; init; }
    public decimal TotalRewardPoints { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
