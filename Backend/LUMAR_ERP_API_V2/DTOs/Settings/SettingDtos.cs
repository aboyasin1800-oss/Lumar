namespace LUMAR_ERP_API_V2.DTOs.Settings;
public sealed record SystemSettingListDto(int SettingId,string? SettingName,string Category,string? Description);
public sealed record SystemSettingDetailsDto(int SettingId,string? SettingName,string? SettingValue,string Category,string? Description);
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
public sealed record UpdatePrintSettingsDto(
    string? MeasurementCardHeaderImage,
    string? MeasurementCardHeaderUseImage,
    string? MeasurementCardHeaderName,
    string? MeasurementCardLocation,
    string? MeasurementCardPhone1,
    string? MeasurementCardPhone2);