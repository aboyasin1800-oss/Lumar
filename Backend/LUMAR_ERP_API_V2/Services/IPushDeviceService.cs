using LUMAR_ERP_API_V2.DTOs.Push;

namespace LUMAR_ERP_API_V2.Services;

public interface IPushDeviceService
{
    Task<PushDeviceSummaryDto?> RegisterAsync(RegisterPushDeviceRequest request, CancellationToken cancellationToken);
    Task<PushDeviceSummaryDto?> RefreshAsync(int deviceId, RegisterPushDeviceRequest request, CancellationToken cancellationToken);
    Task<PushDeviceSummaryDto?> DisableAsync(int deviceId, string reason, CancellationToken cancellationToken);
    Task<IReadOnlyList<PushDeviceSummaryDto>> GetActiveAsync(CancellationToken cancellationToken);
    Task<PushDeliveryResult> DispatchAsync(PushDeliveryRequest request, CancellationToken cancellationToken);
    Task<PushDeliveryResult> DispatchForOfficialNotificationAsync(
        int? customerId,
        int? employeeId,
        int? supplierId,
        string title,
        string body,
        string referenceType,
        string referenceId,
        CancellationToken cancellationToken);
}