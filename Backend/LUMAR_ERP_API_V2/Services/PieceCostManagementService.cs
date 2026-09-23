using LUMAR_ERP_API_V2.DTOs.Pricing;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class PieceCostManagementService(IPieceCostManagementRepository repository)
    : IPieceCostManagementService
{
    public Task<IReadOnlyList<PieceCostManagementDto>> GetAllAsync(CancellationToken cancellationToken) =>
        repository.GetAllAsync(cancellationToken);

    public Task<PieceCostManagementDto?> UpdateAsync(
        int productTypeId,
        UpdatePieceCostManagementDto request,
        CancellationToken cancellationToken) =>
        repository.UpdateAsync(productTypeId, request, cancellationToken);
}