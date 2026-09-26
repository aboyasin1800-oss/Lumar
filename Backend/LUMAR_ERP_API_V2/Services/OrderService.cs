using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class OrderService(
    IOrderRepository repository,
    IVipLevelEvaluationService vipLevelEvaluationService,
    IOrderLoyaltyIntegrationService orderLoyaltyIntegrationService) : IOrderService
{
    public Task<IReadOnlyList<OrderListDto>> GetListAsync(CancellationToken cancellationToken) => repository.GetListAsync(cancellationToken);
    public Task<OrderDetailsDto?> GetByIdAsync(int orderId, CancellationToken cancellationToken) => repository.GetByIdAsync(orderId, cancellationToken);
    public async Task<OrderDetailsDto?> CreateAsync(CreateOrderDto order, CancellationToken cancellationToken)
    {
        if (order.Items.Any(item => item.ProductTypeId <= 0))
            throw new ArgumentException("معرف نوع المنتج الرسمي مطلوب لكل قطعة قبل حفظ الطلب.");

        var created = await repository.CreateAsync(order, cancellationToken);
        if (created is not null)
        {
            await vipLevelEvaluationService.EvaluateNetworkAsync(created.CustomerId, cancellationToken);
            await orderLoyaltyIntegrationService.ProcessIfEligibleAsync(created.OrderId, cancellationToken);
        }

        return created;
    }
    public Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken) => repository.GetItemsAsync(orderId, cancellationToken);
    public Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken) => repository.GetPiecesAsync(orderId, cancellationToken);
    public Task<IReadOnlyList<OrderFabricDto>> GetFabricsAsync(int orderId, CancellationToken cancellationToken) => repository.GetFabricsAsync(orderId, cancellationToken);
    public Task<IReadOnlyList<OrderPaymentDto>> GetPaymentsAsync(int orderId, CancellationToken cancellationToken) => repository.GetPaymentsAsync(orderId, cancellationToken);
    public async Task<OrderDetailsDto?> CollectCustomerPaymentAsync(int orderId, decimal amount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken)
    {
        var collected = await repository.CollectCustomerPaymentAsync(orderId, amount, paymentMethod, cashAccountId, referenceNumber, notes, cancellationToken);
        if (collected is not null)
        {
            await orderLoyaltyIntegrationService.ProcessIfEligibleAsync(orderId, cancellationToken);
        }

        return collected;
    }

    public async Task<OrderDetailsDto?> SettleCustomerBalanceAsync(int orderId, decimal amount, decimal discountAmount, string? paymentMethod, int? cashAccountId, string? referenceNumber, string? notes, CancellationToken cancellationToken)
    {
        var settled = await repository.SettleCustomerBalanceAsync(orderId, amount, discountAmount, paymentMethod, cashAccountId, referenceNumber, notes, cancellationToken);
        if (settled is not null)
        {
            await orderLoyaltyIntegrationService.ProcessIfEligibleAsync(orderId, cancellationToken);
        }

        return settled;
    }
    public Task<OrderDeliveryDto?> GetDeliveryAsync(int orderId, CancellationToken cancellationToken) => repository.GetDeliveryAsync(orderId, cancellationToken);
    public async Task<OrderDetailsDto?> DeliverAsync(int orderId, CancellationToken cancellationToken)
    {
        var delivered = await repository.DeliverAsync(orderId, cancellationToken);
        if (delivered is null)
        {
            return null;
        }

        return await repository.RecognizeDeliveryRevenueAsync(orderId, cancellationToken) ?? delivered;
    }
    public Task<OrderDetailsDto?> WaiveRemainingBalanceAsync(int orderId, CancellationToken cancellationToken) => repository.WaiveRemainingBalanceAsync(orderId, cancellationToken);
    public Task<OrderDetailsDto?> RecognizeDeliveryRevenueAsync(int orderId, CancellationToken cancellationToken) => repository.RecognizeDeliveryRevenueAsync(orderId, cancellationToken);
    public async Task<OrderDetailsDto?> CancelOrderAsync(int orderId, string? reason, string? cancelledBy, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(orderId, cancellationToken);
        var cancelled = await repository.CancelOrderAsync(orderId, reason, cancelledBy, cancellationToken);
        if (cancelled is not null)
        {
            await vipLevelEvaluationService.EvaluateNetworkAsync(existing?.CustomerId ?? cancelled.CustomerId, cancellationToken);
        }

        return cancelled;
    }
}