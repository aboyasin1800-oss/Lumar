namespace LUMAR_ERP_API_V2.DTOs.Messages;

public enum OutboundDispatchOutcome
{
    Sent,
    TransientFailure,
    PermanentFailure
}

public sealed record OutboundDispatchResult(
    int CustomerMessageId,
    string EventType,
    string Channel,
    bool Sent,
    string? ProviderMessageId,
    string? FailureReason,
    int AttemptCount,
    string DeliveryStatus);

public sealed record OutboundAdapterResult(
    OutboundDispatchOutcome Outcome,
    string? ProviderMessageId = null,
    string? FailureReason = null,
    bool Retryable = false);
