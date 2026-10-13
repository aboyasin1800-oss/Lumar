using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class ReadyMadeSalesService(
    IReadyMadeSalesRepository repository,
    IOrderLoyaltyIntegrationService orderLoyaltyIntegrationService,
    IMessageWriteService? messageWriteService = null) : IReadyMadeSalesService
{
    public async Task<ReadyMadeSaleResultDto> CreateAsync(CreateReadyMadeSaleDto sale, CancellationToken cancellationToken)
    {
        var result = await repository.CreateAsync(sale, cancellationToken);
        await orderLoyaltyIntegrationService.ProcessIfEligibleAsync(result.OrderId, cancellationToken);
        await TrySendMessageAsync(
            result.CustomerId,
            result.OrderId,
            "ReadyMadeSaleCompleted",
            "ReadyMadeSaleCompleted",
            result.CustomerName,
            result.OrderNumber,
            result.TotalAmount,
            result.NetAmount,
            cancellationToken);
        return result;
    }

    private async Task TrySendMessageAsync(
        int customerId,
        int orderId,
        string eventType,
        string templateKey,
        string? customerName,
        string orderNumber,
        decimal totalAmount,
        decimal netAmount,
        CancellationToken cancellationToken)
    {
        if (messageWriteService is null || customerId <= 0)
            return;

        try
        {
            var request = new MessageWriteRequest(
                customerId,
                orderId,
                eventType,
                templateKey,
                string.Empty,
                orderNumber,
                "Order",
                new Dictionary<string, object?>
                {
                    ["CustomerName"] = customerName ?? "Customer",
                    ["OrderNumber"] = orderNumber,
                    ["OrderId"] = orderId,
                    ["Amount"] = totalAmount,
                    ["NetAmount"] = netAmount,
                    ["ReferenceId"] = orderNumber,
                });

            await messageWriteService.TryCreateAsync(request, cancellationToken);
        }
        catch
        {
            // Order processing remains valid even if the internal message side is unavailable.
        }
    }
}