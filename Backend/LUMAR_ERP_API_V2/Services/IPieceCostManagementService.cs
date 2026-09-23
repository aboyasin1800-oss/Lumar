using LUMAR_ERP_API_V2.DTOs.Pricing;

namespace LUMAR_ERP_API_V2.Services;

public interface IPieceCostManagementService
{
    Task<IReadOnlyList<PieceCostManagementDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<PieceCostManagementDto?> UpdateAsync(
        int productTypeId,
        UpdatePieceCostManagementDto request,
        CancellationToken cancellationToken);
}