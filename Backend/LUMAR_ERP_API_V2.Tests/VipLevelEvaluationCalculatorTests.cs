using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Services;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class VipLevelEvaluationCalculatorTests
{
    [Fact]
    public void CalculateScore_UsesReferralsOrdersAndNetworkSizeWeights()
    {
        var row = Source(direct: 1, ownOrders: 5, networkOrders: 5, networkSize: 4);
        var criteria = Criteria("GOLD", 1, 5, 5, 4, 80, priority: 30);

        var score = VipLevelEvaluationCalculator.CalculateScore(row, criteria);

        Assert.Equal(100m, score);
    }

    [Fact]
    public void SelectLevel_PromotesOnlyWhenWeightedThresholdIsReached()
    {
        var criteria = new[]
        {
            Criteria("BRONZE", 0, 0, 0, 0, 0, priority: 1),
            Criteria("SILVER", 1, 1, 3, 2, 80, priority: 2),
            Criteria("GOLD", 1, 5, 5, 4, 80, priority: 3),
        };

        var promoted = VipLevelEvaluationCalculator.SelectLevel(
            Source(direct: 1, ownOrders: 5, networkOrders: 5, networkSize: 4),
            criteria);
        var downgraded = VipLevelEvaluationCalculator.SelectLevel(
            Source(direct: 1, ownOrders: 1, networkOrders: 0, networkSize: 2),
            criteria);

        Assert.Equal("GOLD", promoted.Criteria.Code);
        Assert.Equal("BRONZE", downgraded.Criteria.Code);
    }

    [Fact]
    public void CalculateScore_CapsEachMetricWithoutIgnoringNetworkDepthCounts()
    {
        var row = Source(direct: 2, ownOrders: 20, networkOrders: 15, networkSize: 12);
        var criteria = Criteria("PLATINUM", 2, 10, 10, 10, 80, priority: 4);

        var score = VipLevelEvaluationCalculator.CalculateScore(row, criteria);

        Assert.Equal(100m, score);
        Assert.Equal(4, row.Level4ReferralCount);
    }

    private static VipLevelEvaluationSourceDto Source(
        int direct,
        int ownOrders,
        int networkOrders,
        int networkSize) => new(
        1,
        "C-1",
        "Test Customer",
        1,
        "BRONZE",
        "Bronze",
        null,
        null,
        direct,
        direct,
        2,
        3,
        4,
        networkSize,
        4,
        ownOrders,
        networkOrders);

    private static VipLevelEvaluationCriteriaDto Criteria(
        string code,
        int direct,
        int ownOrders,
        int networkOrders,
        int networkSize,
        decimal minimumScore,
        int priority) => new(
        priority,
        code,
        code,
        priority,
        direct,
        ownOrders,
        networkOrders,
        networkSize,
        minimumScore,
        30,
        25,
        25,
        20,
        true);
}