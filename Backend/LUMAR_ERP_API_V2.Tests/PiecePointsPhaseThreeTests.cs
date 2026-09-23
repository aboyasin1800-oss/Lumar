using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class PiecePointsPhaseThreeTests
{
    [Fact]
    public void PieceSetting_UsesConfiguredPoints_WhenTypeIsActive()
    {
        var setting = new LoyaltyPiecePointSettingDto(1, "YALAQ", "يلق", 100m, true, DateTime.UtcNow, DateTime.UtcNow);

        var points = PiecePointsEngine.CalculateForItem(setting, 2m, 100m);

        Assert.Equal(200m, points);
    }

    [Fact]
    public void MissingPieceSetting_GrantsZeroPoints()
    {
        var points = PiecePointsEngine.CalculateForItem(null, 2m, 100m);

        Assert.Equal(0m, points);
    }

    [Fact]
    public void DisabledProgram_GrantsZeroPoints_EvenWithActiveTypeSetting()
    {
        var setting = new LoyaltyPiecePointSettingDto(1, "YALAQ", "يلق", 100m, true, DateTime.UtcNow, DateTime.UtcNow);

        var points = PiecePointsEngine.CalculateForItem(setting, 2m, 100m, false);

        Assert.Equal(0m, points);
    }

    [Fact]
    public void ReenabledProgram_GrantsConfiguredPointsAgain()
    {
        var setting = new LoyaltyPiecePointSettingDto(1, "YALAQ", "يلق", 100m, true, DateTime.UtcNow, DateTime.UtcNow);

        var points = PiecePointsEngine.CalculateForItem(setting, 2m, 100m, true);

        Assert.Equal(200m, points);
    }

    [Fact]
    public void BuyerEarn_AndReferralLevels_AreDistributedAsExpected()
    {
        var buyerEarn = PiecePointsEngine.CalculateBuyerEarn(100m, "YALAQ", 1m, 100m);
        var levelRewards = ReferralRewardEngine.CalculateLevels(100m, 1, 2, 3, 4);

        Assert.Equal(100m, buyerEarn);
        Assert.Equal(50m, levelRewards[0]);
        Assert.Equal(25m, levelRewards[1]);
        Assert.Equal(12.5m, levelRewards[2]);
        Assert.Equal(6.25m, levelRewards[3]);
    }

    [Fact]
    public void Fingerprint_And_Idempotency_PreventDuplicateRewardProcessing()
    {
        var fingerprint = ReferralRewardEngine.CreateFingerprint(101, 102, 999, 1, "PiecePurchase", "RL3", "YALAQ|1|100");
        var duplicateFingerprint = ReferralRewardEngine.CreateFingerprint(101, 102, 999, 1, "PiecePurchase", "RL3", "YALAQ|1|100");

        Assert.Equal(fingerprint, duplicateFingerprint);
        Assert.True(ReferralRewardEngine.IsDuplicate(fingerprint, fingerprint));
    }
}
