using LUMAR_ERP_API_V2.DTOs.Orders;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IReadyMadeSalesRepository
{
    Task<ReadyMadeSaleResultDto> CreateAsync(CreateReadyMadeSaleDto sale, CancellationToken cancellationToken);
}