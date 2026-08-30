using LUMAR_ERP_API_V2.DTOs.Production;
using LUMAR_ERP_API_V2.Repositories;
namespace LUMAR_ERP_API_V2.Services;
public sealed class ScannerService(IScannerRepository repository) : IScannerService { public Task<IReadOnlyList<ScannerDto>> GetScannersAsync(CancellationToken ct) => repository.GetScannersAsync(ct); public Task<IReadOnlyList<LiveScanDto>> GetLiveScansAsync(CancellationToken ct) => repository.GetLiveScansAsync(ct); }