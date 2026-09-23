namespace LUMAR_ERP_API_V2.DTOs.Pricing;

public sealed record PricingProfitSettingsDto(
    decimal GlobalProfitPercentage,
    IReadOnlyDictionary<int, decimal> ProductTypeProfitPercentages);

public sealed class PricingProfitPercentageRequestDto
{
    public decimal ProfitPercentage { get; init; }
}
