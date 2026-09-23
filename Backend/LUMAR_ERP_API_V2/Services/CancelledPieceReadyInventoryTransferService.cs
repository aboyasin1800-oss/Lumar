using LUMAR_ERP_API_V2.Utilities;

namespace LUMAR_ERP_API_V2.Services;

public interface ICancelledPieceReadyInventoryTransferService
{
    CancelledPieceReadyInventoryTransferResult Evaluate(
        bool orderCancelled,
        bool pieceStartedProduction,
        bool hasAssemblyStage,
        string? pieceStatus,
        string? finalStage,
        bool alreadyTransferred);
}

public sealed class CancelledPieceReadyInventoryTransferService : ICancelledPieceReadyInventoryTransferService
{
    public CancelledPieceReadyInventoryTransferResult Evaluate(
        bool orderCancelled,
        bool pieceStartedProduction,
        bool hasAssemblyStage,
        string? pieceStatus,
        string? finalStage,
        bool alreadyTransferred)
        => CancelledPieceReadyInventoryTransferGuard.Evaluate(
            orderCancelled,
            pieceStartedProduction,
            hasAssemblyStage,
            pieceStatus,
            finalStage,
            alreadyTransferred);
}
