using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Repositories;
namespace LUMAR_ERP_API_V2.Services;
public sealed class ScannerService(IScannerRepository repository) : IScannerService
{
    public Task<IReadOnlyList<ScannerDto>> GetScannersAsync(CancellationToken ct) => repository.GetScannersAsync(ct);
    public Task<ScannerDto?> GetByIdAsync(int scannerId, CancellationToken ct) => repository.GetByIdAsync(scannerId, ct);
    public Task<ScannerDto?> CreateAsync(CreateScannerDto request, CancellationToken ct) => repository.CreateAsync(request, ct);
    public Task<ScannerDto?> UpdateAsync(int scannerId, UpdateScannerDto request, CancellationToken ct) => repository.UpdateAsync(scannerId, request, ct);
    public Task<ScannerDto?> ActivateAsync(int scannerId, CancellationToken ct) => repository.ActivateAsync(scannerId, ct);
    public Task<ScannerDto?> DeactivateAsync(int scannerId, CancellationToken ct) => repository.DeactivateAsync(scannerId, ct);
    public Task<IReadOnlyList<LiveScanDto>> GetLiveScansAsync(CancellationToken ct) => repository.GetLiveScansAsync(ct);
}