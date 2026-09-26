using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class LoyaltyRedemptionServiceTests
{
    [Theory]
    [InlineData(false, 100, 0, 0, false, "disabled")]
    [InlineData(true, 50, 100, 0, false, "minimum")]
    [InlineData(true, 150, 0, 100, false, "maximum")]
    [InlineData(true, 150, 0, 0, true, "valid")]
    public async Task Preview_EnforcesConfiguredRedemptionLimits(
        bool allowRedemption,
        decimal requestedPoints,
        decimal minimumPoints,
        decimal maximumPoints,
        bool expectedValid,
        string expectedMessage)
    {
        var loyaltyRepository = new FakeLoyaltyRepository();
        await loyaltyRepository.CreateTransactionAsync(10, "Earn", 1000m, null, null, "Seed", "Seed", CancellationToken.None);
        var orderRepository = new FakeOrderRepository(new OrderDetailsDto(
            1003,
            "ORD-1003",
            10,
            "Ali",
            "0500000000",
            DateTime.UtcNow,
            null,
            1000m,
            0m,
            0m,
            1000m,
            "Normal",
            "Confirmed",
            "Test order",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null),
            []);
        var settingsRepository = new FakeSettingsRepository(new LoyaltyProgramSettingsDto(
            1,
            true,
            100m,
            1m,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            allowRedemption,
            minimumPoints,
            maximumPoints));
        var service = new LoyaltyRedemptionService(
            loyaltyRepository,
            new FakeRedemptionRepository(loyaltyRepository),
            orderRepository,
            settingsRepository);

        var preview = await service.PreviewAsync(10, 1003, requestedPoints, 1m, CancellationToken.None);

        Assert.Equal(expectedValid, preview.IsValid);
        Assert.Contains(expectedMessage, preview.ValidationMessage ?? "valid", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Redeem_UpdatesBalance_And_CreatesRedemption_AndCredit()
    {
        var loyaltyRepository = new FakeLoyaltyRepository();
        var redemptionRepository = new FakeRedemptionRepository(loyaltyRepository);
        var orderRepository = new FakeOrderRepository(new OrderDetailsDto(
            1001,
            "ORD-1001",
            10,
            "Ali",
            "0500000000",
            DateTime.UtcNow,
            null,
            1000m,
            0m,
            0m,
            1000m,
            "Normal",
            "Confirmed",
            "Test order",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null),
            []);

        await loyaltyRepository.EnsureAccountAsync(10, CancellationToken.None);
        var account = await loyaltyRepository.GetAccountByCustomerAsync(10, CancellationToken.None);
        await loyaltyRepository.CreateTransactionAsync(10, "Earn", 1000m, null, null, "TestSeed", "Seed", CancellationToken.None);

        var service = new LoyaltyRedemptionService(loyaltyRepository, redemptionRepository, orderRepository);
        var result = await service.ApplyAsync(10, 1001, 200m, 1m, CancellationToken.None);

        Assert.Equal(800m, result.NewBalanceAfterRedeem);
        Assert.Equal(200m, result.CreditAmount);
        Assert.Equal("Applied", result.Redemption.Status);
        Assert.Equal("Redeem", result.RedeemTransaction.TransactionType);
        Assert.Equal(1, redemptionRepository.GetByCustomerAsync(10, CancellationToken.None).Result.Count);
    }

    [Fact]
    public async Task Reverse_Redemption_RecreatesPoints_Without_Deleting_Original()
    {
        var loyaltyRepository = new FakeLoyaltyRepository();
        var redemptionRepository = new FakeRedemptionRepository(loyaltyRepository);
        var orderRepository = new FakeOrderRepository(new OrderDetailsDto(
            1002,
            "ORD-1002",
            11,
            "Sara",
            "0500000001",
            DateTime.UtcNow,
            null,
            500m,
            0m,
            0m,
            500m,
            "Normal",
            "Confirmed",
            "Test order",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null),
            []);

        await loyaltyRepository.CreateTransactionAsync(11, "Earn", 1000m, null, null, "Seed", "Seed", CancellationToken.None);
        var service = new LoyaltyRedemptionService(loyaltyRepository, redemptionRepository, orderRepository);
        var applied = await service.ApplyAsync(11, 1002, 200m, 1m, CancellationToken.None);
        var reversed = await service.ReverseAsync(applied.Redemption.LoyaltyRedemptionId, CancellationToken.None);

        Assert.Equal("Reversed", reversed.Status);
        Assert.True(reversed.ReversalLoyaltyTransactionId > 0);
        Assert.Equal(1000m, (await loyaltyRepository.GetAccountByCustomerAsync(11, CancellationToken.None))!.CurrentPoints);
    }

    private sealed class FakeOrderRepository(OrderDetailsDto order, IReadOnlyList<OrderItemDto> items) : IOrderRepository
    {
        public Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult(orderId == order.OrderId ? order : null);
        public Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderListDto>>([]);
        public Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult(items as IReadOnlyList<OrderItemDto>);
        public Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPieceDto>>([]);
        public Task<IReadOnlyList<OrderFabricDto>> GetFabricsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderFabricDto>>([]);
        public Task<IReadOnlyList<OrderPaymentDto>> GetPaymentsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPaymentDto>>([]);
        public Task<OrderDetailsDto?> CollectCustomerPaymentAsync(int orderId, decimal amount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDetailsDto?> SettleCustomerBalanceAsync(int orderId, decimal amount, decimal discountAmount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDeliveryDto?>(null);
        public Task<OrderDetailsDto?> DeliverAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDetailsDto?> WaiveRemainingBalanceAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDetailsDto?> RecognizeDeliveryRevenueAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrderDetailsDto?> CancelOrderAsync(int orderId, string? reason, string? cancelledBy, CancellationToken cancellationToken) => throw new NotSupportedException();
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

    private sealed class FakeLoyaltyRepository : ILoyaltyRepository
    {
        private readonly Dictionary<int, LoyaltyAccountDto> _accounts = new();
        private readonly List<LoyaltyTransactionDto> _transactions = new();

        public Task<LoyaltyDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyTransactionsScreenDto> GetTransactionsScreenAsync(string? search, string? transactionType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyRewardsScreenDto> GetRewardsScreenAsync(string? search, string? rewardType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<LoyaltyAccountDto?> GetAccountByCustomerAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult(_accounts.TryGetValue(customerId, out var acc) ? acc : null);

        public Task<LoyaltyAccountDto> EnsureAccountAsync(int customerId, CancellationToken cancellationToken)
        {
            if (!_accounts.TryGetValue(customerId, out var account))
            {
                account = new LoyaltyAccountDto(0, customerId, 0m, 0m, 0m, 0m, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
                _accounts[customerId] = account;
            }
            return Task.FromResult(account);
        }

        public Task<IReadOnlyList<LoyaltyAccountDto>> GetAllAccountsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LoyaltyAccountDto>>(_accounts.Values.ToList());

        public Task<LoyaltyAccountDto?> UpdateAccountLifecycleAsync(int customerId, string status, DateTime? warningStartedAtUtc, DateTime? frozenAtUtc, DateTime? reactivatedAtUtc, string? freezeReason, DateTime? lastQualifyingActivityAtUtc, CancellationToken cancellationToken)
        {
            if (!_accounts.TryGetValue(customerId, out var account)) return Task.FromResult<LoyaltyAccountDto?>(null);
            var updated = account with
            {
                LoyaltyAccountStatus = status,
                WarningStartedAtUtc = warningStartedAtUtc,
                FrozenAtUtc = frozenAtUtc,
                ReactivatedAtUtc = reactivatedAtUtc,
                FreezeReason = freezeReason,
                LastQualifyingActivityAtUtc = lastQualifyingActivityAtUtc,
                UpdatedAt = DateTime.UtcNow,
            };
            _accounts[customerId] = updated;
            return Task.FromResult<LoyaltyAccountDto?>(updated);
        }

        public Task<IReadOnlyList<LoyaltyTransactionDto>> GetTransactionsByCustomerAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LoyaltyTransactionDto>>(_transactions.Where(x => x.CustomerId == customerId).ToList());

        public Task<LoyaltyBalanceDto> GetBalanceAsync(int customerId, CancellationToken cancellationToken)
        {
            var account = _accounts.TryGetValue(customerId, out var current) ? current : new LoyaltyAccountDto(0, customerId, 0m, 0m, 0m, 0m, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
            return Task.FromResult(new LoyaltyBalanceDto(account.LoyaltyAccountId, customerId, account.CurrentPoints, account.LifetimeEarnedPoints, account.LifetimeRedeemedPoints, account.PendingExpirePoints, 0m, account.CurrentPoints, account.VipLevelId, account.UpdatedAt, account.LastActivityAt));
        }

        public Task<LoyaltyTransactionResultDto> CreateTransactionAsync(int customerId, string transactionType, decimal points, int? orderId, int? rewardId, string source, string? notes, CancellationToken cancellationToken, bool allowFrozenQualifyingPurchase = false)
        {
            var account = _accounts.TryGetValue(customerId, out var current) ? current : new LoyaltyAccountDto(0, customerId, 0m, 0m, 0m, 0m, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
            var balanceBefore = account.CurrentPoints;
            var balanceAfter = balanceBefore + points;
            var updated = account with { CurrentPoints = balanceAfter, UpdatedAt = DateTime.UtcNow, LastActivityAt = DateTime.UtcNow };
            _accounts[customerId] = updated;
            var transaction = new LoyaltyTransactionDto(_transactions.Count + 1, updated.LoyaltyAccountId, customerId, orderId, rewardId, transactionType, points, balanceBefore, balanceAfter, source, notes, DateTime.UtcNow);
            _transactions.Add(transaction);
            return Task.FromResult(new LoyaltyTransactionResultDto(transaction, updated, balanceBefore, balanceAfter));
        }
    }

    private sealed class FakeRedemptionRepository : ILoyaltyRedemptionRepository
    {
        private readonly FakeLoyaltyRepository _loyaltyRepository;
        private readonly List<LoyaltyRedemptionDto> _items = new();
        private int _nextId = 1;

        public FakeRedemptionRepository(FakeLoyaltyRepository loyaltyRepository) => _loyaltyRepository = loyaltyRepository;

        public Task<LoyaltyRedemptionHistoryDto> GetHistoryAsync(string? search, string? transactionType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<LoyaltyRedemptionDto?> GetByIdAsync(int redemptionId, CancellationToken cancellationToken)
            => Task.FromResult(_items.FirstOrDefault(item => item.LoyaltyRedemptionId == redemptionId));

        public Task<IReadOnlyList<LoyaltyRedemptionDto>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LoyaltyRedemptionDto>>(_items.Where(item => item.CustomerId == customerId).ToList());

        public Task<LoyaltyRedemptionResultDto> ApplyAtomicAsync(int customerId, int orderId, decimal pointsRedeemed, CancellationToken cancellationToken)
        {
            var transactionResult = _loyaltyRepository.CreateTransactionAsync(customerId, "Redeem", -pointsRedeemed, orderId, null, "LoyaltyRedemption", "test", cancellationToken).Result;
            var redemption = CreateAsync(customerId, orderId, transactionResult.Transaction.LoyaltyTransactionId, pointsRedeemed, 1m, pointsRedeemed, "test", cancellationToken).Result;
            return Task.FromResult(new LoyaltyRedemptionResultDto(redemption, transactionResult.Transaction, pointsRedeemed, transactionResult.Account.CurrentPoints));
        }

        public Task<LoyaltyRedemptionDto> CreateAsync(int customerId, int orderId, long loyaltyTransactionId, decimal pointsRedeemed, decimal pointMonetaryValue, decimal creditAmount, string referenceNumber, CancellationToken cancellationToken)
        {
            var item = new LoyaltyRedemptionDto(_nextId++, customerId, orderId, pointsRedeemed, pointMonetaryValue, creditAmount, "Applied", DateTime.UtcNow, null, null);
            _items.Add(item);
            return Task.FromResult(item);
        }

        public Task<LoyaltyCreditDto> CreateCreditAsync(int customerId, int orderId, int redemptionId, decimal creditAmount, decimal pointsRedeemed, string source, string? referenceNumber, CancellationToken cancellationToken)
            => Task.FromResult(new LoyaltyCreditDto(1, customerId, orderId, redemptionId, creditAmount, pointsRedeemed, source, referenceNumber, DateTime.UtcNow, false, null));

        public Task<LoyaltyRedemptionDto> ReverseAsync(int redemptionId, long reversalTransactionId, CancellationToken cancellationToken)
        {
            var current = _items.Single(item => item.LoyaltyRedemptionId == redemptionId) with { Status = "Reversed", ReversedAtUtc = DateTime.UtcNow, ReversalLoyaltyTransactionId = reversalTransactionId };
            _items[_items.FindIndex(item => item.LoyaltyRedemptionId == redemptionId)] = current;
            return Task.FromResult(current);
        }
    }
}
