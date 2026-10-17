namespace LUMAR_ERP_API_V2.DTOs.Settings;
public sealed record SystemSettingListDto(int SettingId,string? SettingName,string Category,string? Description);
public sealed record SystemSettingDetailsDto(int SettingId,string? SettingName,string? SettingValue,string Category,string? Description);
public sealed record GeneralCashAccountSettingDto(int? CashAccountId, string? AccountName, bool IsConfigured, string? Message);
public sealed record UpdateGeneralCashAccountSettingDto(int? CashAccountId);
public sealed record SettingCategoryDto(string Category,int SettingCount);
public sealed record ProductionRouteEntryDto
{
    public int ProductTypeId { get; init; }
    public string ProductTypeCode { get; init; } = string.Empty;
    public string ProductTypeNameAr { get; init; } = string.Empty;
    public IReadOnlyList<string> Stages { get; init; } = Array.Empty<string>();
    public bool IsEnabled { get; init; } = true;
    public string? PieceType { get; init; }
}
public sealed record ProductionRoutesConfigDto(IReadOnlyList<ProductionRouteEntryDto> Routes);
public sealed record UpdateCurrencySettingsDto(string CurrencyName, string PreferredCurrency);
public sealed record MessageSettingsDto(
    bool MessageMasterEnabled,
    bool DirectReferralDefaultOn,
    bool OtherReferralDefaultOff,
    bool FridayGreetingEnabled,
    bool CampaignEnabled,
    bool WhatsAppEnabled = true);
public sealed record WhatsAppConnectionSettingsDto(
    bool Enabled,
    string BaseUrl,
    string ApiVersion,
    string PhoneNumberId,
    string? AccessToken,
    string? VerifyToken,
    string? AppSecret,
    int TimeoutSeconds,
    bool IsConfigured,
    string? AccessTokenMasked,
    string? VerifyTokenMasked,
    string? AppSecretMasked,
    string? Message);
public sealed record UpdatePrintSettingsDto(
    string? MeasurementCardHeaderImage,
    string? MeasurementCardHeaderUseImage,
    string? MeasurementCardHeaderName,
    string? MeasurementCardLocation,
    string? MeasurementCardPhone1,
    string? MeasurementCardPhone2);