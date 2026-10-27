using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class OrderService(
    IOrderRepository repository,
    IVipLevelEvaluationService vipLevelEvaluationService,
    IOrderLoyaltyIntegrationService orderLoyaltyIntegrationService,
    IMessageWriteService? messageWriteService = null,
    ICustomerRepository? customerRepository = null,
    ISettingsRepository? settingsRepository = null) : IOrderService
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
            if (vipLevelEvaluationService is not null)
                await vipLevelEvaluationService.EvaluateNetworkAsync(created.CustomerId, cancellationToken);

            OrderLoyaltyProcessingResult? loyaltyResult = null;
            if (orderLoyaltyIntegrationService is not null)
                loyaltyResult = await orderLoyaltyIntegrationService.ProcessIfEligibleAsync(created.OrderId, cancellationToken);

            if (messageWriteService is not null)
            {
                try
                {
                    var messageContext = await BuildOrderLifecycleContextAsync(created, cancellationToken, loyaltyResult);
                    var messageRequest = new MessageWriteRequest(
                        created.CustomerId,
                        created.OrderId,
                        "OrderCreated",
                        "OrderCreated",
                        created.PhoneNumber ?? string.Empty,
                        created.OrderNumber,
                        "Order",
                        messageContext);

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
                var messageContext = await BuildOrderLifecycleContextAsync(delivered, cancellationToken);
                await messageWriteService.TryCreateAsync(new MessageWriteRequest(
                    delivered.CustomerId,
                    delivered.OrderId,
                    "OrderDelivered",
                    "OrderDelivered",
                    delivered.PhoneNumber ?? string.Empty,
                    delivered.OrderNumber,
                    "Order",
                    messageContext), cancellationToken);
            }
            catch
            {
                // Order completion remains valid even if the internal message side is unavailable.
            }
        }

        return delivered;
    }
    private async Task<Dictionary<string, object?>> BuildOrderLifecycleContextAsync(OrderDetailsDto order, CancellationToken cancellationToken, OrderLoyaltyProcessingResult? loyaltyResult = null)
    {
        var context = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CustomerName"] = order.CustomerName,
            ["OrderNumber"] = order.OrderNumber,
            ["OrderId"] = order.OrderId,
            ["Amount"] = order.TotalAmount,
            ["TotalAmount"] = order.TotalAmount,
            ["PaidAmount"] = order.PaidAmount,
            ["RemainingAmount"] = order.RemainingAmount,
            ["DeliveryDate"] = NormalizeCalendarDate(order.DeliveryDate),
            ["PhoneNumber"] = order.PhoneNumber,
            ["CustomerPhoneNumber"] = order.PhoneNumber,
            ["CurrencyName"] = await GetRequiredSettingValueAsync("Currency", cancellationToken),
            ["StoreName"] = await GetRequiredSettingValueAsync("StoreName", cancellationToken),
            ["CustomerCode"] = await GetCustomerCodeAsync(order.CustomerId, cancellationToken),
            ["PointsEarned"] = loyaltyResult?.BuyerDelta,
            ["PointsBalance"] = loyaltyResult?.BuyerAccount.CurrentPoints,
        };

        var items = await repository.GetItemsAsync(order.OrderId, cancellationToken);
        context["OrderDetails"] = string.Join(", ", items.Select(item => item.PieceType).Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase));

        return context;
    }

    private static DateTime? NormalizeCalendarDate(DateTime? input)
    {
        if (!input.HasValue)
            return null;

        var calendar = input.Value.Date;
        return DateTime.SpecifyKind(calendar, DateTimeKind.Unspecified);
    }

    private async Task<string?> GetCustomerCodeAsync(int customerId, CancellationToken cancellationToken)
    {
        if (customerRepository is null)
            return null;

        var customer = await customerRepository.GetByIdAsync(customerId, cancellationToken);
        return string.IsNullOrWhiteSpace(customer?.CustomerCode) ? null : customer.CustomerCode;
    }

    private async Task<string?> GetRequiredSettingValueAsync(string settingName, CancellationToken cancellationToken)
    {
        if (settingsRepository is null)
            return null;

        var setting = await settingsRepository.GetByKeyAsync(settingName, cancellationToken);
        return string.IsNullOrWhiteSpace(setting?.SettingValue) ? null : setting.SettingValue;
    }

    public Task<OrderDetailsDto?> WaiveRemainingBalanceAsync(int orderId, CancellationToken cancellationToken) => repository.WaiveRemainingBalanceAsync(orderId, cancellationToken);
    public Task<OrderDetailsDto?> RecognizeDeliveryRevenueAsync(int orderId, CancellationToken cancellationToken) => repository.RecognizeDeliveryRevenueAsync(orderId, cancellationToken);
    public async Task<OrderDetailsDto?> CancelOrderAsync(int orderId, string? reason, string? cancelledBy, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(orderId, cancellationToken);
        var cancelled = await repository.CancelOrderAsync(orderId, reason, cancelledBy, cancellationToken);
        if (cancelled is not null)
        {
            if (vipLevelEvaluationService is not null)
            {
                await vipLevelEvaluationService.EvaluateNetworkAsync(existing?.CustomerId ?? cancelled.CustomerId, cancellationToken);
            }

            if (messageWriteService is not null)
            {
                try
                {
                    await messageWriteService.TryCreateAsync(new MessageWriteRequest(
                        cancelled.CustomerId,
                        cancelled.OrderId,
                        "OrderCancelled",
                        "OrderCancelled",
                        cancelled.PhoneNumber ?? string.Empty,
                        cancelled.OrderNumber,
                        "Order",
                        new Dictionary<string, object?>
                        {
                            ["CustomerName"] = cancelled.CustomerName ?? "Customer",
                            ["OrderNumber"] = cancelled.OrderNumber,
                            ["OrderId"] = cancelled.OrderId,
                            ["Amount"] = cancelled.TotalAmount,
                            ["Reason"] = reason,
                        }), cancellationToken);
                }
                catch
                {
                    // Order cancellation remains valid even if the internal message side is unavailable.
                }
            }
        }

        return cancelled;
    }
}