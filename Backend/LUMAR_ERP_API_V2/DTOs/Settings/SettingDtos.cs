namespace LUMAR_ERP_API_V2.DTOs.Settings;
public sealed record SystemSettingListDto(int SettingId,string? SettingName,string Category,string? Description);
public sealed record SystemSettingDetailsDto(int SettingId,string? SettingName,string? SettingValue,string Category,string? Description);
public sealed record SettingCategoryDto(string Category,int SettingCount);