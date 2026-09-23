using LUMAR_ERP_API_V2.DTOs.Pricing;

namespace LUMAR_ERP_API_V2.Services;

public interface IPricingProfitSettingsService
{
    Task<PricingProfitSettingsDto> GetAsync(CancellationToken cancellationToken);
    Task<PricingProfitSettingsDto> SetGlobalAsync(decimal value, CancellationToken cancellationToken);
    Task<PricingProfitSettingsDto> SetProductTypeAsync(int productTypeId, decimal value, CancellationToken cancellationToken);
}
