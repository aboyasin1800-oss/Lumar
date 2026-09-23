namespace LUMAR_ERP_API_V2.DTOs.Loyalty;

public sealed record LoyaltyPiecePointSettingDto(
    int LoyaltyPiecePointSettingId,
    string PieceCode,
    string PieceName,
    decimal Points,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int? ProductTypeId = null);

public sealed record ProductLoyaltyPointSettingDto(
    int ProductTypeId,
    string Code,
    string NameAr,
    string? Category,
    int? LoyaltyPiecePointSettingId,
    decimal? Points,
    bool? IsSettingActive,
    bool IsConfigured);

public sealed record UpdateProductLoyaltyPointSettingDto(
    decimal Points,
    bool IsActive = true);

public sealed record ReadyMadeProductTypeLoyaltyPointSettingDto(
    int ProductTypeId,
    string Code,
    string NameAr,
    string? Category,
    int? SettingId,
    decimal? Points,
    bool? IsSettingActive,
    bool IsConfigured);

public sealed record UpdateReadyMadeProductTypeLoyaltyPointSettingDto(
    decimal Points,
    bool IsActive = true);

public sealed record ImportedProductLoyaltyPointSettingDto(
    int ImportedReadyMadeProductId,
    string ProductName,
    string ProductType,
    string ProductCode,
    bool IsProductActive,
    int? SettingId,
    decimal? Points,
    bool? IsSettingActive,
    bool IsConfigured);

public sealed record UpdateImportedProductLoyaltyPointSettingDto(
    decimal Points,
    bool IsActive = true);

public sealed record LoyaltyProgramSettingsDto(
    int LoyaltyProgramSettingId,
    bool IsEnabled,
    decimal PointsPerPiece,
    decimal PointMonetaryValue,
    DateTime EffectiveFromUtc,
    DateTime UpdatedAtUtc,
    bool AllowRedemption = true,
    decimal MinimumRedemptionPoints = 0m,
    decimal MaximumRedemptionPoints = 0m,
    bool LoyaltyFreezeEnabled = false,
    int GracePeriodDays = 180,
    int WarningPeriodDays = 30,
    bool ManualReactivationEnabled = true,
    bool PurchaseReactivationEnabled = true);

public sealed record PiecePointCalculationRequest(
    string PieceCode,
    decimal Quantity,
    decimal? OverridePoints,
    string? Source,
    string? Notes);

public sealed record OfficialPiecePointCalculationRequest(
    int? ProductTypeId,
    int? ImportedReadyMadeProductId,
    decimal Quantity,
    string? Source,
    string? Notes);

public sealed record ReferralRewardDistributionDto(
    int Level,
    decimal Percentage,
    decimal Amount,
    decimal? BuyerEarn,
    int ReferrerCustomerId,
    int ReferredCustomerId,
    string Fingerprint);

public sealed record PiecePointsResultDto(
    decimal BasePoints,
    decimal BuyerEarn,
    IReadOnlyList<decimal> ReferralLevels,
    string Fingerprint,
    string ReconciliationKey);
