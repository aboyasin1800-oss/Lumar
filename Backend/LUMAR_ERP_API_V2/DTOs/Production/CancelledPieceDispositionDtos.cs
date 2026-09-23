namespace LUMAR_ERP_API_V2.DTOs.Production;

public sealed record CancelledPieceDispositionDto(
    int CancelledPieceDispositionId,
    int PieceId,
    string Decision,
    string? Reason,
    string? DecidedBy,
    DateTime DecidedAt,
    string TransferStatus,
    int? ReadyMadeInventoryProductId,
    DateTime? TransferredAt,
    DateTime CreatedAt);

public sealed class CancelledPieceDispositionDecisionRequestDto
{
    public int PieceId { get; init; }
    public string Decision { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? DecidedBy { get; init; }
}
