namespace LUMAR_ERP_API_V2.DTOs.Settings;

public sealed record WhatsAppConnectionStatusDto(
    bool Enabled,
    string PhoneNumberId,
    bool TokenConfigured,
    bool ConnectionConfigured,
    bool HasSecretAccessToken,
    string? AccessTokenMasked,
    string? Message);

public sealed record WhatsAppConnectionSettingsUpdateDto(
    bool Enabled = true,
    string? PhoneNumberId = null,
    string? AccessToken = null,
    string? VerifyToken = null,
    string? AppSecret = null,
    string? BaseUrl = null,
    string? ApiVersion = null,
    int TimeoutSeconds = 30);
