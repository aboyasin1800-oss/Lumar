using LUMAR_ERP_API_V2.DTOs.Push;

namespace LUMAR_ERP_API_V2.Services;

public sealed class FirebasePushSender(ILogger<FirebasePushSender>? logger = null) : IPushSender
{
    public string Provider => "FCM";

    public Task<PushSendOutcome> SendAsync(PushDeviceTokenRecord device, string title, string body, string? referenceType, string? referenceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(device.TokenValue))
            return Task.FromResult(new PushSendOutcome(false, null, "Token is empty or unreadable.", false, "Failed"));

        logger?.LogInformation("FCM sender is configured as backend-ready but not live-enabled. Device {DeviceId} token hash {TokenHash} has been accepted for future configuration.", device.DeviceId, device.TokenHash);

        return Task.FromResult(new PushSendOutcome(false, null, "FCM credentials are not configured in this environment. Push is backend-ready but not live-enabled.", false, "NotConfigured"));
    }
}
