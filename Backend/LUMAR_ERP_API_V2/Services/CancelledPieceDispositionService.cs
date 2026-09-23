using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class CancelledPieceDispositionService(ICancelledPieceDispositionRepository repository) : ICancelledPieceDispositionService
{
    public Task<CancelledPieceDispositionDto?> GetByPieceIdAsync(int pieceId, CancellationToken cancellationToken) => repository.GetByPieceIdAsync(pieceId, cancellationToken);

    public Task<CancelledPieceDispositionDto> SaveDecisionAsync(int pieceId, string decision, string? reason, string? decidedBy, CancellationToken cancellationToken)
        => repository.SaveDecisionAsync(pieceId, decision, reason, decidedBy, cancellationToken);

    public Task<CancelledPieceDispositionDto?> ExecuteDecisionAsync(int pieceId, CancellationToken cancellationToken)
        => repository.ExecuteDecisionAsync(pieceId, cancellationToken);
}
