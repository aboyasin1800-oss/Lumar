using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class OrderService(
    IOrderRepository repository,
    IVipLevelEvaluationService vipLevelEvaluationService,
    IOrderLoyaltyIntegrationService orderLoyaltyIntegrationService,
    IMessageWriteService? messageWriteService = null) : IOrderService
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

            if (messageWriteService is not null)
            {
                try
                {
                    var messageRequest = new MessageWriteRequest(
                        created.CustomerId,
                        created.OrderId,
                        "OrderCreated",
                        "OrderCreated",
                        created.PhoneNumber ?? string.Empty,
                        created.OrderNumber,
                        "Order",
                        new Dictionary<string, object?>
                        {
                            ["CustomerName"] = created.CustomerName,
                            ["OrderNumber"] = created.OrderNumber,
                            ["OrderId"] = created.OrderId,
                            ["Amount"] = created.TotalAmount,
                            ["PhoneNumber"] = created.PhoneNumber
                        });

                    await messageWriteService.TryCreateAsync(messageRequest, cancellationToken);
                }
                catch
                {
                    // The order commit remains valid even when the internal message side is unavailable.
                }
            }
        }

        return created;
    }
    public Task<IReadOnlyList<OrderItemDto>> GetItemsAsync(int orderId, CancellationToken cancellationToken) => repository.GetItemsAsync(orderId, cancellationToken);
    public Task<IReadOnlyList<OrderPieceDto>> GetPiecesAsync(int orderId, CancellationToken cancellationToken) => repository.GetPiecesAsync(orderId, cancellationToken);
    public Task<IReadOnlyList<OrderFabricDto>> GetFabricsAsync(int orderId, CancellationToken cancellationToken) => repository.GetFabricsAsync(orderId, cancellationToken);
    public Task<IReadOnlyList<OrderPaymentDto>> GetPaymentsAsync(int orderId, CancellationToken cancellationToken) => repository.GetPaymentsAsync(orderId, cancellationToken);
    public Task<IReadOnlyList<OrderTrackingDto>?> GetTrackingAsync(int orderId, CancellationToken cancellationToken) => repository.GetTrackingAsync(orderId, cancellationToken);
    public static DailyOrderWorkloadStatusDto EvaluateDailyOrderWorkload(int dailyOrderCount)
    {
        var isWorkloadHigh = dailyOrderCount >= 25;
        return new DailyOrderWorkloadStatusDto(isWorkloadHigh, isWorkloadHigh
            ? "نعتذر عن أي تأخير محتمل. يوجد ضغط في الطلبات حالياً، ويعمل فريقنا على إنجاز طلبكم بأسرع وقت ممكن."
            : null);
    }

    public async Task<DailyOrderWorkloadStatusDto> GetDailyWorkloadStatusAsync(CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var dayStartUtc = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);
        var nextDayUtc = dayStartUtc.AddDays(1);
        var dailyOrderCount = await repository.CountCreatedOrdersForDayAsync(dayStartUtc, nextDayUtc, cancellationToken);
        return EvaluateDailyOrderWorkload(dailyOrderCount);
    }

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
        if (delivered is not null && messageWriteService is not null)
        {
            try
            {
                await messageWriteService.TryCreateAsync(new MessageWriteRequest(
                    delivered.CustomerId,
                    delivered.OrderId,
                    "OrderDelivered",
                    "OrderDelivered",
                    delivered.PhoneNumber ?? string.Empty,
                    delivered.OrderNumber,
                    "Order",
                    new Dictionary<string, object?>
                    {
                        ["CustomerName"] = delivered.CustomerName ?? "Customer",
                        ["OrderNumber"] = delivered.OrderNumber,
                        ["OrderId"] = delivered.OrderId,
                        ["Amount"] = delivered.TotalAmount,
                    }), cancellationToken);
            }
            catch
            {
                // Order completion remains valid even if the internal message side is unavailable.
            }
        }

        return delivered;
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