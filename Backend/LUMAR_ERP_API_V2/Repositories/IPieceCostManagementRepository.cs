using LUMAR_ERP_API_V2.DTOs.Pricing;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IPieceCostManagementRepository
{
    Task<IReadOnlyList<PieceCostManagementDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<PieceCostManagementDto?> UpdateAsync(
        int productTypeId,
        UpdatePieceCostManagementDto request,
        CancellationToken cancellationToken);
}