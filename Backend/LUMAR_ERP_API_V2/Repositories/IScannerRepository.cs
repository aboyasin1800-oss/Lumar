using LUMAR_ERP_API_V2.DTOs.Production;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IScannerRepository
{
    Task<IReadOnlyList<ScannerDto>> GetScannersAsync(CancellationToken cancellationToken);
    Task<ScannerDto?> GetByIdAsync(int scannerId, CancellationToken cancellationToken);
    Task<ScannerDto?> CreateAsync(CreateScannerDto request, CancellationToken cancellationToken);
    Task<ScannerDto?> UpdateAsync(int scannerId, UpdateScannerDto request, CancellationToken cancellationToken);
    Task<ScannerDto?> ActivateAsync(int scannerId, CancellationToken cancellationToken);
    Task<ScannerDto?> DeactivateAsync(int scannerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LiveScanDto>> GetLiveScansAsync(CancellationToken cancellationToken);
}