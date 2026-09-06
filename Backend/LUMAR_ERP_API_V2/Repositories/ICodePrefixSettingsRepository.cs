using LUMAR_ERP_API_V2.DTOs.Settings;

namespace LUMAR_ERP_API_V2.Repositories;

public interface ICodePrefixSettingsRepository
{
    Task<IReadOnlyList<CodePrefixSettingDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<CodePrefixSettingDto?> GetByKeyAsync(string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<CodePrefixSettingDto>> UpsertAsync(IReadOnlyList<CodePrefixSettingValueDto> settings, CancellationToken cancellationToken);
}
