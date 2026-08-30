using LUMAR_ERP_API_V2.DTOs.Orders;

namespace LUMAR_ERP_API_V2.Services;

public interface IOrderService
{
    Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken);
    Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken);
    Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken);
    Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken);
}