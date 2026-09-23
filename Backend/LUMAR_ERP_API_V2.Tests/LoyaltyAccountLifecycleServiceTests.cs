using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class LoyaltyAccountLifecycleServiceTests
{
    [Fact]
    public async Task EvaluateAsync_MovesAccountToWarningBeforeFreeze()
    {
        var account = Account(DateTime.UtcNow.AddDays(-170));
        var repository = new FakeLoyaltyRepository(account);
        var settings = new FakeSettingsRepository(new LoyaltyProgramSettingsDto(1, true, 0m, 1m, DateTime.UtcNow, DateTime.UtcNow, true, 0m, 0m, true, 180, 30, true, true));
        var service = new LoyaltyAccountLifecycleService(repository, settings);

        var updated = await service.EvaluateAsync(account.CustomerId, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Warning", updated!.LoyaltyAccountStatus);
        Assert.Equal(account.CurrentPoints, updated.CurrentPoints);
    }

    [Fact]
    public async Task EvaluateAsync_FreezesWithoutChangingBalance()
    {
        var account = Account(DateTime.UtcNow.AddDays(-181));
        var repository = new FakeLoyaltyRepository(account);
        var settings = new FakeSettingsRepository(new LoyaltyProgramSettingsDto(1, true, 0m, 1m, DateTime.UtcNow, DateTime.UtcNow, true, 0m, 0m, true, 180, 30, true, true));
        var service = new LoyaltyAccountLifecycleService(repository, settings);

        var updated = await service.EvaluateAsync(account.CustomerId, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Frozen", updated!.LoyaltyAccountStatus);
        Assert.Equal(account.CurrentPoints, updated.CurrentPoints);
        Assert.Equal(account.LifetimeEarnedPoints, updated.LifetimeEarnedPoints);
    }

    [Fact]
    public async Task ReactivateManuallyAsync_RestoresActiveWithoutChangingBalance()
    {
        var account = Account(DateTime.UtcNow.AddDays(-181)) with { LoyaltyAccountStatus = "Frozen", FrozenAtUtc = DateTime.UtcNow.AddDays(-1) };
        var repository = new FakeLoyaltyRepository(account);
        var settings = new FakeSettingsRepository(new LoyaltyProgramSettingsDto(1, true, 0m, 1m, DateTime.UtcNow, DateTime.UtcNow, true, 0m, 0m, true, 180, 30, true, true));
        var service = new LoyaltyAccountLifecycleService(repository, settings);

        var updated = await service.ReactivateManuallyAsync(account.CustomerId, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Active", updated!.LoyaltyAccountStatus);
        Assert.Null(updated.FrozenAtUtc);
        Assert.Equal(account.CurrentPoints, updated.CurrentPoints);
    }

    [Fact]
    public async Task PrepareQualifyingPurchaseAsync_DoesNotPersistReactivationBeforePurchase()
    {
        var account = Account(DateTime.UtcNow.AddDays(-181)) with { LoyaltyAccountStatus = "Frozen", FrozenAtUtc = DateTime.UtcNow.AddDays(-1) };
        var repository = new FakeLoyaltyRepository(account);
        var settings = new FakeSettingsRepository(new LoyaltyProgramSettingsDto(1, true, 0m, 1m, DateTime.UtcNow, DateTime.UtcNow, true, 0m, 0m, true, 180, 30, true, true));
        var service = new LoyaltyAccountLifecycleService(repository, settings);

        var prepared = await service.PrepareQualifyingPurchaseAsync(account.CustomerId, CancellationToken.None);
        var persisted = await repository.GetAccountByCustomerAsync(account.CustomerId, CancellationToken.None);

        Assert.NotNull(prepared);
        Assert.Equal("Active", prepared!.LoyaltyAccountStatus);
        Assert.Equal(0, repository.LifecycleUpdateCount);
        Assert.Equal("Frozen", persisted!.LoyaltyAccountStatus);
    }

    private static LoyaltyAccountDto Account(DateTime createdAt) => new(1, 42, 1000m, 1200m, 200m, 0m, null, createdAt, createdAt, createdAt, "Active");

    private sealed class FakeLoyaltyRepository(LoyaltyAccountDto account) : ILoyaltyRepository
    {
        private LoyaltyAccountDto _account = account;
        public int LifecycleUpdateCount { get; private set; }
        public Task<LoyaltyDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyTransactionsScreenDto> GetTransactionsScreenAsync(string? search, string? transactionType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyRewardsScreenDto> GetRewardsScreenAsync(string? search, string? rewardType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyAccountDto?> GetAccountByCustomerAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult<LoyaltyAccountDto?>(_account.CustomerId == customerId ? _account : null);
        public Task<LoyaltyAccountDto> EnsureAccountAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult(_account);
        public Task<IReadOnlyList<LoyaltyAccountDto>> GetAllAccountsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LoyaltyAccountDto>>([_account]);
        public Task<LoyaltyAccountDto?> UpdateAccountLifecycleAsync(int customerId, string status, DateTime? warningStartedAtUtc, DateTime? frozenAtUtc, DateTime? reactivatedAtUtc, string? freezeReason, DateTime? lastQualifyingActivityAtUtc, CancellationToken cancellationToken)
        {
            LifecycleUpdateCount++;
            _account = _account with { LoyaltyAccountStatus = status, WarningStartedAtUtc = warningStartedAtUtc, FrozenAtUtc = frozenAtUtc, ReactivatedAtUtc = reactivatedAtUtc, FreezeReason = freezeReason, LastQualifyingActivityAtUtc = lastQualifyingActivityAtUtc, UpdatedAt = DateTime.UtcNow };
            return Task.FromResult<LoyaltyAccountDto?>(_account);
        }
        public Task<IReadOnlyList<LoyaltyTransactionDto>> GetTransactionsByCustomerAsync(int customerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyBalanceDto> GetBalanceAsync(int customerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyTransactionResultDto> CreateTransactionAsync(int customerId, string transactionType, decimal points, int? orderId, int? rewardId, string source, string? notes, CancellationToken cancellationToken, bool allowFrozenQualifyingPurchase = false) => throw new NotSupportedException();
    }

    private sealed class FakeSettingsRepository(LoyaltyProgramSettingsDto settings) : ILoyaltyManagementSettingsRepository
    {
        public Task<IReadOnlyList<LoyaltyProgramSettingsDto>> GetProgramSettingsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LoyaltyProgramSettingsDto>>([settings]);
        public Task<LoyaltyProgramSettingsDto?> GetProgramSettingByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<LoyaltyProgramSettingsDto?>(settings);
        public Task<LoyaltyProgramSettingsDto?> CreateProgramSettingsAsync(CreateLoyaltyProgramSettingsDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyProgramSettingsDto?> UpdateProgramSettingsAsync(int id, UpdateLoyaltyProgramSettingsDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyProgramSettingsDto?> ActivateProgramSettingsAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyProgramSettingsDto?> DeactivateProgramSettingsAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LoyaltyPiecePointSettingDto>> GetPiecePointSettingsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyPiecePointSettingDto?> GetPiecePointSettingByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyPiecePointSettingDto?> CreatePiecePointSettingAsync(CreateLoyaltyPiecePointSettingDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyPiecePointSettingDto?> UpdatePiecePointSettingAsync(int id, UpdateLoyaltyPiecePointSettingDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyPiecePointSettingDto?> ActivatePiecePointSettingAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyPiecePointSettingDto?> DeactivatePiecePointSettingAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<VipLevelDto>> GetVipLevelsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<VipLevelDto?> GetVipLevelByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<VipLevelDto?> CreateVipLevelAsync(CreateVipLevelDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<VipLevelDto?> UpdateVipLevelAsync(int id, UpdateVipLevelDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<VipLevelDto?> ActivateVipLevelAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<VipLevelDto?> DeactivateVipLevelAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LoyaltyRuleDto>> GetLoyaltyRulesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyRuleDto?> GetLoyaltyRuleByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyRuleDto?> CreateLoyaltyRuleAsync(CreateLoyaltyRuleDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyRuleDto?> UpdateLoyaltyRuleAsync(int id, UpdateLoyaltyRuleDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyRuleDto?> ActivateLoyaltyRuleAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyRuleDto?> DeactivateLoyaltyRuleAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReferralRewardDto>> GetReferralRewardsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReferralRewardDto?> GetReferralRewardByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReferralRewardDto?> CreateReferralRewardAsync(CreateReferralRewardDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReferralRewardDto?> UpdateReferralRewardAsync(int id, UpdateReferralRewardDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReferralRewardDto?> ActivateReferralRewardAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReferralRewardDto?> DeactivateReferralRewardAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
