using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Utilities;

namespace LUMAR_ERP_API_V2.Services;

public interface IProductionTrackingService
{
    IReadOnlyList<string> GetRoute(string? pieceType);
    string? GetNextStage(string? pieceType, string? currentStage);
    ProductionTrackingTransitionValidation ValidateTransition(string? pieceType, string? currentStage, string? requestedStage);
    ProductionTrackingAdvanceResultDto AdvancePiece(string? pieceType, string? currentStatus, string? requestedStage, int? pieceId = null, string? trackingCode = null);
    string DetermineOrderStatus(int completedPieceCount, int totalRequiredPieces, int cancelledPieces, bool hasBlockedPieces, string? orderStatus);
}