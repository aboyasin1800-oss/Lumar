namespace LUMAR_ERP_API_V2.Utilities;

public sealed record CancelledPieceReadyInventoryTransferResult(
    bool IsEligible,
    bool IsAlreadyTransferred,
    string FinalStage,
    string? Reason);

public static class CancelledPieceReadyInventoryTransferGuard
{
    public static CancelledPieceReadyInventoryTransferResult Evaluate(
        bool orderCancelled,
        bool pieceStartedProduction,
        bool hasAssemblyStage,
        string? pieceStatus,
        string? finalStage,
        bool alreadyTransferred)
    {
        var normalizedStage = NormalizeStage(finalStage);
        if (alreadyTransferred)
        {
            return new CancelledPieceReadyInventoryTransferResult(false, true, normalizedStage,
                "The piece has already been transferred to ready inventory.");
        }

        if (!orderCancelled)
        {
            return new CancelledPieceReadyInventoryTransferResult(false, false, normalizedStage,
                "The related order must be cancelled before this transfer is considered.");
        }

        if (!pieceStartedProduction)
        {
            return new CancelledPieceReadyInventoryTransferResult(false, false, normalizedStage,
                "The piece must have started production before being considered for transfer.");
        }

        if (!hasAssemblyStage)
        {
            return new CancelledPieceReadyInventoryTransferResult(false, false, normalizedStage,
                "The piece must have a recorded Assembly stage before transfer.");
        }

        var normalizedStatus = NormalizeStatus(pieceStatus);
        if (!string.Equals(normalizedStage, "Assembly", StringComparison.OrdinalIgnoreCase))
        {
            return new CancelledPieceReadyInventoryTransferResult(false, false, normalizedStage,
                "The final recorded stage is not Assembly.");
        }

        if (!IsCompletedAssemblyStatus(normalizedStatus))
        {
            return new CancelledPieceReadyInventoryTransferResult(false, false, normalizedStage,
                "The piece is not in a completed assembly state.");
        }

        return new CancelledPieceReadyInventoryTransferResult(true, false, normalizedStage,
            "Eligible for transfer to ready inventory after cancelled-order completion at Assembly.");
    }

    private static bool IsCompletedAssemblyStatus(string? status)
    {
        return status is not null && (
            string.Equals(status, "Ready", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Assembly", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "ReadyForSale", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeStage(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    }

    private static string NormalizeStatus(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
