using LUMAR_ERP_API_V2.DTOs.Production;
namespace LUMAR_ERP_API_V2.Services;
public interface IScannerService { Task<IReadOnlyList<ScannerDto>> GetScannersAsync(CancellationToken ct); Task<IReadOnlyList<LiveScanDto>> GetLiveScansAsync(CancellationToken ct); }