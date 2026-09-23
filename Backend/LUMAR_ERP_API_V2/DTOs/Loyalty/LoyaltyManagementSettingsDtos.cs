namespace LUMAR_ERP_API_V2.DTOs.Loyalty;

public sealed record CreateLoyaltyProgramSettingsDto(
    bool IsEnabled,
    decimal PointsPerPiece,
    decimal PointMonetaryValue,
    DateTime EffectiveFromUtc,
    bool AllowRedemption = true,
    decimal MinimumRedemptionPoints = 0m,
    decimal MaximumRedemptionPoints = 0m,
    bool LoyaltyFreezeEnabled = false,
    int GracePeriodDays = 180,
    int WarningPeriodDays = 30,
    bool ManualReactivationEnabled = true,
    bool PurchaseReactivationEnabled = true);

public sealed record UpdateLoyaltyProgramSettingsDto(
    bool IsEnabled,
    decimal PointsPerPiece,
    decimal PointMonetaryValue,
    DateTime EffectiveFromUtc,
    bool AllowRedemption = true,
    decimal MinimumRedemptionPoints = 0m,
    decimal MaximumRedemptionPoints = 0m,
    bool LoyaltyFreezeEnabled = false,
    int GracePeriodDays = 180,
    int WarningPeriodDays = 30,
    bool ManualReactivationEnabled = true,
    bool PurchaseReactivationEnabled = true);

public sealed record CreateLoyaltyPiecePointSettingDto(
    string PieceCode,
    string PieceName,
    decimal Points,
    bool IsActive = true);

public sealed record UpdateLoyaltyPiecePointSettingDto(
    string PieceCode,
    string PieceName,
    decimal Points,
    bool IsActive);

public sealed record VipLevelDto(
    int VipLevelId,
    string Code,
    string DisplayName,
    decimal MinimumPoints,
    decimal Multiplier,
    int Priority,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateVipLevelDto(
    string Code,
    string DisplayName,
    decimal MinimumPoints,
    decimal Multiplier,
    int Priority,
    bool IsActive = true);

public sealed record UpdateVipLevelDto(
    string Code,
    string DisplayName,
    decimal MinimumPoints,
    decimal Multiplier,
    int Priority,
    bool IsActive);

public sealed record LoyaltyRuleDto(
    int LoyaltyRuleId,
    string RuleName,
    string RuleType,
    bool IsActive,
    decimal PointsValue,
    decimal SpendingAmount,
    decimal Multiplier,
    decimal BonusPoints,
    int Priority,
    int? VipLevelId,
    DateTime? StartDate,
    DateTime? EndDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateLoyaltyRuleDto(
    string RuleName,
    string RuleType,
    decimal PointsValue,
    decimal SpendingAmount,
    decimal Multiplier,
    decimal BonusPoints,
    int Priority,
    int? VipLevelId,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive = true);

public sealed record UpdateLoyaltyRuleDto(
    string RuleName,
    string RuleType,
    decimal PointsValue,
    decimal SpendingAmount,
    decimal Multiplier,
    decimal BonusPoints,
    int Priority,
    int? VipLevelId,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive);

public sealed record ReferralRewardDto(
    int ReferralRewardId,
    string RewardCode,
    string RewardName,
    string RewardType,
    decimal FixedAmount,
    decimal LoyaltyPoints,
    decimal BonusMultiplier,
    int MinSuccessfulReferrals,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateReferralRewardDto(
    string RewardCode,
    string RewardName,
    string RewardType,
    decimal FixedAmount,
    decimal LoyaltyPoints,
    decimal BonusMultiplier,
    int MinSuccessfulReferrals,
    bool IsActive = true);

public sealed record UpdateReferralRewardDto(
    string RewardCode,
    string RewardName,
    string RewardType,
    decimal FixedAmount,
    decimal LoyaltyPoints,
    decimal BonusMultiplier,
    int MinSuccessfulReferrals,
    bool IsActive);
