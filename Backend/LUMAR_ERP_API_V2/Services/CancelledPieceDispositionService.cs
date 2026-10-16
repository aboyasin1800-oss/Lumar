using LUMAR_ERP_API_V2.DTOs.Messages;
using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class CancelledPieceDispositionService(
    ICancelledPieceDispositionRepository repository,
    IProductionRepository productionRepository,
    IOrderRepository orderRepository,
    IMessageWriteService? messageWriteService = null) : ICancelledPieceDispositionService
{
    public Task<CancelledPieceDispositionDto?> GetByPieceIdAsync(int pieceId, CancellationToken cancellationToken) => repository.GetByPieceIdAsync(pieceId, cancellationToken);

    public async Task<CancelledPieceDispositionDto> SaveDecisionAsync(int pieceId, string decision, string? reason, string? decidedBy, CancellationToken cancellationToken)
    {
        var saved = await repository.SaveDecisionAsync(pieceId, decision, reason, decidedBy, cancellationToken);
        await EmitPieceCancelledMessageAsync(pieceId, cancellationToken);
        return saved;
    }

    public async Task<CancelledPieceDispositionDto?> ExecuteDecisionAsync(int pieceId, CancellationToken cancellationToken)
    {
        var executed = await repository.ExecuteDecisionAsync(pieceId, cancellationToken);
        await EmitPieceCancelledMessageAsync(pieceId, cancellationToken);
        return executed;
    }

    private async Task EmitPieceCancelledMessageAsync(int pieceId, CancellationToken cancellationToken)
    {
        if (messageWriteService is null)
            return;

        var piece = await productionRepository.GetPieceByIdAsync(pieceId, cancellationToken);
        if (piece is null)
            return;

        var order = await orderRepository.GetByIdAsync(piece.OrderId, cancellationToken);
        if (order is null)
            return;

        try
        {
            await messageWriteService.TryCreateAsync(new MessageWriteRequest(
                order.CustomerId,
                order.OrderId,
                "PieceCancelled",
                "PieceCancelled",
                order.PhoneNumber ?? string.Empty,
                $"{order.OrderNumber}:{piece.PieceNumber}",
                "Piece",
                new Dictionary<string, object?>
                {
                    ["CustomerName"] = order.CustomerName ?? "Customer",
                    ["OrderNumber"] = order.OrderNumber,
                    ["OrderId"] = order.OrderId,
                    ["PieceId"] = piece.PieceId,
                    ["PieceNumber"] = piece.PieceNumber,
                    ["PieceType"] = piece.PieceType,
                    ["TrackingCode"] = piece.TrackingCode,
                }), cancellationToken);
        }
        catch
        {
            // Piece cancellation remains valid even if the internal message side is unavailable.
        }
    }
}
