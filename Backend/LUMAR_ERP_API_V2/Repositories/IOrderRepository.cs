using LUMAR_ERP_API_V2.DTOs.Orders;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IOrderRepository
{
    Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken);
    Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken);
    Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderFabricDto>> GetFabricsAsync(int orderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderPaymentDto>> GetPaymentsAsync(int orderId, CancellationToken cancellationToken);
    Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken);
}