using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class LoyaltyManagementSettingsTests
{
    [Fact]
    public async Task Service_Can_Manage_Program_Settings_And_Toggle_Activation()
    {
        var service = new FakeLoyaltyManagementSettingsService();

        var created = await service.CreateProgramSettingsAsync(new CreateLoyaltyProgramSettingsDto(true, 100m, 1m, DateTime.UtcNow), CancellationToken.None);
        Assert.NotNull(created);
        Assert.True(created!.IsEnabled);

        var updated = await service.UpdateProgramSettingsAsync(created.LoyaltyProgramSettingId, new UpdateLoyaltyProgramSettingsDto(false, 80m, 1.5m, DateTime.UtcNow.AddDays(1)), CancellationToken.None);
        Assert.NotNull(updated);
        Assert.False(updated!.IsEnabled);
        Assert.Equal(80m, updated.PointsPerPiece);

        var toggled = await service.DeactivateProgramSettingsAsync(created.LoyaltyProgramSettingId, CancellationToken.None);
        Assert.NotNull(toggled);
        Assert.False(toggled!.IsEnabled);
    }

    [Fact]
    public async Task Service_Can_Manage_Vip_Levels_And_Loyalty_Rules()
    {
        var service = new FakeLoyaltyManagementSettingsService();

        var vip = await service.CreateVipLevelAsync(new CreateVipLevelDto("GOLD", "Gold", 5000m, 1.5m, 10, true), CancellationToken.None);
        Assert.NotNull(vip);
        Assert.Equal("GOLD", vip!.Code);

        var rule = await service.CreateLoyaltyRuleAsync(new CreateLoyaltyRuleDto("Weekend Bonus", "Bonus", 50m, 1000m, 1.2m, 5m, 5, vip.VipLevelId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), true), CancellationToken.None);
        Assert.NotNull(rule);
        Assert.Equal("Weekend Bonus", rule!.RuleName);

        var updatedRule = await service.UpdateLoyaltyRuleAsync(rule.LoyaltyRuleId, new UpdateLoyaltyRuleDto("Weekend Bonus", "Bonus", 75m, 1200m, 1.3m, 10m, 6, vip.VipLevelId, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(40), true), CancellationToken.None);
        Assert.NotNull(updatedRule);
        Assert.Equal(75m, updatedRule!.PointsValue);

        var deactivated = await service.DeactivateLoyaltyRuleAsync(rule.LoyaltyRuleId, CancellationToken.None);
        Assert.NotNull(deactivated);
        Assert.False(deactivated!.IsActive);
    }

    private sealed class FakeLoyaltyManagementSettingsService : ILoyaltyManagementSettingsService
    {
        private int _programSettingId = 1;
        private int _vipLevelId = 1;
        private int _ruleId = 1;

        public Task<IReadOnlyList<LoyaltyProgramSettingsDto>> GetProgramSettingsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LoyaltyProgramSettingsDto>>(new[] { new LoyaltyProgramSettingsDto(1, true, 100m, 1m, DateTime.UtcNow, DateTime.UtcNow) });

        public Task<LoyaltyProgramSettingsDto?> GetProgramSettingByIdAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult<LoyaltyProgramSettingsDto?>(new LoyaltyProgramSettingsDto(id, true, 100m, 1m, DateTime.UtcNow, DateTime.UtcNow));

        public Task<LoyaltyProgramSettingsDto?> CreateProgramSettingsAsync(CreateLoyaltyProgramSettingsDto request, CancellationToken cancellationToken)
        {
            var dto = new LoyaltyProgramSettingsDto(_programSettingId++, request.IsEnabled, request.PointsPerPiece, request.PointMonetaryValue, request.EffectiveFromUtc, DateTime.UtcNow);
            return Task.FromResult<LoyaltyProgramSettingsDto?>(dto);
        }

        public Task<LoyaltyProgramSettingsDto?> UpdateProgramSettingsAsync(int id, UpdateLoyaltyProgramSettingsDto request, CancellationToken cancellationToken)
            => Task.FromResult<LoyaltyProgramSettingsDto?>(new LoyaltyProgramSettingsDto(id, request.IsEnabled, request.PointsPerPiece, request.PointMonetaryValue, request.EffectiveFromUtc, DateTime.UtcNow));

        public Task<LoyaltyProgramSettingsDto?> ActivateProgramSettingsAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult<LoyaltyProgramSettingsDto?>(new LoyaltyProgramSettingsDto(id, true, 100m, 1m, DateTime.UtcNow, DateTime.UtcNow));

        public Task<LoyaltyProgramSettingsDto?> DeactivateProgramSettingsAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult<LoyaltyProgramSettingsDto?>(new LoyaltyProgramSettingsDto(id, false, 100m, 1m, DateTime.UtcNow, DateTime.UtcNow));

        public Task<IReadOnlyList<LoyaltyPiecePointSettingDto>> GetPiecePointSettingsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LoyaltyPiecePointSettingDto>>(Array.Empty<LoyaltyPiecePointSettingDto>());

        public Task<LoyaltyPiecePointSettingDto?> GetPiecePointSettingByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<LoyaltyPiecePointSettingDto?>(null);
        public Task<LoyaltyPiecePointSettingDto?> CreatePiecePointSettingAsync(CreateLoyaltyPiecePointSettingDto request, CancellationToken cancellationToken) => Task.FromResult<LoyaltyPiecePointSettingDto?>(new LoyaltyPiecePointSettingDto(1, request.PieceCode, request.PieceName, request.Points, request.IsActive, DateTime.UtcNow, DateTime.UtcNow));
        public Task<LoyaltyPiecePointSettingDto?> UpdatePiecePointSettingAsync(int id, UpdateLoyaltyPiecePointSettingDto request, CancellationToken cancellationToken) => Task.FromResult<LoyaltyPiecePointSettingDto?>(new LoyaltyPiecePointSettingDto(id, request.PieceCode, request.PieceName, request.Points, request.IsActive, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));
        public Task<LoyaltyPiecePointSettingDto?> ActivatePiecePointSettingAsync(int id, CancellationToken cancellationToken) => Task.FromResult<LoyaltyPiecePointSettingDto?>(new LoyaltyPiecePointSettingDto(id, "YALAQ", "YALAQ", 100m, true, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));
        public Task<LoyaltyPiecePointSettingDto?> DeactivatePiecePointSettingAsync(int id, CancellationToken cancellationToken) => Task.FromResult<LoyaltyPiecePointSettingDto?>(new LoyaltyPiecePointSettingDto(id, "YALAQ", "YALAQ", 100m, false, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));

        public Task<IReadOnlyList<VipLevelDto>> GetVipLevelsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<VipLevelDto>>(new[] { new VipLevelDto(1, "GOLD", "Gold", 5000m, 1.5m, 10, true, DateTime.UtcNow, DateTime.UtcNow) });

        public Task<VipLevelDto?> GetVipLevelByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<VipLevelDto?>(new VipLevelDto(id, "GOLD", "Gold", 5000m, 1.5m, 10, true, DateTime.UtcNow, DateTime.UtcNow));
        public Task<VipLevelDto?> CreateVipLevelAsync(CreateVipLevelDto request, CancellationToken cancellationToken)
        {
            var dto = new VipLevelDto(_vipLevelId++, request.Code, request.DisplayName, request.MinimumPoints, request.Multiplier, request.Priority, request.IsActive, DateTime.UtcNow, DateTime.UtcNow);
            return Task.FromResult<VipLevelDto?>(dto);
        }

        public Task<VipLevelDto?> UpdateVipLevelAsync(int id, UpdateVipLevelDto request, CancellationToken cancellationToken)
            => Task.FromResult<VipLevelDto?>(new VipLevelDto(id, request.Code, request.DisplayName, request.MinimumPoints, request.Multiplier, request.Priority, request.IsActive, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));

        public Task<VipLevelDto?> ActivateVipLevelAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult<VipLevelDto?>(new VipLevelDto(id, "GOLD", "Gold", 5000m, 1.5m, 10, true, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));

        public Task<VipLevelDto?> DeactivateVipLevelAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult<VipLevelDto?>(new VipLevelDto(id, "GOLD", "Gold", 5000m, 1.5m, 10, false, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));

        public Task<IReadOnlyList<LoyaltyRuleDto>> GetLoyaltyRulesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LoyaltyRuleDto>>(Array.Empty<LoyaltyRuleDto>());

        public Task<LoyaltyRuleDto?> GetLoyaltyRuleByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<LoyaltyRuleDto?>(null);
        public Task<LoyaltyRuleDto?> CreateLoyaltyRuleAsync(CreateLoyaltyRuleDto request, CancellationToken cancellationToken)
        {
            var dto = new LoyaltyRuleDto(_ruleId++, request.RuleName, request.RuleType, request.IsActive, request.PointsValue, request.SpendingAmount, request.Multiplier, request.BonusPoints, request.Priority, request.VipLevelId, request.StartDate, request.EndDate, DateTime.UtcNow, DateTime.UtcNow);
            return Task.FromResult<LoyaltyRuleDto?>(dto);
        }

        public Task<LoyaltyRuleDto?> UpdateLoyaltyRuleAsync(int id, UpdateLoyaltyRuleDto request, CancellationToken cancellationToken)
            => Task.FromResult<LoyaltyRuleDto?>(new LoyaltyRuleDto(id, request.RuleName, request.RuleType, request.IsActive, request.PointsValue, request.SpendingAmount, request.Multiplier, request.BonusPoints, request.Priority, request.VipLevelId, request.StartDate, request.EndDate, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));

        public Task<LoyaltyRuleDto?> ActivateLoyaltyRuleAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult<LoyaltyRuleDto?>(new LoyaltyRuleDto(id, "Weekend Bonus", "Bonus", true, 50m, 1000m, 1.2m, 5m, 5, 1, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));

        public Task<LoyaltyRuleDto?> DeactivateLoyaltyRuleAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult<LoyaltyRuleDto?>(new LoyaltyRuleDto(id, "Weekend Bonus", "Bonus", false, 50m, 1000m, 1.2m, 5m, 5, 1, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));

        public Task<IReadOnlyList<ReferralRewardDto>> GetReferralRewardsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ReferralRewardDto>>(Array.Empty<ReferralRewardDto>());

        public Task<ReferralRewardDto?> GetReferralRewardByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<ReferralRewardDto?>(null);
        public Task<ReferralRewardDto?> CreateReferralRewardAsync(CreateReferralRewardDto request, CancellationToken cancellationToken) => Task.FromResult<ReferralRewardDto?>(new ReferralRewardDto(1, request.RewardCode, request.RewardName, request.RewardType, request.FixedAmount, request.LoyaltyPoints, request.BonusMultiplier, request.MinSuccessfulReferrals, request.IsActive, DateTime.UtcNow, DateTime.UtcNow));
        public Task<ReferralRewardDto?> UpdateReferralRewardAsync(int id, UpdateReferralRewardDto request, CancellationToken cancellationToken) => Task.FromResult<ReferralRewardDto?>(new ReferralRewardDto(id, request.RewardCode, request.RewardName, request.RewardType, request.FixedAmount, request.LoyaltyPoints, request.BonusMultiplier, request.MinSuccessfulReferrals, request.IsActive, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));
        public Task<ReferralRewardDto?> ActivateReferralRewardAsync(int id, CancellationToken cancellationToken) => Task.FromResult<ReferralRewardDto?>(new ReferralRewardDto(id, "REF-001", "Referral Bonus", "Fixed", 50m, 100m, 1.0m, 1, true, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));
        public Task<ReferralRewardDto?> DeactivateReferralRewardAsync(int id, CancellationToken cancellationToken) => Task.FromResult<ReferralRewardDto?>(new ReferralRewardDto(id, "REF-001", "Referral Bonus", "Fixed", 50m, 100m, 1.0m, 1, false, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));
    }
}
