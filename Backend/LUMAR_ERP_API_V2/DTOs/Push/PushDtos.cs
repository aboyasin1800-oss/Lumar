namespace LUMAR_ERP_API_V2.DTOs.Push;

public sealed class RegisterPushDeviceRequest
{
    public string Provider { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string PushToken { get; set; } = string.Empty;
    public string? DeviceKey { get; set; }
    public string? DeviceName { get; set; }
    public string? AppVersion { get; set; }
}

public sealed record PushDeviceSummaryDto(
    int DeviceId,
    int MobileAccountId,
    string Provider,
    string Platform,
    string DeviceKey,
    bool IsActive,
    bool IsDisabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? LastRegisteredAtUtc,
    DateTime? DisabledAtUtc,
    string? DisabledReason,
    string? TokenCiphertext = null,
    string? TokenHash = null);

public sealed record PushDeliveryRequest(
    int? CustomerId,
    int? SupplierId,
    int? MobileAccountId,
    string Title,
    string Body,
    string? ReferenceType = null,
    string? ReferenceId = null,
    string? Channel = "Push");

public sealed record PushDeliveryResult(
    bool Success,
    string? ProviderMessageId,
    string? Status,
    string? Error,
    bool IsRetryable,
    int SentCount,
    int FailedCount,
    IReadOnlyList<string> DeviceKeys);

public sealed record PushDeviceTokenRecord(
    int DeviceId,
    int MobileAccountId,
    string Provider,
    string Platform,
    string DeviceKey,
    string TokenCiphertext,
    string TokenHash,
    string TokenValue,
    bool IsActive,
    bool IsDisabled,
    DateTime UpdatedAtUtc,
    DateTime? DisabledAtUtc,
    string? DisabledReason);
