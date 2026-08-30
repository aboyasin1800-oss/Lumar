using LUMAR_ERP_API_V2.DTOs.Production;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IScannerRepository
{
    Task<IReadOnlyList<ScannerDto>> GetScannersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LiveScanDto>> GetLiveScansAsync(CancellationToken cancellationToken);
}