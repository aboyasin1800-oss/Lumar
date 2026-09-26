using LUMAR_ERP_API_V2.DTOs.SalesReference;

namespace LUMAR_ERP_API_V2.Services;

public interface ISalesReferenceService
{
    Task<SalesReferenceVersionResponse> GetVersionAsync(CancellationToken cancellationToken);
    Task<SalesReferenceSnapshotResponse> GetSnapshotAsync(CancellationToken cancellationToken);
}