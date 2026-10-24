using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class ProductionService(
    IProductionRepository repository,
    IOrderLoyaltyIntegrationService orderLoyaltyIntegrationService,
    IOrderRepository orderRepository,
    IMessageWriteService? messageWriteService = null,
    ICustomerRepository? customerRepository = null,
    ISettingsRepository? settingsRepository = null) : IProductionService
{
    public Task<IReadOnlyList<PieceDto>> GetPiecesAsync(CancellationToken ct) => repository.GetPiecesAsync(ct);
    public Task<IReadOnlyList<PieceDto>> GetReadyMadePiecesAsync(CancellationToken ct) => repository.GetReadyMadePiecesAsync(ct);
    public Task<PieceDto?> GetPieceByIdAsync(int id, CancellationToken ct) => repository.GetPieceByIdAsync(id, ct);
    public Task<PieceDto?> GetReadyMadePieceByIdAsync(int id, CancellationToken ct) => repository.GetReadyMadePieceByIdAsync(id, ct);
    public Task<WorkCardDto?> GetWorkCardAsync(int id, CancellationToken ct) => repository.GetWorkCardAsync(id, ct);
    public Task<WorkCardDto?> GetReadyMadeWorkCardAsync(int id, CancellationToken ct) => repository.GetReadyMadeWorkCardAsync(id, ct);
    public Task<IReadOnlyList<TrackingEventDto>> GetPieceTrackingAsync(int id, CancellationToken ct) => repository.GetPieceTrackingAsync(id, ct);
    public Task<IReadOnlyList<ProductionStageDto>> GetStagesAsync(CancellationToken ct) => repository.GetStagesAsync(ct);
    public Task<ProductionDashboardDto> GetDashboardAsync(CancellationToken ct) => repository.GetDashboardAsync(ct);
    public Task<FactoryMonitoringDashboardDto> GetFactoryMonitoringAsync(CancellationToken ct) => repository.GetFactoryMonitoringAsync(ct);
    public Task<IReadOnlyList<ReadyMadeProductionOrderDto>> GetReadyMadeOrdersAsync(CancellationToken ct) => repository.GetReadyMadeOrdersAsync(ct);
    public Task<ReadyMadeProductionOrderDto?> GetReadyMadeOrderByIdAsync(int orderId, CancellationToken ct) => repository.GetReadyMadeOrderByIdAsync(orderId, ct);
    public Task<IReadOnlyList<ReadyMadeProductionOrderItemDto>> GetReadyMadeOrderItemsAsync(int id, CancellationToken ct) => repository.GetReadyMadeOrderItemsAsync(id, ct);
    public Task<IReadOnlyList<ReadyMadeProductionPieceDto>> GetReadyMadeItemPiecesAsync(int id, CancellationToken ct) => repository.GetReadyMadeItemPiecesAsync(id, ct);
    public Task<IReadOnlyList<TrackingEventDto>> GetReadyMadePieceTrackingAsync(int id, CancellationToken ct) => repository.GetReadyMadePieceTrackingAsync(id, ct);
    public Task<ReadyMadeProductionOrderCreateResultDto> CreateReadyMadeOrderAsync(ReadyMadeProductionOrderCreateDto order, CancellationToken ct) => repository.CreateReadyMadeOrderAsync(order, ct);
    public Task<IReadOnlyList<ProductionDeliveryDto>> GetDeliveriesAsync(CancellationToken ct) => repository.GetDeliveriesAsync(ct);
    public Task<ProductionTrackingRouteDto?> GetPieceRouteAsync(int pieceId, CancellationToken ct) => repository.GetPieceRouteAsync(pieceId, ct);
    public Task<ProductionTrackingRouteDto?> GetPieceRouteByTrackingCodeAsync(string trackingCode, CancellationToken ct) => repository.GetPieceRouteByTrackingCodeAsync(trackingCode, ct);
    public Task<ProductionTrackingRouteDto?> GetReadyMadePieceRouteAsync(int pieceId, CancellationToken ct) => repository.GetReadyMadePieceRouteAsync(pieceId, ct);
    public Task<ProductionTrackingRouteDto?> GetReadyMadePieceRouteByTrackingCodeAsync(string trackingCode, CancellationToken ct) => repository.GetReadyMadePieceRouteByTrackingCodeAsync(trackingCode, ct);
    public Task<StageAttributionCorrectionPreviewDto?> PreviewStageAttributionCorrectionAsync(StageAttributionCorrectionPreviewRequestDto request, CancellationToken ct)
        => repository.PreviewStageAttributionCorrectionAsync(request, ct);

    public Task<StageAttributionCorrectionResultDto> ExecuteStageAttributionCorrectionAsync(StageAttributionCorrectionExecutionCommand request, CancellationToken ct)
        => repository.ExecuteStageAttributionCorrectionAsync(request, ct);

    public async Task<ProductionTrackingAdvanceResultDto?> AdvancePieceStageAsync(ProductionTrackingAdvanceRequestDto request, CancellationToken ct)
    {
        var result = await repository.AdvancePieceStageAsync(request, ct);
        if (result?.Updated == true && !result.IsReadyMade && result.PieceId is > 0)
        {
            var piece = await repository.GetPieceByIdAsync(result.PieceId.Value, ct);
            if (piece is not null)
            {
                await orderLoyaltyIntegrationService.ProcessIfEligibleAsync(piece.OrderId, ct);
                if (result.BecameReadyForDelivery)
                {
                    var order = await orderRepository.GetByIdAsync(piece.OrderId, ct);
                    await TrySendMessageAsync(
                        order?.CustomerId ?? 0,
                        piece.OrderId,
                        "OrderReady",
                        "OrderReady",
                        order?.CustomerName,
                        order?.OrderNumber ?? piece.OrderNumber,
                        order?.PhoneNumber ?? string.Empty,
                        order?.TotalAmount ?? 0m,
                        ct);
                }
            }
        }

        return result;
    }

    private async Task TrySendMessageAsync(int customerId, int orderId, string eventType, string templateKey, string? customerName, string orderNumber, string phoneNumber, decimal totalAmount, CancellationToken cancellationToken)
    {
        if (messageWriteService is null || customerId <= 0)
            return;

        try
        {
            var context = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerName"] = customerName ?? "Customer",
                ["OrderNumber"] = orderNumber,
                ["OrderId"] = orderId,
                ["Amount"] = totalAmount,
                ["TotalAmount"] = totalAmount,
                ["PhoneNumber"] = phoneNumber,
                ["CustomerPhoneNumber"] = phoneNumber,
                ["CurrencyName"] = await GetSettingValueAsync("Currency", cancellationToken),
                ["StoreName"] = await GetSettingValueAsync("StoreName", cancellationToken),
            };

            var order = await orderRepository.GetByIdAsync(orderId, cancellationToken);
            if (order is not null)
            {
                context["PaidAmount"] = order.PaidAmount;
                context["RemainingAmount"] = order.RemainingAmount;
                context["DeliveryDate"] = order.DeliveryDate ?? DateTime.UtcNow;
                context["OrderDetails"] = order.OrderNumber;
                context["CustomerCode"] = await GetCustomerCodeAsync(customerId, cancellationToken);
            }

            await messageWriteService.TryCreateAsync(new MessageWriteRequest(
                customerId,
                orderId,
                eventType,
                templateKey,
                phoneNumber,
                orderNumber,
                "Order",
                context), cancellationToken);
        }
        catch
        {
            // Ignore message-side failure so production flow remains durable.
        }
    }

    private async Task<string?> GetCustomerCodeAsync(int customerId, CancellationToken cancellationToken)
    {
        if (customerRepository is null)
            return null;

        var customer = await customerRepository.GetByIdAsync(customerId, cancellationToken);
        return string.IsNullOrWhiteSpace(customer?.CustomerCode) ? null : customer.CustomerCode;
    }

    private async Task<string?> GetSettingValueAsync(string settingName, CancellationToken cancellationToken)
    {
        if (settingsRepository is null)
            return null;

        var setting = await settingsRepository.GetByKeyAsync(settingName, cancellationToken);
        return string.IsNullOrWhiteSpace(setting?.SettingValue) ? null : setting.SettingValue;
    }
}