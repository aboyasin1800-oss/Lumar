namespace LUMAR_ERP_API_V2.DTOs.Settings;

public sealed record CodePrefixSettingDto(
    string Key,
    string DisplayName,
    string CurrentValue,
    string DefaultValue,
    string Example
);

public sealed record CodePrefixSettingValueDto(
    string Key,
    string Value
);

public sealed record CodePrefixSettingsBatchDto(
    IReadOnlyList<CodePrefixSettingValueDto> Settings
);
