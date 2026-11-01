namespace LUMAR_ERP_API_V2.Utilities;

public sealed record PieceSaleValue(decimal SalePrice, decimal DiscountShare);

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
    public static IReadOnlyList<PieceSaleValue> AllocatePieceValues(
        IReadOnlyList<decimal> officialPiecePrices, decimal orderDiscount)
    {
        if (officialPiecePrices.Count == 0 || officialPiecePrices.Any(price => price < 0))
            throw new ArgumentOutOfRangeException(nameof(officialPiecePrices));

        var total = decimal.Round(officialPiecePrices.Sum(), 2, MidpointRounding.AwayFromZero);
        if (orderDiscount < 0 || orderDiscount > total || orderDiscount != decimal.Round(orderDiscount, 2))
            throw new ArgumentOutOfRangeException(nameof(orderDiscount));

        var saleValues = AllocateCents(officialPiecePrices, total);
        var discounts = AllocateCents(saleValues
            .Select(price => total == 0 ? 0 : orderDiscount * price / total).ToArray(), orderDiscount);

        return Array.AsReadOnly(saleValues.Select((price, index) =>
            new PieceSaleValue(price, discounts[index])).ToArray());
    }

    private static decimal[] AllocateCents(IReadOnlyList<decimal> values, decimal total)
    {
        var allocated = values.Select(value => decimal.Floor(value * 100m) / 100m).ToArray();
        var remainingCents = decimal.ToInt32((total - allocated.Sum()) * 100m);
        foreach (var index in Enumerable.Range(0, values.Count)
            .OrderByDescending(index => values[index] - allocated[index])
            .ThenBy(index => index)
            .Take(remainingCents))
        {
            allocated[index] += 0.01m;
        }
        return allocated;
    }

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
