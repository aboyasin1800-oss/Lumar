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
    Task<OrderDetailsDto?> CollectCustomerPaymentAsync(int orderId, decimal amount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken);
    Task<OrderDetailsDto?> SettleCustomerBalanceAsync(int orderId, decimal amount, decimal discountAmount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken);
    Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken);
    Task<OrderDetailsDto?> DeliverAsync(int orderId, CancellationToken cancellationToken);
    Task<OrderDetailsDto?> WaiveRemainingBalanceAsync(int orderId, CancellationToken cancellationToken);
    Task<OrderDetailsDto?> RecognizeDeliveryRevenueAsync(int orderId, CancellationToken cancellationToken);
    Task<OrderDetailsDto?> CancelOrderAsync(int orderId, string? reason, string? cancelledBy, CancellationToken cancellationToken);
}