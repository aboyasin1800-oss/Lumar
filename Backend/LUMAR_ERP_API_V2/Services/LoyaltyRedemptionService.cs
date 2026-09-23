using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Utilities;

namespace LUMAR_ERP_API_V2.Services;

public sealed class LoyaltyRedemptionService(
    ILoyaltyRepository loyaltyRepository,
    ILoyaltyRedemptionRepository redemptionRepository,
    IOrderRepository orderRepository,
    ILoyaltyManagementSettingsRepository? settingsRepository = null,
    ILoyaltyAccountLifecycleService? lifecycleService = null) : ILoyaltyRedemptionService
{
    public Task<LoyaltyRedemptionHistoryDto> GetHistoryAsync(string? search, string? transactionType, DateTime? from, DateTime? to, int? customerId, CancellationToken cancellationToken)
        => redemptionRepository.GetHistoryAsync(search, transactionType, from, to, customerId, cancellationToken);

    public Task<IReadOnlyList<LoyaltyRedemptionDto>> GetHistoryByCustomerAsync(int customerId, CancellationToken cancellationToken)
        => redemptionRepository.GetByCustomerAsync(customerId, cancellationToken);

    public async Task<LoyaltyRedemptionPreviewDto> PreviewAsync(int customerId, int orderId, decimal pointsRedeemed, decimal pointMonetaryValue, CancellationToken cancellationToken)
    {
        if (settingsRepository is not null)
        {
            var program = (await settingsRepository.GetProgramSettingsAsync(cancellationToken)).FirstOrDefault();
            if (program?.IsEnabled != true)
                return new LoyaltyRedemptionPreviewDto(customerId, orderId, 0m, 0m, pointsRedeemed, pointMonetaryValue, 0m, false, "Loyalty program is disabled.");
            if (!program.AllowRedemption)
                return new LoyaltyRedemptionPreviewDto(customerId, orderId, 0m, 0m, pointsRedeemed, pointMonetaryValue, 0m, false, "Loyalty redemption is disabled.");
            if (pointsRedeemed < program.MinimumRedemptionPoints)
                return new LoyaltyRedemptionPreviewDto(customerId, orderId, 0m, 0m, pointsRedeemed, pointMonetaryValue, 0m, false, "PointsRedeemed is below the configured minimum.");
            if (program.MaximumRedemptionPoints > 0m && pointsRedeemed > program.MaximumRedemptionPoints)
                return new LoyaltyRedemptionPreviewDto(customerId, orderId, 0m, 0m, pointsRedeemed, pointMonetaryValue, 0m, false, "PointsRedeemed exceeds the configured maximum.");
        }

        var account = lifecycleService is not null
            ? await lifecycleService.EvaluateAsync(customerId, cancellationToken)
            : await loyaltyRepository.GetAccountByCustomerAsync(customerId, cancellationToken);
        if (account is null) throw new InvalidOperationException("Loyalty account not found.");
        var order = await orderRepository.GetByIdAsync(orderId, cancellationToken) ?? throw new InvalidOperationException("Order not found.");

        if (account.LoyaltyAccountStatus.Equals("Frozen", StringComparison.OrdinalIgnoreCase))
            return new LoyaltyRedemptionPreviewDto(customerId, orderId, account.CurrentPoints, order.RemainingAmount, pointsRedeemed, pointMonetaryValue, 0m, false, "Loyalty account is frozen.");

        if (pointsRedeemed <= 0m)
            return new LoyaltyRedemptionPreviewDto(customerId, orderId, account.CurrentPoints, order.RemainingAmount, pointsRedeemed, pointMonetaryValue, 0m, false, "PointsRedeemed must be greater than zero.");
        if (pointsRedeemed > account.CurrentPoints)
            return new LoyaltyRedemptionPreviewDto(customerId, orderId, account.CurrentPoints, order.RemainingAmount, pointsRedeemed, pointMonetaryValue, 0m, false, "PointsRedeemed exceeds current points balance.");
        if (order.CustomerId != customerId)
            return new LoyaltyRedemptionPreviewDto(customerId, orderId, account.CurrentPoints, order.RemainingAmount, pointsRedeemed, pointMonetaryValue, 0m, false, "Order does not belong to customer.");

        var creditAmount = pointsRedeemed * pointMonetaryValue;
        if (creditAmount > order.RemainingAmount)
            return new LoyaltyRedemptionPreviewDto(customerId, orderId, account.CurrentPoints, order.RemainingAmount, pointsRedeemed, pointMonetaryValue, creditAmount, false, "CreditAmount exceeds remaining order amount.");

        return new LoyaltyRedemptionPreviewDto(customerId, orderId, account.CurrentPoints, order.RemainingAmount, pointsRedeemed, pointMonetaryValue, creditAmount, true, null);
    }

    public async Task<LoyaltyRedemptionResultDto> ApplyAsync(int customerId, int orderId, decimal pointsRedeemed, decimal pointMonetaryValue, CancellationToken cancellationToken)
    {
        if (lifecycleService is not null) await lifecycleService.EvaluateAsync(customerId, cancellationToken);
        return await redemptionRepository.ApplyAtomicAsync(customerId, orderId, pointsRedeemed, cancellationToken);
    }

    public async Task<LoyaltyRedemptionDto> ReverseAsync(int redemptionId, CancellationToken cancellationToken)
    {
        var redemption = await redemptionRepository.GetByIdAsync(redemptionId, cancellationToken)
            ?? throw new InvalidOperationException("Redemption not found.");

        var originalTransaction = (await loyaltyRepository.GetTransactionsByCustomerAsync(redemption.CustomerId, cancellationToken))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault(x => x.OrderId == redemption.OrderId && x.Source == "LoyaltyRedemption" && x.TransactionType == "Redeem");

        if (originalTransaction is null)
        {
            throw new InvalidOperationException("Original redemption transaction was not found.");
        }

        var reversal = LoyaltyReversalEngine.CreateReversal(originalTransaction);
        var reversalResult = await loyaltyRepository.CreateTransactionAsync(redemption.CustomerId, "Reversal", Math.Abs(reversal.Points), redemption.OrderId, null, "LoyaltyRedemptionReversal", $"Reversal:{redemptionId}", cancellationToken);

        return await redemptionRepository.ReverseAsync(redemptionId, reversalResult.Transaction.LoyaltyTransactionId, cancellationToken);
    }
}
