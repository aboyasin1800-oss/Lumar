using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Repositories;
namespace LUMAR_ERP_API_V2.Services;
public sealed class PieceWageService(IPieceWageRepository repository) : IPieceWageService { public Task<IReadOnlyList<PieceWageDto>> GetByPieceIdAsync(int id, CancellationToken ct) => repository.GetByPieceIdAsync(id, ct); }