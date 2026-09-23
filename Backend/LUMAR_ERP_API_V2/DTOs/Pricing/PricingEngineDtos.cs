using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Pricing;

public sealed class PricingEngineRequestDto
{
    [Range(1, int.MaxValue)]
    public int ProductTypeId { get; init; }

    [Required, StringLength(50)]
    public string FabricCode { get; init; } = string.Empty;

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Consumption { get; init; }

    [Required, StringLength(20)]
    public string ConsumptionUnit { get; init; } = string.Empty;

    [Range(1, 1000000)]
    public int Quantity { get; init; } = 1;

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal PieceProfitPercentage { get; init; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal GlobalProfitPercentage { get; init; }
}

public sealed record PricingEngineResponseDto(
    bool IsReady,
    IReadOnlyList<string> Reasons,
    int ProductTypeId,
    string FabricCode,
    string ConsumptionUnit,
    decimal ConsumptionPerPiece,
    int Quantity,
    decimal? InchPrice,
    decimal? FabricCostPerPiece,
    decimal? FabricCostTotal,
    decimal? SewingCost,
    decimal? ConsumablesCost,
    decimal? IroningAndPackagingCost,
    decimal? FixedOperatingCost,
    decimal? OperationalCostPerPiece,
    decimal? FullCostPerPiece,
    decimal? FullCostTotal,
    decimal? PieceProfitPercentage,
    decimal? PieceProfitValuePerPiece,
    decimal? PriceAfterPieceProfitPerPiece,
    decimal? GlobalProfitPercentage,
    decimal? GlobalProfitValuePerPiece,
    decimal? FinalPricePerPiece,
    decimal? FinalPriceTotal,
    string? FabricSource,
    string? OperatingCostSource);
