using LUMAR_ERP_API_V2.DTOs.Settings;

namespace LUMAR_ERP_API_V2.Services;

public interface IWhatsAppConnectionSettingsStore
{
    Task<WhatsAppConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken);
    Task<WhatsAppRuntimeSettings> GetRuntimeSettingsAsync(CancellationToken cancellationToken);
    Task<WhatsAppConnectionStatusDto> UpdateAsync(WhatsAppConnectionSettingsUpdateDto request, CancellationToken cancellationToken);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken);
}
