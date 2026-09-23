using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class ReadyMadeSalesService(
    IReadyMadeSalesRepository repository,
    IOrderLoyaltyIntegrationService orderLoyaltyIntegrationService) : IReadyMadeSalesService
{
    public async Task<ReadyMadeSaleResultDto> CreateAsync(CreateReadyMadeSaleDto sale, CancellationToken cancellationToken)
    {
        var result = await repository.CreateAsync(sale, cancellationToken);
        await orderLoyaltyIntegrationService.ProcessIfEligibleAsync(result.OrderId, cancellationToken);
        return result;
    }
}