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
    Task<IReadOnlyList<ReadyMadeProductionOrderDto>> GetReadyMadeOrdersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ReadyMadeProductionOrderItemDto>> GetReadyMadeOrderItemsAsync(int orderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReadyMadeProductionPieceDto>> GetReadyMadeItemPiecesAsync(int itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductionDeliveryDto>> GetDeliveriesAsync(CancellationToken cancellationToken);
}