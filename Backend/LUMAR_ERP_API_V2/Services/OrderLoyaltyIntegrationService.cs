using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.DTOs.Referral;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Utilities;

namespace LUMAR_ERP_API_V2.Services;

public sealed record OrderLoyaltyProcessingResult(
    int OrderId,
    int BuyerCustomerId,
    decimal BasePoints,
    decimal BuyerEarn,
    decimal BuyerDelta,
    IReadOnlyList<OrderReferralRewardResult> ReferralRewards,
    LoyaltyAccountDto BuyerAccount,
    IReadOnlyList<LoyaltyAccountDto> LoyaltyAccounts,
    IReadOnlyList<ReferralAccountSummaryDto> ReferralAccounts,
    IReadOnlyList<LoyaltyTransactionDto> BuyerTransactions,
    IReadOnlyList<ReferralTransactionDto> RewardTransactions);

public sealed record OrderReferralRewardResult(
    int Level,
    int ReferrerCustomerId,
    int ReferredCustomerId,
    decimal Percentage,
    decimal Points,
    decimal Delta,
    bool Repeated,
    string Fingerprint);

public sealed class OrderLoyaltyIntegrationService(
    IOrderRepository orderRepository,
    IPiecePointSettingsRepository piecePointSettingsRepository,
    IReferralRepository referralRepository,
    ILoyaltyRepository loyaltyRepository,
    ILoyaltyAccountLifecycleService? lifecycleService = null,
    IVipLevelEvaluationService? vipLevelEvaluationService = null) : IOrderLoyaltyIntegrationService
{
    private const string EngineVersion = "RL3-R1";

    public async Task<OrderLoyaltyProcessingResult?> ProcessIfEligibleAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null || string.Equals(order.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await ProcessOrderAsync(orderId, cancellationToken);
    }

    public async Task<OrderLoyaltyProcessingResult> ProcessOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {orderId} was not found.");

        var items = await orderRepository.GetItemsAsync(orderId, cancellationToken);
        if (items.Count == 0)
        {
            throw new InvalidOperationException($"Order {orderId} has no items to process.");
        }

        var orderItemsFingerprint = string.Join("|", items.OrderBy(x => x.OrderItemId).Select(x => $"{x.ProductTypeId}:{x.ImportedReadyMadeProductId}:{x.Quantity}"));
        var buyerCustomerId = order.CustomerId;
        var programEnabled = (await piecePointSettingsRepository.GetProgramSettingsAsync(cancellationToken))?.IsEnabled == true;
        var basePoints = 0m;
        foreach (var item in items)
        {
            var setting = item.ImportedReadyMadeProductId is int importedProductId
                ? await piecePointSettingsRepository.GetActiveImportedPointSettingAsync(importedProductId, cancellationToken)
                : item.ProductTypeId is int productTypeId
                    ? string.Equals(order.SaleCategory, "ReadyMadeSale", StringComparison.OrdinalIgnoreCase)
                        ? await piecePointSettingsRepository.GetActiveReadyMadeProductTypePointSettingAsync(productTypeId, cancellationToken)
                        : await piecePointSettingsRepository.GetActiveProductPointSettingAsync(productTypeId, cancellationToken)
                    : null;
            basePoints += PiecePointsEngine.CalculateForItem(setting, item.Quantity, 0m, programEnabled);
        }

        var buyerFingerprint = CreateBuyerFingerprint(orderId, buyerCustomerId, orderItemsFingerprint);
        var duplicateOrderNote = $"OrderId:{orderId}|Fingerprint:{buyerFingerprint}";
        var existingBuyerTransactions = await loyaltyRepository.GetTransactionsByCustomerAsync(buyerCustomerId, cancellationToken);
        var priorBuyerOrderTransaction = existingBuyerTransactions
            .Where(x => x.OrderId == orderId && x.Source == "PiecePurchase")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        var isDuplicateOrder = priorBuyerOrderTransaction is not null &&
            priorBuyerOrderTransaction.Notes != null &&
            priorBuyerOrderTransaction.Notes.Contains(duplicateOrderNote, StringComparison.OrdinalIgnoreCase);

        if (isDuplicateOrder)
        {
            var existingBuyerAccount = await loyaltyRepository.GetAccountByCustomerAsync(buyerCustomerId, cancellationToken) ?? await loyaltyRepository.EnsureAccountAsync(buyerCustomerId, cancellationToken);
            return new OrderLoyaltyProcessingResult(
                orderId,
                buyerCustomerId,
                basePoints,
                0m,
                0m,
                [],
                existingBuyerAccount,
                [existingBuyerAccount],
                [],
                [],
                []);
        }

        var buyerEarn = PiecePointsEngine.CalculateBuyerEarn(basePoints, "official-source", items.Sum(x => x.Quantity), 0m);
        var priorBuyerEarn = priorBuyerOrderTransaction?.Points ?? 0m;
        var buyerDelta = Math.Max(0m, buyerEarn - priorBuyerEarn);
        var buyerAccount = await loyaltyRepository.EnsureAccountAsync(buyerCustomerId, cancellationToken);
        var buyerWasFrozen = buyerAccount.LoyaltyAccountStatus.Equals("Frozen", StringComparison.OrdinalIgnoreCase);

        if (buyerDelta <= 0m)
        {
            return new OrderLoyaltyProcessingResult(
                orderId,
                buyerCustomerId,
                basePoints,
                0m,
                0m,
                [],
                buyerAccount,
                [buyerAccount],
                [],
                [],
                []);
        }

        if (lifecycleService is not null)
        {
            buyerAccount = await lifecycleService.PrepareQualifyingPurchaseAsync(buyerCustomerId, cancellationToken) ?? buyerAccount;
            if (buyerAccount.LoyaltyAccountStatus.Equals("Frozen", StringComparison.OrdinalIgnoreCase))
            {
                return new OrderLoyaltyProcessingResult(
                    orderId,
                    buyerCustomerId,
                    basePoints,
                    0m,
                    0m,
                    [],
                    buyerAccount,
                    [buyerAccount],
                    [],
                    [],
                    []);
            }
        }
        var buyerBefore = buyerAccount.CurrentPoints;

        var buyerTransactionResult = await loyaltyRepository.CreateTransactionAsync(
            buyerCustomerId,
            "Earn",
            buyerDelta,
            orderId,
            null,
            "PiecePurchase",
            $"OrderId:{orderId}|Fingerprint:{buyerFingerprint}",
            cancellationToken,
            allowFrozenQualifyingPurchase: buyerWasFrozen);

        var rewards = new List<OrderReferralRewardResult>();
        var rewardTransactions = new List<ReferralTransactionDto>();
        var loyaltyAdjustments = new List<LoyaltyTransactionDto> { buyerTransactionResult.Transaction };
        var loyaltyAccountUpdates = new List<LoyaltyAccountDto> { buyerTransactionResult.Account };
        var referralAccountUpdates = new List<ReferralAccountSummaryDto>();

        var referralChain = await ResolveReferralChainAsync(buyerCustomerId, cancellationToken);
        foreach (var levelEntry in referralChain)
        {
            var percentage = levelEntry.Level switch
            {
                1 => 0.50m,
                2 => 0.25m,
                3 => 0.125m,
                4 => 0.0625m,
                _ => 0m,
            };

            var points = basePoints * percentage;
            var fingerprint = ReferralRewardEngine.CreateFingerprint(
                levelEntry.ReferrerCustomerId,
                buyerCustomerId,
                orderId,
                levelEntry.Level,
                "PiecePurchase",
                EngineVersion,
                orderItemsFingerprint);

            var previousRewards = await referralRepository.GetTransactionsByCustomerAsync(levelEntry.ReferrerCustomerId, cancellationToken);
            var priorRewardForOrder = previousRewards
                .Where(x => x.TransactionType == "RewardGranted" && x.OrderId == orderId && x.ReferrerCustomerId == levelEntry.ReferrerCustomerId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();
            var isDuplicate = priorRewardForOrder is not null &&
                priorRewardForOrder.Notes != null &&
                priorRewardForOrder.Notes.Contains(fingerprint, StringComparison.OrdinalIgnoreCase);

            if (isDuplicate)
            {
                rewards.Add(new OrderReferralRewardResult(levelEntry.Level, levelEntry.ReferrerCustomerId, buyerCustomerId, percentage, 0m, 0m, true, fingerprint));
                continue;
            }

            var deltaPoints = priorRewardForOrder is null ? points : Math.Max(0m, points - priorRewardForOrder.LoyaltyPoints);
            if (deltaPoints <= 0m)
            {
                rewards.Add(new OrderReferralRewardResult(levelEntry.Level, levelEntry.ReferrerCustomerId, buyerCustomerId, percentage, 0m, 0m, true, fingerprint));
                continue;
            }

            if (lifecycleService is not null)
            {
                var referrerLifecycle = await lifecycleService.EvaluateAsync(levelEntry.ReferrerCustomerId, cancellationToken);
                if (referrerLifecycle?.LoyaltyAccountStatus.Equals("Frozen", StringComparison.OrdinalIgnoreCase) == true)
                {
                    rewards.Add(new OrderReferralRewardResult(levelEntry.Level, levelEntry.ReferrerCustomerId, buyerCustomerId, percentage, points, 0m, false, fingerprint));
                    continue;
                }
            }

            var referrerAccount = await referralRepository.GetAccountByCustomerAsync(levelEntry.ReferrerCustomerId, cancellationToken) ?? new ReferralAccountSummaryDto
            {
                CustomerId = levelEntry.ReferrerCustomerId,
                TotalReferrals = 0,
                SuccessfulReferrals = 0,
                TotalRewardsAmount = 0m,
                TotalRewardPoints = 0m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            var rewardRecord = await referralRepository.CreateRewardGrantedAsync(
                levelEntry.ReferrerCustomerId,
                buyerCustomerId,
                referrerAccount.ReferralCodeId,
                orderId,
                0m,
                deltaPoints,
                $"Level:{levelEntry.Level}|OrderId:{orderId}|Fingerprint:{fingerprint}|OrderItems:{orderItemsFingerprint}|EngineVersion:{EngineVersion}",
                cancellationToken);
            rewardTransactions.Add(rewardRecord);

            var updatedReferralAccount = new ReferralAccountSummaryDto
            {
                CustomerId = referrerAccount.CustomerId,
                ReferralCodeId = referrerAccount.ReferralCodeId,
                ReferralCode = referrerAccount.ReferralCode,
                ReferralCodeIsActive = referrerAccount.ReferralCodeIsActive,
                TotalReferrals = referrerAccount.TotalReferrals,
                SuccessfulReferrals = referrerAccount.SuccessfulReferrals,
                TotalRewardsAmount = referrerAccount.TotalRewardsAmount,
                TotalRewardPoints = referrerAccount.TotalRewardPoints + deltaPoints,
                CreatedAt = referrerAccount.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
            };
            referralAccountUpdates.Add(await referralRepository.UpsertAccountAsync(updatedReferralAccount, cancellationToken));

            var adjustResult = await loyaltyRepository.CreateTransactionAsync(
                levelEntry.ReferrerCustomerId,
                "Adjust",
                deltaPoints,
                orderId,
                null,
                "ReferralReward",
                $"RewardGranted:Level:{levelEntry.Level}|OrderId:{orderId}|Fingerprint:{fingerprint}",
                cancellationToken);
            loyaltyAdjustments.Add(adjustResult.Transaction);
            loyaltyAccountUpdates.Add(adjustResult.Account);

            rewards.Add(new OrderReferralRewardResult(levelEntry.Level, levelEntry.ReferrerCustomerId, buyerCustomerId, percentage, points, deltaPoints, false, fingerprint));
        }

        var currentBuyerAccount = loyaltyAccountUpdates
            .Where(x => x.CustomerId == buyerCustomerId)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefault() ?? buyerAccount;

        if (lifecycleService is not null)
        {
            var markedBuyer = await lifecycleService.MarkQualifyingPurchaseAsync(buyerCustomerId, cancellationToken);
            if (markedBuyer is not null)
            {
                currentBuyerAccount = markedBuyer;
                loyaltyAccountUpdates.Add(markedBuyer);
            }
        }

        if (vipLevelEvaluationService is not null)
        {
            await vipLevelEvaluationService.EvaluateNetworkAsync(buyerCustomerId, cancellationToken);
        }

        return new OrderLoyaltyProcessingResult(
            orderId,
            buyerCustomerId,
            basePoints,
            buyerEarn,
            currentBuyerAccount.CurrentPoints - buyerBefore,
            rewards,
            currentBuyerAccount,
            loyaltyAccountUpdates.DistinctBy(x => x.CustomerId).ToList(),
            referralAccountUpdates.DistinctBy(x => x.CustomerId).ToList(),
            loyaltyAdjustments,
            rewardTransactions);
    }

    private static string CreateBuyerFingerprint(int orderId, int buyerCustomerId, string orderItemsFingerprint) =>
        $"{orderId}|{buyerCustomerId}|{orderItemsFingerprint}|{EngineVersion}";

    private async Task<IReadOnlyList<(int Level, int ReferrerCustomerId, int ReferredCustomerId)>> ResolveReferralChainAsync(int buyerCustomerId, CancellationToken cancellationToken)
    {
        var chain = new List<(int Level, int ReferrerCustomerId, int ReferredCustomerId)>();
        var currentCustomerId = buyerCustomerId;
        var level = 1;

        while (level <= 4)
        {
            var registrations = await referralRepository.GetTransactionsByCustomerAsync(currentCustomerId, cancellationToken);
            var referrerCustomerId = registrations
                .Where(x => x.TransactionType == "Registration" && x.ReferredCustomerId == currentCustomerId)
                .Select(x => x.ReferrerCustomerId)
                .FirstOrDefault();

            if (referrerCustomerId == 0)
            {
                break;
            }

            chain.Add((level, referrerCustomerId, currentCustomerId));
            currentCustomerId = referrerCustomerId;
            level++;
        }

        return chain;
    }
}
