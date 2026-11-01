namespace LUMAR_ERP_API_V2.Repositories;

public sealed record ReferralRewardMutation(
    int ReferrerCustomerId,
    int ReferredCustomerId,
    int? ReferralCodeId,
    int OrderId,
    string TransactionType,
    decimal FixedRewardAmount,
    decimal LoyaltyPoints,
    string Notes);