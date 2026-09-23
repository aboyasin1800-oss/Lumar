using LUMAR_ERP_API_V2.DTOs.Production;
namespace LUMAR_ERP_API_V2.Services;
public interface IScannerService
{
    Task<IReadOnlyList<ScannerDto>> GetScannersAsync(CancellationToken ct);
    Task<ScannerDto?> GetByIdAsync(int scannerId, CancellationToken ct);
    Task<ScannerDto?> CreateAsync(CreateScannerDto request, CancellationToken ct);
    Task<ScannerDto?> UpdateAsync(int scannerId, UpdateScannerDto request, CancellationToken ct);
    Task<ScannerDto?> ActivateAsync(int scannerId, CancellationToken ct);
    Task<ScannerDto?> DeactivateAsync(int scannerId, CancellationToken ct);
    Task<IReadOnlyList<LiveScanDto>> GetLiveScansAsync(CancellationToken ct);
}