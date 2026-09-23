using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Utilities;

namespace LUMAR_ERP_API_V2.Services;

public sealed class ProductionTrackingService : IProductionTrackingService
{
    public IReadOnlyList<string> GetRoute(string? pieceType) => ProductionTrackingEngine.GetRoute(pieceType);

    public string? GetNextStage(string? pieceType, string? currentStage) => ProductionTrackingEngine.GetNextStage(pieceType, currentStage);

    public ProductionTrackingTransitionValidation ValidateTransition(string? pieceType, string? currentStage, string? requestedStage) =>
        ProductionTrackingEngine.ValidateTransition(pieceType, currentStage, requestedStage);

    public ProductionTrackingAdvanceResultDto AdvancePiece(string? pieceType, string? currentStatus, string? requestedStage, int? pieceId = null, string? trackingCode = null) =>
        ProductionTrackingEngine.AdvancePiece(pieceType, currentStatus, requestedStage, pieceId, trackingCode);

    public string DetermineOrderStatus(int completedPieceCount, int totalRequiredPieces, int cancelledPieces, bool hasBlockedPieces, string? orderStatus) =>
        ProductionTrackingEngine.DetermineOrderStatus(completedPieceCount, totalRequiredPieces, cancelledPieces, hasBlockedPieces, orderStatus);
}
