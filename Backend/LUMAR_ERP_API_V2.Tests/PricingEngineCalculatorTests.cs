using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class PricingEngineCalculatorTests
{
    [Fact]
    public void Applies_piece_profit_then_global_profit()
    {
        var result = PricingEngineCalculator.Calculate(
            consumptionPerPiece: 0,
            inchPrice: 0,
            sewingCost: 8000,
            consumablesCost: 0,
            ironingAndPackagingCost: 0,
            fixedOperatingCost: 0,
            pieceProfitPercentage: 20,
            globalProfitPercentage: 10,
            quantity: 1);

        Assert.Equal(8000m, result.FullCostPerPiece);
        Assert.Equal(1600m, result.PieceProfitValuePerPiece);
        Assert.Equal(9600m, result.PriceAfterPieceProfitPerPiece);
        Assert.Equal(960m, result.GlobalProfitValuePerPiece);
        Assert.Equal(10560m, result.FinalPricePerPiece);
    }

    [Fact]
    public void Accepts_zero_global_profit_and_two_hundred_percent_piece_profit()
    {
        var result = PricingEngineCalculator.Calculate(
            consumptionPerPiece: 0,
            inchPrice: 0,
            sewingCost: 8000,
            consumablesCost: 0,
            ironingAndPackagingCost: 0,
            fixedOperatingCost: 0,
            pieceProfitPercentage: 200,
            globalProfitPercentage: 0,
            quantity: 1);

        Assert.Equal(24000m, result.FinalPricePerPiece);
    }

    [Fact]
    public void Calculates_fabric_and_total_values_per_piece_and_quantity()
    {
        var result = PricingEngineCalculator.Calculate(
            consumptionPerPiece: 65,
            inchPrice: 2,
            sewingCost: 100,
            consumablesCost: 20,
            ironingAndPackagingCost: 30,
            fixedOperatingCost: 50,
            pieceProfitPercentage: 0,
            globalProfitPercentage: 0,
            quantity: 3);

        Assert.Equal(130m, result.FabricCostPerPiece);
        Assert.Equal(390m, result.FabricCostTotal);
        Assert.Equal(330m, result.FullCostPerPiece);
        Assert.Equal(990m, result.FullCostTotal);
        Assert.Equal(990m, result.FinalPriceTotal);
    }

    [Fact]
    public void Rejects_negative_percentages()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PricingEngineCalculator.Calculate(
            consumptionPerPiece: 1,
            inchPrice: 1,
            sewingCost: 1,
            consumablesCost: 1,
            ironingAndPackagingCost: 1,
            fixedOperatingCost: 1,
            pieceProfitPercentage: -1,
            globalProfitPercentage: 0,
            quantity: 1));
    }
}
