using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class OrderService(IOrderRepository repository) : IOrderService
{
    public Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken) => repository.GetListAsync(cancellationToken);
    public Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken) => repository.GetByIdAsync(orderId, cancellationToken);
    public Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken) => repository.CreateAsync(order, cancellationToken);
    public Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken) => repository.GetItemsAsync(orderId, cancellationToken);
    public Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken) => repository.GetPiecesAsync(orderId, cancellationToken);
    public Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken) => repository.GetDeliveryAsync(orderId, cancellationToken);
}