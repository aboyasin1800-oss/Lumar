using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class PricingEngineCalculatorTests
{
    [Fact]
    public void Preserves_independent_official_piece_prices_and_fixed_discount_shares()
    {
        var values = PricingEngineCalculator.AllocatePieceValues([20000m, 15406.25m], 1000m);

        Assert.Equal(20000m, values[0].SalePrice);
        Assert.Equal(15406.25m, values[1].SalePrice);
        Assert.Equal(35406.25m, values.Sum(value => value.SalePrice));
        Assert.Equal(1000m, values.Sum(value => value.DiscountShare));
        Assert.All(values, value => Assert.InRange(value.DiscountShare, 0m, value.SalePrice));
        var added = PricingEngineCalculator.AllocatePieceValues([15406.25m], 0m);
        Assert.Equal(0m, added[0].DiscountShare);
    }

    [Fact]
    public void Piece_allocations_preserve_money_totals_when_unit_prices_have_fractional_cents()
    {
        var values = PricingEngineCalculator.AllocatePieceValues([0.335m, 0.335m, 0.335m], 0.50m);

        Assert.Equal(1.01m, values.Sum(value => value.SalePrice));
        Assert.Equal(0.50m, values.Sum(value => value.DiscountShare));
        Assert.All(values, value => Assert.InRange(value.SalePrice, 0.33m, 0.34m));
        Assert.All(values, value => Assert.Equal(value.DiscountShare, decimal.Round(value.DiscountShare, 2)));
    }

    [Fact]
    public void Piece_allocations_reject_invalid_prices_and_discount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PricingEngineCalculator.AllocatePieceValues([-1m], 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => PricingEngineCalculator.AllocatePieceValues([10m], 11m));
        Assert.Throws<ArgumentOutOfRangeException>(() => PricingEngineCalculator.AllocatePieceValues([10m], -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => PricingEngineCalculator.AllocatePieceValues([10m], 0.001m));
    }

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
