using LUMAR_ERP_API_V2.DTOs.Pricing;

namespace LUMAR_ERP_API_V2.Services;

public interface IPieceCostSettingService
{
    Task<IReadOnlyList<PieceCostSettingDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<PieceCostSettingDto?> UpsertAsync(int productTypeId, UpsertPieceCostSettingDto setting, CancellationToken cancellationToken);
}