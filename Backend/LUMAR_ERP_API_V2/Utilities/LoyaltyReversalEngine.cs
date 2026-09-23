using LUMAR_ERP_API_V2.DTOs.Loyalty;

namespace LUMAR_ERP_API_V2.Utilities;

public static class LoyaltyReversalEngine
{
    public static LoyaltyTransactionDto CreateReversal(LoyaltyTransactionDto original)
    {
        var restoredPoints = Math.Abs(original.Points);
        return new LoyaltyTransactionDto(
            0,
            original.LoyaltyAccountId,
            original.CustomerId,
            original.OrderId,
            original.RewardId,
            "Reversal",
            restoredPoints,
            original.BalanceAfter,
            original.BalanceAfter + restoredPoints,
            "LoyaltyReversal",
            $"Reversal of original {original.TransactionType} transaction.",
            DateTime.UtcNow);
    }
}
