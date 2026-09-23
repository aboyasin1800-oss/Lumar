using LUMAR_ERP_API_V2.DTOs.Loyalty;

namespace LUMAR_ERP_API_V2.Utilities;

public static class LoyaltyAccountResolver
{
    public static LoyaltyAccountDto EnsureAccount(LoyaltyAccountDto? current, int customerId)
    {
        if (current is not null) return current;

        var now = DateTime.UtcNow;
        return new LoyaltyAccountDto(0, customerId, 0m, 0m, 0m, 0m, null, now, now, now);
    }

    public static LoyaltyTransactionResultDto ApplyTransaction(
        LoyaltyAccountDto account,
        string transactionType,
        decimal points,
        int? orderId,
        int? rewardId,
        string source,
        string? notes)
    {
        var normalizedType = (transactionType ?? string.Empty).Trim();
        var balanceBefore = account.CurrentPoints;
        var balanceAfter = balanceBefore + points;
        var updatedAccount = account with
        {
            CurrentPoints = balanceAfter,
            UpdatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        if (normalizedType.Equals("Earn", StringComparison.OrdinalIgnoreCase))
        {
            updatedAccount = updatedAccount with
            {
                LifetimeEarnedPoints = account.LifetimeEarnedPoints + Math.Max(points, 0m)
            };
        }

        if (normalizedType.Equals("Reversal", StringComparison.OrdinalIgnoreCase) || normalizedType.Equals("Adjust", StringComparison.OrdinalIgnoreCase))
        {
            updatedAccount = updatedAccount with
            {
                LifetimeRedeemedPoints = account.LifetimeRedeemedPoints + Math.Max(-points, 0m)
            };
        }

        var transaction = new LoyaltyTransactionDto(
            0,
            account.LoyaltyAccountId,
            account.CustomerId,
            orderId,
            rewardId,
            normalizedType,
            points,
            balanceBefore,
            balanceAfter,
            source,
            notes,
            DateTime.UtcNow);

        return new LoyaltyTransactionResultDto(transaction, updatedAccount, balanceBefore, balanceAfter);
    }
}
