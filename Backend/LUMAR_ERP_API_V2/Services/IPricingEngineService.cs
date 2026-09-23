using LUMAR_ERP_API_V2.DTOs.Pricing;

namespace LUMAR_ERP_API_V2.Services;

public interface IPricingEngineService
{
    Task<PricingEngineResponseDto> CalculateAsync(
        PricingEngineRequestDto request,
        CancellationToken cancellationToken);
}
