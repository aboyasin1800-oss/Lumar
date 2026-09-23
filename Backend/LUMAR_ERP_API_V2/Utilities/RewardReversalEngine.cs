using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.DTOs.Referral;

namespace LUMAR_ERP_API_V2.Utilities;

public static class RewardReversalEngine
{
    public static IReadOnlyList<RewardReversalResultDto> ReverseReferralRewards(IEnumerable<ReferralTransactionDto> referralTransactions)
    {
        var items = referralTransactions
            .Where(x => x.TransactionType == "RewardGranted" && x.LoyaltyPoints > 0m)
            .Select(x => new RewardReversalResultDto(
                0,
                x.ReferrerCustomerId,
                x.ReferredCustomerId ?? x.ReferrerCustomerId,
                x.LoyaltyPoints,
                "RewardReversal",
                $"Original reward reversal for order {x.OrderId ?? 0}."))
            .ToList();

        return items;
    }

    public static RewardReversalResultDto ReverseBuyerEarn(LoyaltyTransactionDto buyerEarnTransaction)
    {
        return new RewardReversalResultDto(
            0,
            buyerEarnTransaction.CustomerId,
            buyerEarnTransaction.CustomerId,
            Math.Abs(buyerEarnTransaction.Points),
            "BuyerEarnReversal",
            $"Buyer earn reversal for order {buyerEarnTransaction.OrderId ?? 0}.");
    }
}
