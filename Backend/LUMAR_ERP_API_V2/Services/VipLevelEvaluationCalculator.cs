using LUMAR_ERP_API_V2.DTOs.Loyalty;

namespace LUMAR_ERP_API_V2.Services;

public sealed record VipLevelEvaluationSelection(
    VipLevelEvaluationCriteriaDto Criteria,
    decimal Score);

public static class VipLevelEvaluationCalculator
{
    public static VipLevelEvaluationSelection SelectLevel(
        VipLevelEvaluationSourceDto row,
        IReadOnlyList<VipLevelEvaluationCriteriaDto> criteria)
    {
        foreach (var item in criteria.OrderByDescending(item => item.Priority).ThenByDescending(item => item.VipLevelId))
        {
            if (item.MinimumScore <= 0m)
            {
                return new VipLevelEvaluationSelection(item, 0m);
            }

            if (row.DirectReferralCount <= 0 || row.NetworkSize <= 0) continue;

            var score = CalculateScore(row, item);
            if (score >= item.MinimumScore)
            {
                return new VipLevelEvaluationSelection(item, score);
            }
        }

        var fallback = criteria.OrderBy(item => item.Priority).ThenBy(item => item.VipLevelId).First();
        return new VipLevelEvaluationSelection(fallback, CalculateScore(row, fallback));
    }

    public static decimal CalculateScore(
        VipLevelEvaluationSourceDto row,
        VipLevelEvaluationCriteriaDto criteria)
    {
        decimal CappedRatio(int actual, int target) =>
            target <= 0 ? 1m : Math.Min((decimal)actual / target, 1m);

        var score =
            CappedRatio(row.DirectReferralCount, criteria.MinimumDirectReferrals) * criteria.DirectReferralWeight +
            CappedRatio(row.OwnOrderCount, criteria.MinimumOwnOrders) * criteria.OwnOrderWeight +
            CappedRatio(row.NetworkOrderCount, criteria.MinimumNetworkOrders) * criteria.NetworkOrderWeight +
            CappedRatio(row.NetworkSize, criteria.MinimumNetworkSize) * criteria.NetworkSizeWeight;

        return Math.Round(score, 2, MidpointRounding.AwayFromZero);
    }
}