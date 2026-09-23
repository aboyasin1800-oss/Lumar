using LUMAR_ERP_API_V2.DTOs.Orders;

namespace LUMAR_ERP_API_V2.Services;

public interface IReadyMadeSalesService
{
    Task<ReadyMadeSaleResultDto> CreateAsync(CreateReadyMadeSaleDto sale, CancellationToken cancellationToken);
}