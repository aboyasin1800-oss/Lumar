namespace LUMAR_ERP_API_V2.DTOs.WhatsApp;

/// <summary>Gateway health, independent of WhatsApp account readiness.</summary>
public sealed record LocalWhatsAppHealthResponse(bool Healthy, int ContractVersion, bool OperationalActivation = false);

/// <summary>
/// Central Local WhatsApp status contract. ReadyToSend is the single source of truth for dispatch eligibility.
/// </summary>
public sealed record LocalWhatsAppSessionResponse(
    string State,
    string? CurrentNumber,
    string? LastError,
    bool Linked,
    bool OperationalActivation = false,
    bool ReadyToSend = false,
    bool GatewayAvailable = true,
    bool OwnerActionRequired = false,
    string? Warning = null);

/// <summary>Short-lived account linking material; never persisted or logged.</summary>
public sealed record LocalWhatsAppLinkResponse(string? Kind, string? Value, DateTimeOffset? ExpiresAt);