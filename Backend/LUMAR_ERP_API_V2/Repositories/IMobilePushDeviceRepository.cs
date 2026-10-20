using LUMAR_ERP_API_V2.DTOs.Push;

namespace LUMAR_ERP_API_V2.Repositories;

public interface IMobilePushDeviceRepository
{
    Task<PushDeviceSummaryDto?> RegisterAsync(RegisterPushDeviceRequest request, int mobileAccountId, CancellationToken cancellationToken);
    Task<PushDeviceSummaryDto?> RefreshAsync(int deviceId, RegisterPushDeviceRequest request, CancellationToken cancellationToken);
    Task<PushDeviceSummaryDto?> DisableAsync(int deviceId, string reason, CancellationToken cancellationToken);
    Task<IReadOnlyList<PushDeviceSummaryDto>> GetActiveAsync(int mobileAccountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PushDeviceTokenRecord>> GetActiveTokensAsync(int mobileAccountId, CancellationToken cancellationToken);
    Task<PushDeviceSummaryDto?> GetByDeviceIdAsync(int deviceId, CancellationToken cancellationToken);
    Task<bool> IsTokenRegisteredToAnotherAccountAsync(string tokenHash, int mobileAccountId, CancellationToken cancellationToken);
}