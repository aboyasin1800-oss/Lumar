using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class ProductionService(
    IProductionRepository repository,
    IOrderLoyaltyIntegrationService orderLoyaltyIntegrationService,
    IOrderRepository orderRepository,
    IMessageWriteService? messageWriteService = null) : IProductionService
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
    public async Task<ProductionTrackingAdvanceResultDto?> AdvancePieceStageAsync(ProductionTrackingAdvanceRequestDto request, CancellationToken ct)
    {
        var result = await repository.AdvancePieceStageAsync(request, ct);
        if (result?.Updated == true && !result.IsReadyMade && result.PieceId is > 0)
        {
            var piece = await repository.GetPieceByIdAsync(result.PieceId.Value, ct);
            if (piece is not null)
            {
                await orderLoyaltyIntegrationService.ProcessIfEligibleAsync(piece.OrderId, ct);
                if (result.Message.Contains("ready for delivery", StringComparison.OrdinalIgnoreCase))
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
                        ct);
                }
            }
        }

        return result;
    }

    private async Task TrySendMessageAsync(int customerId, int orderId, string eventType, string templateKey, string? customerName, string orderNumber, string phoneNumber, CancellationToken cancellationToken)
    {
        if (messageWriteService is null || customerId <= 0)
            return;

        try
        {
            await messageWriteService.TryCreateAsync(new MessageWriteRequest(
                customerId,
                orderId,
                eventType,
                templateKey,
                phoneNumber,
                orderNumber,
                "Order",
                new Dictionary<string, object?>
                {
                    ["CustomerName"] = customerName ?? "Customer",
                    ["OrderNumber"] = orderNumber,
                    ["OrderId"] = orderId,
                }), cancellationToken);
        }
        catch
        {
            // Ignore message-side failure so production flow remains durable.
        }
    }
}