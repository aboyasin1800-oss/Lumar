using LUMAR_ERP_API_V2.DTOs.Production;
namespace LUMAR_ERP_API_V2.Services;
public interface IPieceWageService { Task<IReadOnlyList<PieceWageDto>> GetByPieceIdAsync(int id, CancellationToken ct); }