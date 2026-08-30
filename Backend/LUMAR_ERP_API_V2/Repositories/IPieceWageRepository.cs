using LUMAR_ERP_API_V2.DTOs.Production;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IPieceWageRepository
{
    Task<IReadOnlyList<PieceWageDto>> GetByPieceIdAsync(int pieceId, CancellationToken cancellationToken);
}