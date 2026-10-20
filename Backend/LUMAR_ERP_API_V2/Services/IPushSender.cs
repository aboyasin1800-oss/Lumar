using LUMAR_ERP_API_V2.DTOs.Push;

namespace LUMAR_ERP_API_V2.Services;

public interface IPushSender
{
    string Provider { get; }
    Task<PushSendOutcome> SendAsync(PushDeviceTokenRecord device, string title, string body, string? referenceType, string? referenceId, CancellationToken cancellationToken);
}

public sealed record PushSendOutcome(bool Success, string? ProviderMessageId, string? Error, bool IsRetryable, string Status);
