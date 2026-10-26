namespace LUMAR_ERP_API_V2.DTOs.WhatsApp;

/// <summary>Gateway health, independent of WhatsApp account readiness.</summary>
public sealed record LocalWhatsAppHealthResponse(bool Healthy, int ContractVersion, bool OperationalActivation = false);

/// <summary>Proven session readiness without operational provider activation.</summary>
public sealed record LocalWhatsAppSessionResponse(string State, string? CurrentNumber, string? LastError, bool Linked, bool OperationalActivation = false);

/// <summary>Short-lived account linking material; never persisted or logged.</summary>
public sealed record LocalWhatsAppLinkResponse(string? Kind, string? Value, DateTimeOffset? ExpiresAt);