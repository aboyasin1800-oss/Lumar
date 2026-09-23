namespace LUMAR_ERP_API_V2.Utilities;

public sealed record PricingCalculation(
    decimal FabricCostPerPiece,
    decimal FabricCostTotal,
    decimal OperationalCostPerPiece,
    decimal FullCostPerPiece,
    decimal FullCostTotal,
    decimal PieceProfitValuePerPiece,
    decimal PriceAfterPieceProfitPerPiece,
    decimal GlobalProfitValuePerPiece,
    decimal FinalPricePerPiece,
    decimal FinalPriceTotal);

public static class PricingEngineCalculator
{
    public static PricingCalculation Calculate(
        decimal consumptionPerPiece,
        decimal inchPrice,
        decimal sewingCost,
        decimal consumablesCost,
        decimal ironingAndPackagingCost,
        decimal fixedOperatingCost,
        decimal pieceProfitPercentage,
        decimal globalProfitPercentage,
        int quantity)
    {
        if (consumptionPerPiece < 0 || inchPrice < 0 || sewingCost < 0 ||
            consumablesCost < 0 || ironingAndPackagingCost < 0 ||
            fixedOperatingCost < 0 || pieceProfitPercentage < 0 ||
            globalProfitPercentage < 0 || quantity <= 0)
        {
            throw new ArgumentOutOfRangeException();
        }

        var fabricCostPerPiece = consumptionPerPiece * inchPrice;
        var operationalCostPerPiece = sewingCost + consumablesCost +
            ironingAndPackagingCost + fixedOperatingCost;
        var fullCostPerPiece = fabricCostPerPiece + operationalCostPerPiece;
        var pieceProfitValuePerPiece = fullCostPerPiece * pieceProfitPercentage / 100m;
        var priceAfterPieceProfitPerPiece = fullCostPerPiece + pieceProfitValuePerPiece;
        var globalProfitValuePerPiece = priceAfterPieceProfitPerPiece * globalProfitPercentage / 100m;
        var finalPricePerPiece = priceAfterPieceProfitPerPiece + globalProfitValuePerPiece;

        return new PricingCalculation(
            fabricCostPerPiece,
            fabricCostPerPiece * quantity,
            operationalCostPerPiece,
            fullCostPerPiece,
            fullCostPerPiece * quantity,
            pieceProfitValuePerPiece,
            priceAfterPieceProfitPerPiece,
            globalProfitValuePerPiece,
            finalPricePerPiece,
            finalPricePerPiece * quantity);
    }
}
