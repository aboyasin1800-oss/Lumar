using LUMAR_ERP_API_V2.DTOs.Pricing;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class PieceCostSettingService(IPieceCostSettingRepository repository) : IPieceCostSettingService
{
    public Task<IReadOnlyList<PieceCostSettingDto>> GetAllAsync(CancellationToken cancellationToken) => repository.GetAllAsync(cancellationToken);
    public Task<PieceCostSettingDto?> UpsertAsync(int productTypeId, UpsertPieceCostSettingDto setting, CancellationToken cancellationToken) => repository.UpsertAsync(productTypeId, setting, cancellationToken);
}