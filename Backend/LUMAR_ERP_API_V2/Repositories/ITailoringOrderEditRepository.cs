using LUMAR_ERP_API_V2.DTOs.Orders;

namespace LUMAR_ERP_API_V2.Repositories;

public interface ITailoringOrderEditRepository
{
    Task<TailoringOrderEditSnapshot?> GetEditSnapshotAsync(int orderId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<int, decimal>?> GetCapturedBasePointsAsync(int orderId, CancellationToken cancellationToken);
    Task<bool> HasCompletedEditAsync(int orderId, CancellationToken cancellationToken);
    Task CaptureCreationBenefitsAsync(int orderId, CancellationToken cancellationToken);
}