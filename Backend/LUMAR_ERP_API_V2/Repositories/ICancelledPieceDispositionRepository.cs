using LUMAR_ERP_API_V2.DTOs.Production;

namespace LUMAR_ERP_API_V2.Repositories;

public interface ICancelledPieceDispositionRepository
{
    Task<CancelledPieceDispositionDto?> GetByPieceIdAsync(int pieceId, CancellationToken cancellationToken);
    Task<CancelledPieceDispositionDto> SaveDecisionAsync(int pieceId, string decision, string? reason, string? decidedBy, CancellationToken cancellationToken);
    Task<CancelledPieceDispositionDto?> ExecuteDecisionAsync(int pieceId, CancellationToken cancellationToken);
}
