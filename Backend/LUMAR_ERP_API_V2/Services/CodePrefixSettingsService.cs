using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class CodePrefixSettingsService(ICodePrefixSettingsRepository repository) : ICodePrefixSettingsService
{
    public Task<IReadOnlyList<CodePrefixSettingDto>> GetAllAsync(CancellationToken cancellationToken) => repository.GetAllAsync(cancellationToken);

    public Task<CodePrefixSettingDto?> GetByKeyAsync(string key, CancellationToken cancellationToken) => repository.GetByKeyAsync(key, cancellationToken);

    public Task<IReadOnlyList<CodePrefixSettingDto>> UpsertAsync(IReadOnlyList<CodePrefixSettingValueDto> settings, CancellationToken cancellationToken) => repository.UpsertAsync(settings, cancellationToken);
}
