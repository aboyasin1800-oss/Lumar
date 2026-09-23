using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.DTOs.Referral;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class OrderLoyaltyIntegrationServiceTests
{
    [Fact]
    public async Task ProcessOrderAsync_CreatesBuyerEarn_AndReferralRewards()
    {
        var order = new OrderDetailsDto(
            101,
            "ORD-101",
            5,
            "E",
            "0500000000",
            DateTime.UtcNow,
            null,
            0m,
            0m,
            0m,
            0m,
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
            null);

        var orderItems = new List<OrderItemDto>
        {
            new(1, 101, "YALAQ", 1, null, null, null, null, null, null, null, null, null, "T-1", "New", DateTime.UtcNow, 10),
        };

        var orderRepository = new FakeOrderRepository(order, orderItems);
        var loyaltyRepository = new FakeLoyaltyRepository();
        var referralRepository = new FakeReferralRepository();
        referralRepository.RegisterRegistration(4, 5, DateTime.UtcNow.AddDays(-5));
        referralRepository.RegisterRegistration(3, 4, DateTime.UtcNow.AddDays(-4));
        referralRepository.RegisterRegistration(2, 3, DateTime.UtcNow.AddDays(-3));
        referralRepository.RegisterRegistration(1, 2, DateTime.UtcNow.AddDays(-2));

        var pieceSettingsRepository = new FakePiecePointSettingsRepository();
        var service = new OrderLoyaltyIntegrationService(
            orderRepository,
            pieceSettingsRepository,
            referralRepository,
            loyaltyRepository);

        var result = await service.ProcessOrderAsync(101, CancellationToken.None);

        Assert.Equal(100m, result.BuyerEarn);
        Assert.Equal(100m, result.BuyerAccount.CurrentPoints);
        Assert.Collection(
            result.ReferralRewards.OrderBy(x => x.Level),
            reward =>
            {
                Assert.Equal(1, reward.Level);
                Assert.Equal(50m, reward.Points);
            },
            reward =>
            {
                Assert.Equal(2, reward.Level);
                Assert.Equal(25m, reward.Points);
            },
            reward =>
            {
                Assert.Equal(3, reward.Level);
                Assert.Equal(12.5m, reward.Points);
            },
            reward =>
            {
                Assert.Equal(4, reward.Level);
                Assert.Equal(6.25m, reward.Points);
            });

        Assert.Equal(100m, loyaltyRepository.Accounts[5].CurrentPoints);
        Assert.Equal(50m, loyaltyRepository.Accounts[4].CurrentPoints);
        Assert.Equal(25m, loyaltyRepository.Accounts[3].CurrentPoints);
        Assert.Equal(12.5m, loyaltyRepository.Accounts[2].CurrentPoints);
        Assert.Equal(6.25m, loyaltyRepository.Accounts[1].CurrentPoints);

        Assert.Equal(4, referralRepository.Transactions.Count(t => t.TransactionType == "RewardGranted" && t.OrderId == 101));
        Assert.Equal(4, referralRepository.Transactions.Count(t => t.TransactionType == "RewardGranted"));
    }

    [Fact]
    public async Task ProcessReadyMadeOrderAsync_UsesIndependentReadyMadeProductTypeSetting()
    {
        var order = new OrderDetailsDto(
            102,
            "ORD-102",
            5,
            "E",
            "0500000000",
            DateTime.UtcNow,
            null,
            0m,
            0m,
            0m,
            0m,
            "Normal",
            "Confirmed",
            "Ready-made test order",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "ReadyMadeSale",
            false,
            null,
            false,
            null);
        var orderItems = new List<OrderItemDto>
        {
            new(2, 102, "KOT", 1, null, null, null, null, null, null, null, null, null, "T-2", "New", DateTime.UtcNow, 10),
        };
        var loyaltyRepository = new FakeLoyaltyRepository();
        var service = new OrderLoyaltyIntegrationService(
            new FakeOrderRepository(order, orderItems),
            new FakePiecePointSettingsRepository(),
            new FakeReferralRepository(),
            loyaltyRepository);

        var result = await service.ProcessOrderAsync(102, CancellationToken.None);

        Assert.Equal(25m, result.BasePoints);
        Assert.Equal(25m, result.BuyerEarn);
        Assert.Equal(25m, loyaltyRepository.Accounts[5].CurrentPoints);
    }

    [Fact]
    public async Task ProcessIfEligibleAsync_ProcessesNewOrder_AndRemainsIdempotent()
    {
        var order = new OrderDetailsDto(
            111,
            "ORD-111",
            5,
            "E",
            "0500000000",
            DateTime.UtcNow,
            null,
            0m,
            0m,
            0m,
            0m,
            "Normal",
            "New",
            "Created order",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var orderRepository = new FakeOrderRepository(order, new[]
        {
            new OrderItemDto(1, 111, "YALAQ", 1, null, null, null, null, null, null, null, null, null, "T-111", "New", DateTime.UtcNow, 10),
        });
        var loyaltyRepository = new FakeLoyaltyRepository();
        var referralRepository = new FakeReferralRepository();
        referralRepository.RegisterRegistration(4, 5, DateTime.UtcNow.AddDays(-5));
        referralRepository.RegisterRegistration(3, 4, DateTime.UtcNow.AddDays(-4));
        referralRepository.RegisterRegistration(2, 3, DateTime.UtcNow.AddDays(-3));
        referralRepository.RegisterRegistration(1, 2, DateTime.UtcNow.AddDays(-2));

        var service = new OrderLoyaltyIntegrationService(
            orderRepository,
            new FakePiecePointSettingsRepository(),
            referralRepository,
            loyaltyRepository);

        var first = await service.ProcessIfEligibleAsync(111, CancellationToken.None);
        orderRepository.Order = orderRepository.Order with { OrderStatus = "ReadyForDelivery" };
        var second = await service.ProcessIfEligibleAsync(111, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal(100m, first!.BuyerEarn);
        Assert.NotNull(second);
        Assert.Equal(0m, second!.BuyerDelta);
        Assert.Equal(1, loyaltyRepository.Transactions.Count(x => x.OrderId == 111 && x.TransactionType == "Earn"));
        Assert.Equal(4, referralRepository.Transactions.Count(x => x.OrderId == 111 && x.TransactionType == "RewardGranted"));
        Assert.Equal(4, loyaltyRepository.Transactions.Count(x => x.OrderId == 111 && x.TransactionType == "Adjust"));
    }

    [Fact]
    public void ReferralTreeBuilder_BuildsHierarchyFromRegistrationTransactionsOnly()
    {
        var customers = new Dictionary<int, CustomerIdentity>
        {
            [1] = new CustomerIdentity(1, "C-001", "أحمد", true, null),
            [2] = new CustomerIdentity(2, "C-002", "محمد", true, null),
            [3] = new CustomerIdentity(3, "C-003", "علي", true, null),
            [4] = new CustomerIdentity(4, "C-004", "خالد", true, null),
            [5] = new CustomerIdentity(5, "C-005", "سالم", true, null),
        };

        var registrations = new[]
        {
            new ReferralRegistrationEdge(1, 2),
            new ReferralRegistrationEdge(1, 3),
            new ReferralRegistrationEdge(2, 4),
            new ReferralRegistrationEdge(2, 5),
        };

        var tree = ReferralTreeBuilder.Build(1, registrations, customers);

        Assert.Equal(1, tree.RootCustomerId);
        Assert.Equal(2, tree.Children.Count);
        Assert.Equal(2, tree.Children.Single(x => x.CustomerId == 2).DirectChildrenCount);
        Assert.Equal(4, tree.TotalDescendantsCount);
        Assert.Equal(2, tree.MaxDepth);
    }

    [Fact]
    public void ReferralRootDiscovery_FindsActualRegistrationRoots()
    {
        var registrations = new[]
        {
            new ReferralRegistrationEdge(16, 17),
            new ReferralRegistrationEdge(50, 51),
            new ReferralRegistrationEdge(51, 52),
            new ReferralRegistrationEdge(52, 53),
            new ReferralRegistrationEdge(53, 54),
        };

        var roots = ReferralTreeBuilder.DiscoverRootCustomerIds(registrations);

        Assert.Equal(new[] { 16, 50 }, roots);
    }

    [Fact]
    public async Task ProcessOrderAsync_RepeatedCall_IsIdempotent_And_ReconcilesDelta()
    {
        var order = new OrderDetailsDto(
            202,
            "ORD-202",
            5,
            "E",
            "0500000001",
            DateTime.UtcNow,
            null,
            0m,
            0m,
            0m,
            0m,
            "Normal",
            "Confirmed",
            "Delta order",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            "TailoringOrder",
            false,
            null,
            false,
            null);

        var orderRepository = new FakeOrderRepository(order, new[]
        {
            new OrderItemDto(1, 202, "YALAQ", 1, null, null, null, null, null, null, null, null, null, "T-202", "New", DateTime.UtcNow, 10),
        });

        var loyaltyRepository = new FakeLoyaltyRepository();
        var referralRepository = new FakeReferralRepository();
        referralRepository.RegisterRegistration(4, 5, DateTime.UtcNow.AddDays(-5));
        referralRepository.RegisterRegistration(3, 4, DateTime.UtcNow.AddDays(-4));
        referralRepository.RegisterRegistration(2, 3, DateTime.UtcNow.AddDays(-3));
        referralRepository.RegisterRegistration(1, 2, DateTime.UtcNow.AddDays(-2));

        var service = new OrderLoyaltyIntegrationService(
            orderRepository,
            new FakePiecePointSettingsRepository(),
            referralRepository,
            loyaltyRepository);

        var first = await service.ProcessOrderAsync(202, CancellationToken.None);
        Assert.Equal(100m, first.BuyerEarn);

        var second = await service.ProcessOrderAsync(202, CancellationToken.None);
        Assert.Equal(0m, second.BuyerDelta);
        Assert.DoesNotContain(second.ReferralRewards, x => x.Points > 0m && x.Repeated);

        orderRepository.Items = new[]
        {
            new OrderItemDto(1, 202, "YALAQ", 2, null, null, null, null, null, null, null, null, null, "T-202", "New", DateTime.UtcNow, 10),
        };

        var reconciled = await service.ProcessOrderAsync(202, CancellationToken.None);
        Assert.Equal(100m, reconciled.BuyerDelta);
        Assert.Equal(50m, reconciled.ReferralRewards.Single(x => x.Level == 1).Delta);
        Assert.Equal(25m, reconciled.ReferralRewards.Single(x => x.Level == 2).Delta);
    }

    private sealed class FakeOrderRepository(OrderDetailsDto order, IReadOnlyList<OrderItemDto> items) : IOrderRepository
    {
        public OrderDetailsDto Order { get; set; } = order;
        public IReadOnlyList<OrderItemDto> Items { get; set; } = items;

        public Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderListDto>>([]);

        public Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDetailsDto?>(orderId == Order.OrderId ? Order : null);

        public Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult(Items.Where(x => x.OrderId == orderId).ToList() as IReadOnlyList<OrderItemDto>);

        public Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPieceDto>>([]);

        public Task<IReadOnlyList<OrderFabricDto>> GetFabricsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderFabricDto>>([]);

        public Task<IReadOnlyList<OrderPaymentDto>> GetPaymentsAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderPaymentDto>>([]);

        public Task<OrderDetailsDto?> CollectCustomerPaymentAsync(int orderId, decimal amount, string? paymentMethod, string? referenceNumber, string? notes, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderDetailsDto?> SettleCustomerBalanceAsync(int orderId, decimal amount, decimal discountAmount, string? paymentMethod, string? referenceNumber, string? notes, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken) => Task.FromResult<OrderDeliveryDto?>(null);

        public Task<OrderDetailsDto?> DeliverAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderDetailsDto?> WaiveRemainingBalanceAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderDetailsDto?> RecognizeDeliveryRevenueAsync(int orderId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderDetailsDto?> CancelOrderAsync(int orderId, string? reason, string? cancelledBy, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakePiecePointSettingsRepository : IPiecePointSettingsRepository
    {
        public Task<IReadOnlyList<ProductLoyaltyPointSettingDto>> GetProductLoyaltyPointSettingsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductLoyaltyPointSettingDto>>([]);
        public Task<ProductLoyaltyPointSettingDto?> UpsertProductLoyaltyPointSettingAsync(int productTypeId, UpdateProductLoyaltyPointSettingDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReadyMadeProductTypeLoyaltyPointSettingDto>> GetReadyMadeProductTypeLoyaltyPointSettingsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReadyMadeProductTypeLoyaltyPointSettingDto>>([]);
        public Task<ReadyMadeProductTypeLoyaltyPointSettingDto?> UpsertReadyMadeProductTypeLoyaltyPointSettingAsync(int productTypeId, UpdateReadyMadeProductTypeLoyaltyPointSettingDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ImportedProductLoyaltyPointSettingDto>> GetImportedProductLoyaltyPointSettingsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ImportedProductLoyaltyPointSettingDto>>([]);
        public Task<ImportedProductLoyaltyPointSettingDto?> UpsertImportedProductLoyaltyPointSettingAsync(int importedReadyMadeProductId, UpdateImportedProductLoyaltyPointSettingDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyPiecePointSettingDto?> GetActiveProductPointSettingAsync(int productTypeId, CancellationToken cancellationToken)
            => Task.FromResult<LoyaltyPiecePointSettingDto?>(productTypeId == 10 ? new LoyaltyPiecePointSettingDto(1, "YALAQ", "YALAQ", 100m, true, DateTime.UtcNow, DateTime.UtcNow, 10) : null);
        public Task<LoyaltyPiecePointSettingDto?> GetActiveReadyMadeProductTypePointSettingAsync(int productTypeId, CancellationToken cancellationToken)
            => Task.FromResult<LoyaltyPiecePointSettingDto?>(productTypeId == 10 ? new LoyaltyPiecePointSettingDto(2, "KOT", "كوت", 25m, true, DateTime.UtcNow, DateTime.UtcNow, 10) : null);
        public Task<LoyaltyPiecePointSettingDto?> GetActiveImportedPointSettingAsync(int importedReadyMadeProductId, CancellationToken cancellationToken) => Task.FromResult<LoyaltyPiecePointSettingDto?>(null);

        public Task<LoyaltyPiecePointSettingDto?> GetActivePieceSettingAsync(string pieceCode, CancellationToken cancellationToken)
        {
            if (string.Equals(pieceCode, "YALAQ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<LoyaltyPiecePointSettingDto?>(new LoyaltyPiecePointSettingDto(1, "YALAQ", "YALAQ", 100m, true, DateTime.UtcNow, DateTime.UtcNow));
            }

            return Task.FromResult<LoyaltyPiecePointSettingDto?>(null);
        }

        public Task<LoyaltyProgramSettingsDto?> GetProgramSettingsAsync(CancellationToken cancellationToken) => Task.FromResult<LoyaltyProgramSettingsDto?>(new LoyaltyProgramSettingsDto(1, true, 100m, 1m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow));

        public Task<PiecePointsResultDto> EvaluatePiecePointsAsync(int customerId, int orderId, string pieceCode, decimal quantity, string source, string? notes, CancellationToken cancellationToken)
        {
            var setting = GetActivePieceSettingAsync(pieceCode, cancellationToken).Result;
            var fallback = GetProgramSettingsAsync(cancellationToken).Result?.PointsPerPiece ?? 0m;
            var basePoints = PiecePointsEngine.CalculateForItem(setting, quantity, fallback);
            var rewards = ReferralRewardEngine.CalculateLevels(basePoints, 1, 2, 3, 4);
            return Task.FromResult(new PiecePointsResultDto(basePoints, basePoints, rewards, "fp", "recon"));
        }
    }

    private sealed class FakeLoyaltyRepository : ILoyaltyRepository
    {
        public Dictionary<int, LoyaltyAccountDto> Accounts { get; } = new();
        public List<LoyaltyTransactionDto> Transactions { get; } = new();

        public Task<LoyaltyDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyTransactionsScreenDto> GetTransactionsScreenAsync(string? search, string? transactionType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LoyaltyRewardsScreenDto> GetRewardsScreenAsync(string? search, string? rewardType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<LoyaltyAccountDto?> GetAccountByCustomerAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult(Accounts.TryGetValue(customerId, out var account) ? account : null);

        public Task<LoyaltyAccountDto> EnsureAccountAsync(int customerId, CancellationToken cancellationToken)
        {
            if (!Accounts.TryGetValue(customerId, out var account))
            {
                account = new LoyaltyAccountDto(0, customerId, 0m, 0m, 0m, 0m, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
                Accounts[customerId] = account;
            }

            return Task.FromResult(account);
        }

        public Task<IReadOnlyList<LoyaltyAccountDto>> GetAllAccountsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LoyaltyAccountDto>>(Accounts.Values.ToList());

        public Task<LoyaltyAccountDto?> UpdateAccountLifecycleAsync(int customerId, string status, DateTime? warningStartedAtUtc, DateTime? frozenAtUtc, DateTime? reactivatedAtUtc, string? freezeReason, DateTime? lastQualifyingActivityAtUtc, CancellationToken cancellationToken)
        {
            if (!Accounts.TryGetValue(customerId, out var account)) return Task.FromResult<LoyaltyAccountDto?>(null);
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
            Accounts[customerId] = updated;
            return Task.FromResult<LoyaltyAccountDto?>(updated);
        }

        public Task<IReadOnlyList<LoyaltyTransactionDto>> GetTransactionsByCustomerAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LoyaltyTransactionDto>>(Transactions.Where(x => x.CustomerId == customerId).ToList());

        public Task<LoyaltyBalanceDto> GetBalanceAsync(int customerId, CancellationToken cancellationToken)
        {
            var account = Accounts.TryGetValue(customerId, out var current) ? current : new LoyaltyAccountDto(0, customerId, 0m, 0m, 0m, 0m, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
            return Task.FromResult(new LoyaltyBalanceDto(account.LoyaltyAccountId, customerId, account.CurrentPoints, account.LifetimeEarnedPoints, account.LifetimeRedeemedPoints, account.PendingExpirePoints, 0m, account.CurrentPoints, account.VipLevelId, account.UpdatedAt, account.LastActivityAt));
        }

        public Task<LoyaltyTransactionResultDto> CreateTransactionAsync(int customerId, string transactionType, decimal points, int? orderId, int? rewardId, string source, string? notes, CancellationToken cancellationToken, bool allowFrozenQualifyingPurchase = false)
        {
            var account = EnsureAccountAsync(customerId, cancellationToken).Result;
            var result = LoyaltyAccountResolver.ApplyTransaction(account, transactionType, points, orderId, rewardId, source, notes);
            var updated = result.Account with { LoyaltyAccountId = account.LoyaltyAccountId };
            Accounts[customerId] = updated;
            var transaction = result.Transaction with { LoyaltyTransactionId = Transactions.Count + 1, LoyaltyAccountId = account.LoyaltyAccountId };
            Transactions.Add(transaction);
            return Task.FromResult(new LoyaltyTransactionResultDto(transaction, updated, result.BalanceBefore, result.BalanceAfter));
        }
    }

    private sealed class FakeReferralRepository : IReferralRepository
    {
        public List<ReferralTransactionDto> Transactions { get; } = new();
        public Dictionary<int, ReferralAccountSummaryDto> Accounts { get; } = new();

        public Task<ReferralDashboardDto> GetDashboardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReferralDashboardSearchResultDto>> SearchDashboardAsync(string query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReferralAnalyticsDto> GetAnalyticsAsync(string? search, DateTime? from, DateTime? to, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReferralRewardsScreenDto> GetRewardsScreenAsync(string? transactionType, DateTime? from, DateTime? to, int? customerId, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ReferralCodeDto>> GetCodesByCustomerAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ReferralCodeDto>>([]);

        public Task<ReferralAccountSummaryDto?> GetAccountByCustomerAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult(Accounts.TryGetValue(customerId, out var account) ? account : null);

        public Task<IReadOnlyList<ReferralTransactionDto>> GetTransactionsByCustomerAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ReferralTransactionDto>>(Transactions.Where(x => x.ReferrerCustomerId == customerId || x.ReferredCustomerId == customerId).ToList());

        public Task<ReferralAccountSummaryDto> UpsertAccountAsync(ReferralAccountSummaryDto account, CancellationToken cancellationToken)
        {
            Accounts[account.CustomerId] = account;
            return Task.FromResult(account);
        }

        public Task<ReferralCodeDto?> EnsureCodeAsync(int customerId, string? preferredCode, CancellationToken cancellationToken) => Task.FromResult<ReferralCodeDto?>(null);

        public Task<RegisterReferralResponse> RegisterAsync(RegisterReferralRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ReferralRootDto>> GetRootsAsync(CancellationToken cancellationToken)
        {
            var roots = Transactions
                .Where(t => t.TransactionType == "Registration")
                .Select(t => t.ReferrerCustomerId)
                .Except(Transactions
                    .Where(t => t.TransactionType == "Registration")
                    .Select(t => t.ReferredCustomerId)
                    .OfType<int>())
                .Distinct()
                .OrderBy(id => id)
                .Select(id => new ReferralRootDto(id, $"C-{id}", $"Customer {id}", 0, 0, 0))
                .ToList();

            return Task.FromResult<IReadOnlyList<ReferralRootDto>>(roots);
        }

        public Task<ReferralTreeDto?> GetTreeAsync(int customerId, CancellationToken cancellationToken) => Task.FromResult<ReferralTreeDto?>(null);

        public void RegisterRegistration(int referrerCustomerId, int referredCustomerId, DateTime createdAt)
        {
            Transactions.Add(new ReferralTransactionDto(
                Transactions.Count + 1,
                referrerCustomerId,
                referredCustomerId,
                null,
                null,
                null,
                "Registration",
                0m,
                0m,
                $"Registration:{referrerCustomerId}->{referredCustomerId}",
                createdAt));
        }

        public Task<ReferralAccountSummaryDto> EnsureAccountAsync(int customerId, CancellationToken cancellationToken)
        {
            if (!Accounts.TryGetValue(customerId, out var account))
            {
                account = new ReferralAccountSummaryDto
                {
                    CustomerId = customerId,
                    TotalReferrals = 0,
                    SuccessfulReferrals = 0,
                    TotalRewardsAmount = 0m,
                    TotalRewardPoints = 0m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                Accounts[customerId] = account;
            }

            return Task.FromResult(account);
        }

        public Task<ReferralTransactionDto> CreateRewardGrantedAsync(int referrerCustomerId, int referredCustomerId, int? referralCodeId, int orderId, decimal fixedRewardAmount, decimal loyaltyPoints, string notes, CancellationToken cancellationToken)
        {
            var record = new ReferralTransactionDto(
                Transactions.Count + 1,
                referrerCustomerId,
                referredCustomerId,
            referralCodeId,
                orderId,
                null,
                "RewardGranted",
                fixedRewardAmount,
                loyaltyPoints,
                notes,
                DateTime.UtcNow);
            Transactions.Add(record);

            var account = Accounts.TryGetValue(referrerCustomerId, out var current) ? current : new ReferralAccountSummaryDto
            {
                CustomerId = referrerCustomerId,
                TotalReferrals = 0,
                SuccessfulReferrals = 0,
                TotalRewardsAmount = 0m,
                TotalRewardPoints = 0m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            Accounts[referrerCustomerId] = new ReferralAccountSummaryDto
            {
                CustomerId = account.CustomerId,
                ReferralCodeId = account.ReferralCodeId,
                ReferralCode = account.ReferralCode,
                ReferralCodeIsActive = account.ReferralCodeIsActive,
                TotalReferrals = account.TotalReferrals,
                SuccessfulReferrals = account.SuccessfulReferrals,
                TotalRewardsAmount = account.TotalRewardsAmount + fixedRewardAmount,
                TotalRewardPoints = account.TotalRewardPoints + loyaltyPoints,
                CreatedAt = account.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
            };

            return Task.FromResult(record);
        }
    }
}
