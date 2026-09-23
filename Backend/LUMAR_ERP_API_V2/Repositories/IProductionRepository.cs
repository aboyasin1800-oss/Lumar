using LUMAR_ERP_API_V2.DTOs.Production;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IProductionRepository
{
    Task<IReadOnlyList<PieceDto>> GetPiecesAsync(CancellationToken cancellationToken);
    Task<PieceDto?> GetPieceByIdAsync(int pieceId, CancellationToken cancellationToken);
    Task<WorkCardDto?> GetWorkCardAsync(int pieceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrackingEventDto>> GetPieceTrackingAsync(int pieceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductionStageDto>> GetStagesAsync(CancellationToken cancellationToken);
    Task<ProductionDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<FactoryMonitoringDashboardDto> GetFactoryMonitoringAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ReadyMadeProductionOrderDto>> GetReadyMadeOrdersAsync(CancellationToken cancellationToken);
    Task<ReadyMadeProductionOrderDto?> GetReadyMadeOrderByIdAsync(int orderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReadyMadeProductionOrderItemDto>> GetReadyMadeOrderItemsAsync(int orderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReadyMadeProductionPieceDto>> GetReadyMadeItemPiecesAsync(int itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrackingEventDto>> GetReadyMadePieceTrackingAsync(int pieceInstanceId, CancellationToken cancellationToken);
    Task<ReadyMadeProductionOrderCreateResultDto> CreateReadyMadeOrderAsync(ReadyMadeProductionOrderCreateDto order, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductionDeliveryDto>> GetDeliveriesAsync(CancellationToken cancellationToken);
    Task<ProductionTrackingRouteDto?> GetPieceRouteAsync(int pieceId, CancellationToken cancellationToken);
    Task<ProductionTrackingRouteDto?> GetPieceRouteByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken);
    Task<ProductionTrackingAdvanceResultDto?> AdvancePieceStageAsync(ProductionTrackingAdvanceRequestDto request, CancellationToken cancellationToken);
}