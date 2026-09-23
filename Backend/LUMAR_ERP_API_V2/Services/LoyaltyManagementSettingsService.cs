using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class LoyaltyManagementSettingsService(ILoyaltyManagementSettingsRepository repository) : ILoyaltyManagementSettingsService
{
    public Task<IReadOnlyList<LoyaltyProgramSettingsDto>> GetProgramSettingsAsync(CancellationToken cancellationToken) => repository.GetProgramSettingsAsync(cancellationToken);
    public Task<LoyaltyProgramSettingsDto?> GetProgramSettingByIdAsync(int id, CancellationToken cancellationToken) => repository.GetProgramSettingByIdAsync(id, cancellationToken);
    public Task<LoyaltyProgramSettingsDto?> CreateProgramSettingsAsync(CreateLoyaltyProgramSettingsDto request, CancellationToken cancellationToken) => repository.CreateProgramSettingsAsync(request, cancellationToken);
    public Task<LoyaltyProgramSettingsDto?> UpdateProgramSettingsAsync(int id, UpdateLoyaltyProgramSettingsDto request, CancellationToken cancellationToken) => repository.UpdateProgramSettingsAsync(id, request, cancellationToken);
    public Task<LoyaltyProgramSettingsDto?> ActivateProgramSettingsAsync(int id, CancellationToken cancellationToken) => repository.ActivateProgramSettingsAsync(id, cancellationToken);
    public Task<LoyaltyProgramSettingsDto?> DeactivateProgramSettingsAsync(int id, CancellationToken cancellationToken) => repository.DeactivateProgramSettingsAsync(id, cancellationToken);

    public Task<IReadOnlyList<LoyaltyPiecePointSettingDto>> GetPiecePointSettingsAsync(CancellationToken cancellationToken) => repository.GetPiecePointSettingsAsync(cancellationToken);
    public Task<LoyaltyPiecePointSettingDto?> GetPiecePointSettingByIdAsync(int id, CancellationToken cancellationToken) => repository.GetPiecePointSettingByIdAsync(id, cancellationToken);
    public Task<LoyaltyPiecePointSettingDto?> CreatePiecePointSettingAsync(CreateLoyaltyPiecePointSettingDto request, CancellationToken cancellationToken) => repository.CreatePiecePointSettingAsync(request, cancellationToken);
    public Task<LoyaltyPiecePointSettingDto?> UpdatePiecePointSettingAsync(int id, UpdateLoyaltyPiecePointSettingDto request, CancellationToken cancellationToken) => repository.UpdatePiecePointSettingAsync(id, request, cancellationToken);
    public Task<LoyaltyPiecePointSettingDto?> ActivatePiecePointSettingAsync(int id, CancellationToken cancellationToken) => repository.ActivatePiecePointSettingAsync(id, cancellationToken);
    public Task<LoyaltyPiecePointSettingDto?> DeactivatePiecePointSettingAsync(int id, CancellationToken cancellationToken) => repository.DeactivatePiecePointSettingAsync(id, cancellationToken);

    public Task<IReadOnlyList<VipLevelDto>> GetVipLevelsAsync(CancellationToken cancellationToken) => repository.GetVipLevelsAsync(cancellationToken);
    public Task<VipLevelDto?> GetVipLevelByIdAsync(int id, CancellationToken cancellationToken) => repository.GetVipLevelByIdAsync(id, cancellationToken);
    public Task<VipLevelDto?> CreateVipLevelAsync(CreateVipLevelDto request, CancellationToken cancellationToken) => repository.CreateVipLevelAsync(request, cancellationToken);
    public Task<VipLevelDto?> UpdateVipLevelAsync(int id, UpdateVipLevelDto request, CancellationToken cancellationToken) => repository.UpdateVipLevelAsync(id, request, cancellationToken);
    public Task<VipLevelDto?> ActivateVipLevelAsync(int id, CancellationToken cancellationToken) => repository.ActivateVipLevelAsync(id, cancellationToken);
    public Task<VipLevelDto?> DeactivateVipLevelAsync(int id, CancellationToken cancellationToken) => repository.DeactivateVipLevelAsync(id, cancellationToken);

    public Task<IReadOnlyList<LoyaltyRuleDto>> GetLoyaltyRulesAsync(CancellationToken cancellationToken) => repository.GetLoyaltyRulesAsync(cancellationToken);
    public Task<LoyaltyRuleDto?> GetLoyaltyRuleByIdAsync(int id, CancellationToken cancellationToken) => repository.GetLoyaltyRuleByIdAsync(id, cancellationToken);
    public Task<LoyaltyRuleDto?> CreateLoyaltyRuleAsync(CreateLoyaltyRuleDto request, CancellationToken cancellationToken) => repository.CreateLoyaltyRuleAsync(request, cancellationToken);
    public Task<LoyaltyRuleDto?> UpdateLoyaltyRuleAsync(int id, UpdateLoyaltyRuleDto request, CancellationToken cancellationToken) => repository.UpdateLoyaltyRuleAsync(id, request, cancellationToken);
    public Task<LoyaltyRuleDto?> ActivateLoyaltyRuleAsync(int id, CancellationToken cancellationToken) => repository.ActivateLoyaltyRuleAsync(id, cancellationToken);
    public Task<LoyaltyRuleDto?> DeactivateLoyaltyRuleAsync(int id, CancellationToken cancellationToken) => repository.DeactivateLoyaltyRuleAsync(id, cancellationToken);

    public Task<IReadOnlyList<ReferralRewardDto>> GetReferralRewardsAsync(CancellationToken cancellationToken) => repository.GetReferralRewardsAsync(cancellationToken);
    public Task<ReferralRewardDto?> GetReferralRewardByIdAsync(int id, CancellationToken cancellationToken) => repository.GetReferralRewardByIdAsync(id, cancellationToken);
    public Task<ReferralRewardDto?> CreateReferralRewardAsync(CreateReferralRewardDto request, CancellationToken cancellationToken) => repository.CreateReferralRewardAsync(request, cancellationToken);
    public Task<ReferralRewardDto?> UpdateReferralRewardAsync(int id, UpdateReferralRewardDto request, CancellationToken cancellationToken) => repository.UpdateReferralRewardAsync(id, request, cancellationToken);
    public Task<ReferralRewardDto?> ActivateReferralRewardAsync(int id, CancellationToken cancellationToken) => repository.ActivateReferralRewardAsync(id, cancellationToken);
    public Task<ReferralRewardDto?> DeactivateReferralRewardAsync(int id, CancellationToken cancellationToken) => repository.DeactivateReferralRewardAsync(id, cancellationToken);
}
